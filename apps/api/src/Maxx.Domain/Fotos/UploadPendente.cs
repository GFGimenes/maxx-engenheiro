using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;

namespace Maxx.Domain.Fotos;

// O pedido de envio de uma foto, nas duas derivadas que o app gera (ADR 0003): a do feed
// (~1080²) e a da grade (~320²). A API assina as duas URLs e registra o pedido; quem usa a
// foto (o post, a foto de perfil) confirma. O que não for confirmado sai do bucket e do
// banco pela fila, passado o prazo de limpeza (ADR 0002).
public sealed class UploadPendente
{
    // O app reencoda tudo antes de enviar (ADR 0003): só JPEG chega ao bucket.
    public const string TipoAceito = "image/jpeg";

    // 1 MiB e 200 KiB: folga sobre um JPEG de 1080² e de 320² em qualidade de foto.
    public const long TamanhoMaximoDoFeed = 1024 * 1024;
    public const long TamanhoMaximoDaGrade = 200 * 1024;

    // Quanto tempo o app tem para fazer o PUT depois de pedir.
    public static readonly TimeSpan ValidadeDaAssinatura = TimeSpan.FromMinutes(15);

    // Quando a fila apaga o que ninguém confirmou. Bem depois da validade: um envio lento
    // terminado no último minuto ainda tem horas para virar post.
    public static readonly TimeSpan PrazoDeLimpeza = TimeSpan.FromHours(24);

    // Para o EF Core materializar do banco.
    private UploadPendente()
    {
        ChaveDoFeed = null!;
        ChaveDaGrade = null!;
        TipoDeConteudo = null!;
    }

    private UploadPendente(
        Guid usuarioId, FinalidadeDoUpload finalidade, string identificador, long tamanhoDoFeed, long tamanhoDaGrade, DateTimeOffset agora)
    {
        Id = Guid.CreateVersion7();
        UsuarioId = usuarioId;
        Finalidade = finalidade;
        ChaveDoFeed = $"{identificador}/feed.jpg";
        ChaveDaGrade = $"{identificador}/grade.jpg";
        TipoDeConteudo = TipoAceito;
        TamanhoDoFeed = tamanhoDoFeed;
        TamanhoDaGrade = tamanhoDaGrade;
        CriadoEm = agora;
    }

    public Guid Id { get; private set; }

    public Guid UsuarioId { get; private set; }

    public FinalidadeDoUpload Finalidade { get; private set; }

    // Aleatórias: a leitura é pública para quem tem o link.
    public string ChaveDoFeed { get; private set; }

    public string ChaveDaGrade { get; private set; }

    public string TipoDeConteudo { get; private set; }

    public long TamanhoDoFeed { get; private set; }

    public long TamanhoDaGrade { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset? ConfirmadoEm { get; private set; }

    public bool EstaConfirmado => ConfirmadoEm is not null;

    public DateTimeOffset AssinaturaExpiraEm => CriadoEm + ValidadeDaAssinatura;

    public DateTimeOffset LimparEm => CriadoEm + PrazoDeLimpeza;

    public IReadOnlyList<string> Chaves => [ChaveDoFeed, ChaveDaGrade];

    // Quem usa a foto confirma, e a limpeza da fila passa a deixá-la em paz. Uma vez só: a
    // mesma foto não vira dois posts.
    internal void Confirmar(DateTimeOffset agora)
    {
        if (EstaConfirmado)
        {
            throw new DomainException($"O upload {Id} já foi usado.");
        }

        ConfirmadoEm = agora;
    }

    // Recusa em vez de exceção: a borda responde com o código de cada caso. O tipo é
    // comparado como o HTTP compara, sem diferenciar maiúsculas, e gravado na forma canônica,
    // que é a que vai presa na assinatura.
    public static ResultadoDoPedidoDeUpload Pedir(
        Usuario usuario,
        FinalidadeDoUpload finalidade,
        string? tipoDeConteudo,
        long tamanhoDoFeed,
        long tamanhoDaGrade,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        if (!PodeEnviar(usuario, finalidade))
        {
            return new(null, RecusaDoUpload.FinalidadeNaoPermitida);
        }

        if (!string.Equals(tipoDeConteudo?.Trim(), TipoAceito, StringComparison.OrdinalIgnoreCase))
        {
            return new(null, RecusaDoUpload.TipoNaoAceito);
        }

        if (tamanhoDoFeed is < 1 or > TamanhoMaximoDoFeed || tamanhoDaGrade is < 1 or > TamanhoMaximoDaGrade)
        {
            return new(null, RecusaDoUpload.TamanhoInvalido);
        }

        return new(new UploadPendente(usuario.Id, finalidade, Ulid.Novo(agora), tamanhoDoFeed, tamanhoDaGrade, agora), null);
    }

    // Post é só de aluno que não virou ex-aluno (produto.md 2.2 e 6.1). Foto de perfil, de
    // aluno e de professor (produto.md 2.3); a instituição não tem perfil social. Foto de
    // aviso, de quem publica aviso: professor e instituição (produto.md 7, passo 10).
    public static bool PodeEnviar(Usuario usuario, FinalidadeDoUpload finalidade)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        return finalidade switch
        {
            FinalidadeDoUpload.Post => usuario.Papeis == Papeis.Aluno && !usuario.EhExAluno,
            FinalidadeDoUpload.FotoDePerfil => usuario.Papeis.HasFlag(Papeis.Aluno) || usuario.Papeis.HasFlag(Papeis.Professor),
            FinalidadeDoUpload.Aviso => usuario.Papeis.HasFlag(Papeis.Professor) || usuario.Papeis.HasFlag(Papeis.Instituicao),
            _ => false,
        };
    }
}

public enum FinalidadeDoUpload
{
    Post,
    FotoDePerfil,
    Aviso,
}

public enum RecusaDoUpload
{
    FinalidadeNaoPermitida,
    TipoNaoAceito,
    TamanhoInvalido,
}

public sealed record ResultadoDoPedidoDeUpload(UploadPendente? Upload, RecusaDoUpload? Recusa);
