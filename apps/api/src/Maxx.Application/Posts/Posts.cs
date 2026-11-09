using Maxx.Application.Autenticacao;
using Maxx.Application.Conquistas;
using Maxx.Application.Fila;
using Maxx.Application.Fotos;
using Maxx.Application.Medalhas;
using Maxx.Application.Premios;
using Maxx.Application.PremiosParticipacao;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Fotos;
using Maxx.Domain.Premios;
using Maxx.Domain.Social;

namespace Maxx.Application.Posts;

public interface IPostRepository
{
    void Adicionar(Post post);

    // Inclusive o removido pela instituição: quem remove ou desvincula precisa dele.
    Task<Post?> BuscarAsync(Guid id, CancellationToken cancellationToken);

    // Os posts do prêmio, removidos ou não. No máximo um não removido, pelo índice único.
    Task<IReadOnlyList<Post>> DoPremioAsync(Guid medalhaId, CancellationToken cancellationToken);

    // Se o prêmio já rendeu um post que não foi removido.
    Task<bool> PremioTemPostAsync(Guid medalhaId, CancellationToken cancellationToken);

    // Os posts do prêmio de participação, removidos ou não.
    Task<IReadOnlyList<Post>> DoPremioParticipacaoAsync(Guid premioId, CancellationToken cancellationToken);

    // Os posts da conquista, removidos ou não (passo 9).
    Task<IReadOnlyList<Post>> DaConquistaAsync(Guid conquistaId, CancellationToken cancellationToken);

    // Se a conquista já rendeu um post que não foi removido.
    Task<bool> ConquistaTemPostAsync(Guid conquistaId, CancellationToken cancellationToken);

    void Apagar(Post post);
}

// O post como o app mostra: autor com curso e semestre, a foto, o prêmio com tier, pontos
// do momento e raridade atual. Só consulta, sem regra.
public interface IConsultaDePosts
{
    // Nulo para o que não existe e para o removido pela instituição. Com as curtidas e os
    // comentários contados, e se o leitor curtiu.
    Task<PostLido?> PostAsync(Guid id, Guid leitorId, CancellationToken cancellationToken);

    // Os que existem e não foram removidos, pelo id; os outros ficam de fora.
    Task<IReadOnlyDictionary<Guid, PostLido>> PostsAsync(
        IReadOnlyCollection<Guid> ids, Guid leitorId, CancellationToken cancellationToken);
}

public sealed record PostLido(
    Guid Id,
    AutorDoPost Autor,
    string? Legenda,
    string? ChaveDaFotoDoFeed,
    string? ChaveDaFotoDaGrade,
    PremioDoPost? Premio,
    DateTimeOffset CriadoEm,
    int Curtidas,
    int Comentarios,
    bool Curtiu);

// Curso e semestre do aluno são perfil (produto.md 3); o RA nunca sai daqui.
// A foto é a derivada da grade (~320²), que basta para o avatar. A posição é a do histórico
// geral, como no perfil (passo 11): vem no post do feed e do perfil; nas filas de
// comprovação e de contestação, nula.
public sealed record AutorDoPost(
    Guid Id, string NomeDeExibicao, string Arroba, string? Curso, int? Semestre, string? ChaveDaFoto, int? Posicao = null);

// Tier e pontos do momento da concessão (ADR 0007); raridade, a atual. Matéria e bimestre
// na medalha; matéria também na apresentação em sala. Estado e evento só no prêmio de
// participação (passo 8): reivindicado é a marca de pendente.
public sealed record PremioDoPost(
    TipoDeItem Tipo,
    Guid Id,
    string Item,
    string Nome,
    Tier Tier,
    int Pontos,
    RaridadeAtual? Raridade,
    string? Materia,
    int? Bimestre,
    EstadoDoPremioParticipacao? Estado = null,
    string? Evento = null);

// Publicação de post (produto.md 6.1). As regras são do domínio; aqui, buscar o que o
// pedido cita, conferir no bucket que a foto chegou e gravar. A corrida de dois posts com o
// mesmo prêmio é do índice único; a de dois posts com a mesma foto, do xmin do upload.
public sealed class PublicarPostHandler(
    IUsuarioRepository usuarios,
    IUploadPendenteRepository uploads,
    IMedalhaRepository medalhas,
    IConquistaRepository conquistas,
    IPostRepository posts,
    IArmazenamentoDeFotos armazenamento,
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
{
    public async Task<Post> PublicarAsync(
        Guid autorId, string? legenda, Guid? uploadId, Guid? medalhaId, CancellationToken cancellationToken) =>
        await PublicarAsync(autorId, legenda, uploadId, medalhaId, null, cancellationToken);

    // O prêmio do post é a medalha ou a conquista (passo 9), nunca os dois.
    public async Task<Post> PublicarAsync(
        Guid autorId, string? legenda, Guid? uploadId, Guid? medalhaId, Guid? conquistaId, CancellationToken cancellationToken)
    {
        var autor = await usuarios.BuscarAsync(autorId, cancellationToken)
            ?? throw new RecusaException(TipoDeRecusa.NaoAutorizado, CodigosDeRecusa.SessaoInvalida, $"Usuário {autorId} não existe.");

        UploadPendente? upload = null;
        if (uploadId is { } idDoUpload)
        {
            upload = await uploads.BuscarAsync(idDoUpload, cancellationToken)
                ?? throw Recusa(RecusaDaPublicacao.UploadNaoEncontrado, autorId);
        }

        Medalha? medalha = null;
        if (medalhaId is { } idDaMedalha)
        {
            medalha = await medalhas.BuscarAsync(idDaMedalha, cancellationToken)
                ?? throw Recusa(RecusaDaPublicacao.PremioNaoEncontrado, autorId);
        }

        Conquista? conquista = null;
        if (conquistaId is { } idDaConquista)
        {
            conquista = await conquistas.BuscarAsync(idDaConquista, cancellationToken)
                ?? throw Recusa(RecusaDaPublicacao.PremioNaoEncontrado, autorId);
        }

        var resultado = Post.Publicar(autor, legenda, upload, medalha, conquista, relogio.GetUtcNow());
        if (resultado.Post is not { } post)
        {
            throw Recusa(resultado.Recusa!.Value, autorId);
        }

        if ((medalha is not null && await posts.PremioTemPostAsync(medalha.Id, cancellationToken))
            || (conquista is not null && await posts.ConquistaTemPostAsync(conquista.Id, cancellationToken)))
        {
            throw PremioJaPostado((medalha?.Id ?? conquista?.Id)!.Value);
        }

        // O app diz que enviou; o bucket confirma. Sem as duas derivadas, o post teria uma
        // foto quebrada para sempre.
        foreach (var chave in post.ChavesDaFoto)
        {
            if (!await armazenamento.ExisteAsync(chave, cancellationToken))
            {
                throw new RecusaException(TipoDeRecusa.Conflito, CodigosDoPost.FotoNaoEnviada, $"Objeto {chave} não chegou ao bucket.");
            }
        }

        posts.Adicionar(post);
        try
        {
            await unitOfWork.SalvarAsync(cancellationToken);
        }
        catch (ConflitoDeUnicidadeException conflito) when (conflito.Unicidade == Unicidade.PostPorPremio)
        {
            throw PremioJaPostado((medalha?.Id ?? conquista?.Id)!.Value);
        }
        catch (ConflitoDeConcorrenciaException)
        {
            // Outro post confirmou o mesmo upload entre a leitura e a gravação.
            throw new RecusaException(TipoDeRecusa.Conflito, CodigosDoPost.UploadJaUsado, $"Upload {uploadId} usado ao mesmo tempo.");
        }

        return post;
    }

    private static RecusaException PremioJaPostado(Guid premioId) =>
        new(TipoDeRecusa.Conflito, CodigosDoPost.PremioJaPostado, $"Prêmio {premioId} já tem post.");

    internal static RecusaException Recusa(RecusaDaPublicacao recusa, Guid autorId) => recusa switch
    {
        RecusaDaPublicacao.AutorNaoPublica => new(TipoDeRecusa.Proibido, CodigosDoPost.PostNaoPermitido, $"{autorId} não publica."),
        RecusaDaPublicacao.LegendaLonga => new(TipoDeRecusa.Invalido, CodigosDoPost.LegendaLonga, "Legenda acima do limite."),
        RecusaDaPublicacao.Vazio => new(TipoDeRecusa.Invalido, CodigosDoPost.PostVazio, "Post sem prêmio, foto e legenda."),
        RecusaDaPublicacao.UploadNaoEncontrado => new(
            TipoDeRecusa.NaoEncontrado, CodigosDoPost.UploadNaoEncontrado, $"Upload inexistente ou alheio para {autorId}."),
        RecusaDaPublicacao.UploadJaUsado => new(TipoDeRecusa.Conflito, CodigosDoPost.UploadJaUsado, "Upload já confirmado."),
        RecusaDaPublicacao.PremioNaoEncontrado => new(
            TipoDeRecusa.NaoEncontrado, CodigosDoPost.PremioNaoEncontrado, $"Prêmio inexistente ou alheio para {autorId}."),
        RecusaDaPublicacao.PremioAnulado => new(TipoDeRecusa.Conflito, CodigosDoPost.PremioAnulado, "Prêmio anulado."),
        RecusaDaPublicacao.FotoObrigatoria => new(TipoDeRecusa.Invalido, CodigosDoPost.FotoObrigatoria, "Reivindicação sem foto."),
        RecusaDaPublicacao.ConquistaRemovida => new(TipoDeRecusa.Conflito, CodigosDoPost.ConquistaRemovida, "Conquista removida."),
        RecusaDaPublicacao.MaisDeUmPremio => new(TipoDeRecusa.Invalido, CodigosDoPost.MaisDeUmPremio, "Medalha e conquista no mesmo post."),
        _ => throw new ArgumentOutOfRangeException(nameof(recusa), recusa, null),
    };
}

// Remoção de post (produto.md 2.1 e 6.1). O autor apaga: o post sai do banco, com o que
// pendurar nele. A instituição remove: o post some para todos e fica quem removeu e quando.
// Nos dois casos a foto sai do bucket pela fila, porque a leitura é pública para quem tem
// o link (ADR 0002).
public sealed class RemoverPostHandler(
    IUsuarioRepository usuarios,
    IPostRepository posts,
    IPremioParticipacaoRepository premios,
    IFilaDeTrabalhos fila,
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
{
    public async Task RemoverAsync(Guid quemId, Guid postId, CancellationToken cancellationToken)
    {
        var quem = await usuarios.BuscarAsync(quemId, cancellationToken)
            ?? throw new RecusaException(TipoDeRecusa.NaoAutorizado, CodigosDeRecusa.SessaoInvalida, $"Usuário {quemId} não existe.");
        var post = await posts.BuscarAsync(postId, cancellationToken);
        if (post is null || post.EstaRemovido)
        {
            throw new RecusaException(TipoDeRecusa.NaoEncontrado, CodigosDoPost.PostNaoEncontrado, $"Post {postId} não existe.");
        }

        var modo = post.ModoDeRemocaoPor(quem);

        // O post de reivindicação é a comprovação (produto.md 4.7). Enquanto o prêmio ainda pode
        // ser decidido, o autor não o apaga: a foto sairia do bucket junto, e a aprovação e a
        // contestação dependem dela. A instituição remove, e resolve o prêmio no mesmo ato,
        // porque não pode depender da fila para tirar do ar uma foto imprópria.
        if (modo != ModoDeRemocao.NaoPermitida
            && post.PremioParticipacaoId is { } premioId
            && await premios.BuscarAsync(premioId, cancellationToken) is { PrendeOPost: true } premio)
        {
            if (modo == ModoDeRemocao.Apagar)
            {
                throw new RecusaException(
                    TipoDeRecusa.Conflito, CodigosDoPost.ComprovacaoEmAndamento, $"O post {postId} é a comprovação do prêmio {premioId}.");
            }

            await RemocaoPelaInstituicao.ResolverAsync(premio, quem, premios, posts, fila, relogio.GetUtcNow(), cancellationToken);
        }

        switch (modo)
        {
            case ModoDeRemocao.Apagar:
                posts.Apagar(post);
                break;
            case ModoDeRemocao.RemoverPelaInstituicao:
                post.RemoverPelaInstituicao(quem, relogio.GetUtcNow());
                break;
            default:
                throw new RecusaException(
                    TipoDeRecusa.Proibido, CodigosDoPost.RemocaoNaoPermitida, $"{quemId} não remove o post {postId}.");
        }

        if (post.TemFoto)
        {
            fila.Enfileirar(new RemoverFotos(post.ChavesDaFoto));
        }

        try
        {
            await unitOfWork.SalvarAsync(cancellationToken);
        }
        catch (ConflitoDeConcorrenciaException)
        {
            // Outra remoção do mesmo post gravou primeiro.
            throw new RecusaException(TipoDeRecusa.NaoEncontrado, CodigosDoPost.PostNaoEncontrado, $"Post {postId} removido ao mesmo tempo.");
        }
    }
}
