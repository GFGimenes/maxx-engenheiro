using Maxx.Application.Autenticacao;
using Maxx.Application.Autorizacao;
using Maxx.Application.Paginacao;
using Maxx.Application.Posts;
using Maxx.Domain.Identidade;
using Maxx.Domain.Social;

namespace Maxx.Application.Social;

public interface ICurtidaRepository
{
    Task<bool> ExisteAsync(Guid postId, Guid usuarioId, CancellationToken cancellationToken);

    void Adicionar(Curtida curtida);

    // Idempotente: sem curtida, nada acontece.
    Task RemoverAsync(Guid postId, Guid usuarioId, CancellationToken cancellationToken);
}

public interface IComentarioRepository
{
    void Adicionar(Comentario comentario);

    Task<Comentario?> BuscarAsync(Guid id, CancellationToken cancellationToken);

    void Apagar(Comentario comentario);
}

public interface ISeguimentoRepository
{
    Task<bool> ExisteAsync(Guid seguidorId, Guid seguidoId, CancellationToken cancellationToken);

    void Adicionar(Seguimento seguimento);

    // Idempotente: sem seguimento, nada acontece.
    Task RemoverAsync(Guid seguidorId, Guid seguidoId, CancellationToken cancellationToken);
}

// Só consulta, sem regra.
public interface IConsultaSocial
{
    // Curtidas do post e se o leitor está entre elas.
    Task<EstadoDaCurtida> CurtidasAsync(Guid postId, Guid leitorId, CancellationToken cancellationToken);

    // Os comentários não removidos, do mais antigo ao mais novo, como no Instagram.
    Task<Pagina<ComentarioLido>> ComentariosAsync(
        Guid postId, PosicaoNoTempo? depoisDe, int limite, CancellationToken cancellationToken);
}

public sealed record EstadoDaCurtida(int Curtidas, bool Curtiu);

public sealed record AutorDoComentario(Guid Id, string NomeDeExibicao, string Arroba);

public sealed record ComentarioLido(Guid Id, AutorDoComentario Autor, string Texto, DateTimeOffset CriadoEm);

// O que todos os handlers sociais fazem antes: quem pede existe, e o post existe e não foi
// removido.
internal static class Busca
{
    public static async Task<Usuario> UsuarioAsync(IUsuarioRepository usuarios, Guid id, CancellationToken cancellationToken) =>
        await usuarios.BuscarAsync(id, cancellationToken)
            ?? throw new RecusaException(TipoDeRecusa.NaoAutorizado, CodigosDeRecusa.SessaoInvalida, $"Usuário {id} não existe.");

    public static async Task<Post> PostVisivelAsync(IPostRepository posts, Guid id, CancellationToken cancellationToken)
    {
        var post = await posts.BuscarAsync(id, cancellationToken);
        return post is { EstaRemovido: false }
            ? post
            : throw new RecusaException(TipoDeRecusa.NaoEncontrado, CodigosDoPost.PostNaoEncontrado, $"Post {id} não existe.");
    }

    public static RecusaException NaoInterage(Guid usuarioId) =>
        new(TipoDeRecusa.Proibido, CodigosSociais.InteracaoNaoPermitida, $"{usuarioId} não curte nem comenta.");
}

// Curtir e descurtir (produto.md 6.1), os dois idempotentes: o app pode repetir o pedido
// sem medo, e a resposta é sempre o estado depois dele.
public sealed class CurtidaHandler(
    IUsuarioRepository usuarios,
    IPostRepository posts,
    ICurtidaRepository curtidas,
    IConsultaSocial consulta,
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
{
    public async Task<EstadoDaCurtida> CurtirAsync(Guid usuarioId, Guid postId, CancellationToken cancellationToken)
    {
        var usuario = await Busca.UsuarioAsync(usuarios, usuarioId, cancellationToken);
        var post = await Busca.PostVisivelAsync(posts, postId, cancellationToken);
        var curtida = Curtida.Curtir(usuario, post, relogio.GetUtcNow()) ?? throw Busca.NaoInterage(usuarioId);

        if (!await curtidas.ExisteAsync(postId, usuarioId, cancellationToken))
        {
            curtidas.Adicionar(curtida);
            try
            {
                await unitOfWork.SalvarAsync(cancellationToken);
            }
            catch (ConflitoDeUnicidadeException conflito) when (conflito.Unicidade == Unicidade.Curtida)
            {
                // O mesmo pedido, de novo, ao mesmo tempo: a primeira curtida vale.
                unitOfWork.Descartar();
            }
        }

        return await consulta.CurtidasAsync(postId, usuarioId, cancellationToken);
    }

    public async Task<EstadoDaCurtida> DescurtirAsync(Guid usuarioId, Guid postId, CancellationToken cancellationToken)
    {
        var usuario = await Busca.UsuarioAsync(usuarios, usuarioId, cancellationToken);
        await Busca.PostVisivelAsync(posts, postId, cancellationToken);
        if (!Domain.Social.Interacao.PodeInteragir(usuario))
        {
            throw Busca.NaoInterage(usuarioId);
        }

        await curtidas.RemoverAsync(postId, usuarioId, cancellationToken);
        return await consulta.CurtidasAsync(postId, usuarioId, cancellationToken);
    }
}

// Comentar e apagar comentário (produto.md 6.1).
public sealed class ComentarioHandler(
    IUsuarioRepository usuarios,
    IPostRepository posts,
    IComentarioRepository comentarios,
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
{
    public async Task<Comentario> ComentarAsync(Guid autorId, Guid postId, string? texto, CancellationToken cancellationToken)
    {
        var autor = await Busca.UsuarioAsync(usuarios, autorId, cancellationToken);
        var post = await Busca.PostVisivelAsync(posts, postId, cancellationToken);

        var resultado = Comentario.Escrever(autor, post, texto, relogio.GetUtcNow());
        if (resultado.Comentario is not { } comentario)
        {
            throw resultado.Recusa switch
            {
                RecusaDoComentario.NaoInterage => Busca.NaoInterage(autorId),
                RecusaDoComentario.Vazio => new RecusaException(TipoDeRecusa.Invalido, CodigosSociais.ComentarioVazio, "Comentário vazio."),
                RecusaDoComentario.Longo => new RecusaException(TipoDeRecusa.Invalido, CodigosSociais.ComentarioLongo, "Comentário longo."),
                _ => new RecusaException(TipoDeRecusa.NaoEncontrado, CodigosDoPost.PostNaoEncontrado, $"Post {postId} removido."),
            };
        }

        comentarios.Adicionar(comentario);
        await unitOfWork.SalvarAsync(cancellationToken);
        return comentario;
    }

    // O comentário tem de ser do post do caminho: o mesmo id por outro post é 404.
    public async Task RemoverAsync(Guid quemId, Guid postId, Guid comentarioId, CancellationToken cancellationToken)
    {
        var quem = await Busca.UsuarioAsync(usuarios, quemId, cancellationToken);
        await Busca.PostVisivelAsync(posts, postId, cancellationToken);
        var comentario = await comentarios.BuscarAsync(comentarioId, cancellationToken);
        if (comentario is null || comentario.PostId != postId || comentario.EstaRemovido)
        {
            throw new RecusaException(
                TipoDeRecusa.NaoEncontrado, CodigosSociais.ComentarioNaoEncontrado, $"Comentário {comentarioId} não existe no post {postId}.");
        }

        switch (comentario.ModoDeRemocaoPor(quem))
        {
            case ModoDeRemocao.Apagar:
                comentarios.Apagar(comentario);
                break;
            case ModoDeRemocao.RemoverPelaInstituicao:
                comentario.RemoverPelaInstituicao(quem, relogio.GetUtcNow());
                break;
            default:
                throw new RecusaException(
                    TipoDeRecusa.Proibido,
                    CodigosSociais.RemocaoDeComentarioNaoPermitida,
                    $"{quemId} não remove o comentário {comentarioId}.");
        }

        try
        {
            await unitOfWork.SalvarAsync(cancellationToken);
        }
        catch (ConflitoDeConcorrenciaException)
        {
            // Outra remoção do mesmo comentário gravou primeiro.
            throw new RecusaException(
                TipoDeRecusa.NaoEncontrado, CodigosSociais.ComentarioNaoEncontrado, $"Comentário {comentarioId} removido ao mesmo tempo.");
        }
    }
}

// Seguir e deixar de seguir (produto.md 6.3), os dois idempotentes. O vínculo do professor
// com o aluno é perguntado ao banco, nunca lido do token.
public sealed class SeguimentoHandler(
    IUsuarioRepository usuarios,
    ISeguimentoRepository seguimentos,
    IEscopoDeTurma escopo,
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
{
    public async Task SeguirAsync(Guid seguidorId, Guid seguidoId, CancellationToken cancellationToken)
    {
        var seguidor = await Busca.UsuarioAsync(usuarios, seguidorId, cancellationToken);
        var seguido = await usuarios.BuscarAsync(seguidoId, cancellationToken)
            ?? throw new RecusaException(TipoDeRecusa.NaoEncontrado, CodigosSociais.UsuarioNaoEncontrado, $"Usuário {seguidoId} não existe.");

        var professorDoSeguido = seguidor.Papeis.HasFlag(Domain.Academico.Papeis.Professor)
            && await escopo.ProfessorEnsinaAlunoAsync(seguidorId, seguidoId, cancellationToken);
        var seguimento = Seguimento.Seguir(seguidor, seguido, professorDoSeguido, relogio.GetUtcNow())
            ?? throw new RecusaException(TipoDeRecusa.Proibido, CodigosSociais.SeguirNaoPermitido, $"{seguidorId} não segue {seguidoId}.");

        if (await seguimentos.ExisteAsync(seguidorId, seguidoId, cancellationToken))
        {
            return;
        }

        seguimentos.Adicionar(seguimento);
        try
        {
            await unitOfWork.SalvarAsync(cancellationToken);
        }
        catch (ConflitoDeUnicidadeException conflito) when (conflito.Unicidade == Unicidade.Seguimento)
        {
            unitOfWork.Descartar();
        }
    }

    // Deixar de seguir vale sempre, mesmo para o professor que já não dá aula ao aluno.
    public async Task DeixarDeSeguirAsync(Guid seguidorId, Guid seguidoId, CancellationToken cancellationToken)
    {
        await Busca.UsuarioAsync(usuarios, seguidorId, cancellationToken);
        await seguimentos.RemoverAsync(seguidorId, seguidoId, cancellationToken);
    }
}
