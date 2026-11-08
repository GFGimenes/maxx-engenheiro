using Maxx.Domain.Academico;
using Maxx.Domain.Fotos;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;

namespace Maxx.Domain.Social;

// O post do feed (produto.md 6.1): ao menos um entre prêmio, foto e legenda. Um prêmio
// rende um post. Só aluno ativo publica; o ex-aluno mantém os que já tinha. A foto são as
// duas derivadas de um upload confirmado na publicação (ADR 0002 e 0003).
//
// No passo 7 o prêmio é a medalha; o prêmio de participação e a conquista entram como
// outras referências nos passos 8 e 9, nunca mais de uma por post.
public sealed class Post
{
    // Em caracteres, contando um emoji como um, como no nome de exibição.
    public const int TamanhoMaximoDaLegenda = 1000;

    // Para o EF Core materializar do banco.
    private Post()
    {
    }

    private Post(Guid autorId, string? legenda, UploadPendente? upload, Medalha? medalha, DateTimeOffset agora)
    {
        Id = Guid.CreateVersion7();
        AutorId = autorId;
        Legenda = legenda;
        ChaveDaFotoDoFeed = upload?.ChaveDoFeed;
        ChaveDaFotoDaGrade = upload?.ChaveDaGrade;
        MedalhaId = medalha?.Id;
        CriadoEm = agora;
    }

    public Guid Id { get; private set; }

    public Guid AutorId { get; private set; }

    public string? Legenda { get; private set; }

    public string? ChaveDaFotoDoFeed { get; private set; }

    public string? ChaveDaFotoDaGrade { get; private set; }

    public Guid? MedalhaId { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    // Remoção pela instituição (produto.md 2.1): o post some para todos, e fica quem
    // removeu e quando. O autor que apaga o próprio post o apaga de verdade.
    public DateTimeOffset? RemovidoEm { get; private set; }

    public Guid? RemovidoPorId { get; private set; }

    public bool TemFoto => ChaveDaFotoDoFeed is not null;

    public bool TemLegenda => Legenda is not null;

    public bool TemPremio => MedalhaId is not null;

    public bool EstaRemovido => RemovidoEm is not null;

    public IReadOnlyList<string> ChavesDaFoto => TemFoto ? [ChaveDaFotoDoFeed!, ChaveDaFotoDaGrade!] : [];

    // O upload e a medalha chegam já buscados; se a medalha já tem post é pergunta ao banco,
    // feita antes por quem chama e garantida pelo índice único. Publicado, o upload fica
    // confirmado e sai do alcance da limpeza.
    public static ResultadoDaPublicacao Publicar(
        Usuario autor, string? legenda, UploadPendente? upload, Medalha? medalha, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(autor);

        if (autor.Papeis != Papeis.Aluno || autor.EhExAluno)
        {
            return Recusa(RecusaDaPublicacao.AutorNaoPublica);
        }

        legenda = string.IsNullOrWhiteSpace(legenda) ? null : legenda.Trim();
        if (legenda is not null && legenda.EnumerateRunes().Count() > TamanhoMaximoDaLegenda)
        {
            return Recusa(RecusaDaPublicacao.LegendaLonga);
        }

        if (upload is not null)
        {
            if (upload.UsuarioId != autor.Id || upload.Finalidade != FinalidadeDoUpload.Post)
            {
                return Recusa(RecusaDaPublicacao.UploadNaoEncontrado);
            }

            if (upload.EstaConfirmado)
            {
                return Recusa(RecusaDaPublicacao.UploadJaUsado);
            }
        }

        if (medalha is not null)
        {
            if (medalha.AlunoId != autor.Id)
            {
                return Recusa(RecusaDaPublicacao.PremioNaoEncontrado);
            }

            if (!medalha.EstaAtiva)
            {
                return Recusa(RecusaDaPublicacao.PremioAnulado);
            }
        }

        if (legenda is null && upload is null && medalha is null)
        {
            return Recusa(RecusaDaPublicacao.Vazio);
        }

        upload?.Confirmar(agora);
        return new(new Post(autor.Id, legenda, upload, medalha, agora), null);
    }

    // Quem pede a remoção decide o modo: o autor apaga, a instituição remove. Ninguém mais,
    // nem o professor; post já removido não volta a ser removido.
    public ModoDeRemocao ModoDeRemocaoPor(Usuario quem)
    {
        ArgumentNullException.ThrowIfNull(quem);

        if (EstaRemovido)
        {
            return ModoDeRemocao.NaoPermitida;
        }

        if (quem.Id == AutorId)
        {
            return ModoDeRemocao.Apagar;
        }

        return quem.Papeis.HasFlag(Papeis.Instituicao) ? ModoDeRemocao.RemoverPelaInstituicao : ModoDeRemocao.NaoPermitida;
    }

    public void RemoverPelaInstituicao(Usuario instituicao, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(instituicao);
        if (ModoDeRemocaoPor(instituicao) != ModoDeRemocao.RemoverPelaInstituicao)
        {
            throw new DomainException($"@{instituicao.Prefixo} não remove o post {Id} como instituição.");
        }

        RemovidoEm = agora;
        RemovidoPorId = instituicao.Id;
    }

    // Medalha anulada (decisão do passo 7, produto.md 4.8): o post perde o vínculo e fica
    // como post comum. Sem foto nem legenda ele ficaria vazio, e post vazio não existe: quem
    // chama o apaga em vez de desvincular.
    public bool FicariaVazioSemPremio => !TemFoto && !TemLegenda;

    public void DesvincularPremio()
    {
        if (FicariaVazioSemPremio)
        {
            throw new DomainException($"O post {Id} ficaria vazio sem o prêmio: apague em vez de desvincular.");
        }

        MedalhaId = null;
    }

    private static ResultadoDaPublicacao Recusa(RecusaDaPublicacao recusa) => new(null, recusa);
}

public enum RecusaDaPublicacao
{
    AutorNaoPublica,
    LegendaLonga,
    Vazio,

    // Não existe, é de outra pessoa ou é de outra finalidade: para quem pede, dá no mesmo.
    UploadNaoEncontrado,
    UploadJaUsado,
    PremioNaoEncontrado,
    PremioAnulado,
}

public enum ModoDeRemocao
{
    NaoPermitida,
    Apagar,
    RemoverPelaInstituicao,
}

public sealed record ResultadoDaPublicacao(Post? Post, RecusaDaPublicacao? Recusa);
