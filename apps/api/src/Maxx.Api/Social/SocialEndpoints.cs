using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Maxx.Api.Posts;
using Maxx.Application;
using Maxx.Application.Autenticacao;
using Maxx.Application.Fotos;
using Maxx.Application.Paginacao;
using Maxx.Application.Posts;
using Maxx.Application.Social;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maxx.Api.Social;

// Curtida, comentário e seguir (produto.md 6.1 e 6.3). Curtir, descurtir, seguir e deixar
// de seguir são idempotentes: o app repete o pedido sem medo.
internal static class SocialEndpoints
{
    // Página de comentários: cabe numa tela e meia de celular.
    public const int ComentariosPorPagina = 20;

    public static IEndpointRouteBuilder MapSocialEndpoints(this IEndpointRouteBuilder v1)
    {
        var post = v1.MapGroup("/posts/{postId:guid}").WithTags("Posts");

        post.MapPut("/curtida", CurtirAsync)
            .WithName("CurtirPost")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        post.MapDelete("/curtida", DescurtirAsync)
            .WithName("DescurtirPost")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        post.MapGet("/comentarios", ComentariosAsync)
            .WithName("GetComentarios")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        post.MapPost("/comentarios", ComentarAsync)
            .WithName("Comentar")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        post.MapDelete("/comentarios/{comentarioId:guid}", RemoverComentarioAsync)
            .WithName("RemoverComentario")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var seguindo = v1.MapGroup("/seguindo").WithTags("Seguir");

        seguindo.MapPut("/{usuarioId:guid}", SeguirAsync)
            .WithName("Seguir")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        seguindo.MapDelete("/{usuarioId:guid}", DeixarDeSeguirAsync)
            .WithName("DeixarDeSeguir");

        return v1;
    }

    private static async Task<Ok<CurtidasResponse>> CurtirAsync(
        Guid postId, ClaimsPrincipal principal, CurtidaHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(CurtidasResponse.De(await handler.CurtirAsync(PostEndpoints.UsuarioId(principal), postId, cancellationToken)));

    private static async Task<Ok<CurtidasResponse>> DescurtirAsync(
        Guid postId, ClaimsPrincipal principal, CurtidaHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(CurtidasResponse.De(await handler.DescurtirAsync(PostEndpoints.UsuarioId(principal), postId, cancellationToken)));

    // O post removido não mostra comentário: 404, como o próprio post.
    private static async Task<Ok<PaginaDeComentariosResponse>> ComentariosAsync(
        Guid postId,
        string? cursor,
        ClaimsPrincipal principal,
        IConsultaDePosts consultaDePosts,
        IConsultaSocial consulta,
        IArmazenamentoDeFotos armazenamento,
        CancellationToken cancellationToken)
    {
        var posicao = CursorDeTempo.Ler(cursor);
        _ = await consultaDePosts.PostAsync(postId, PostEndpoints.UsuarioId(principal), cancellationToken)
            ?? throw new RecusaException(
                TipoDeRecusa.NaoEncontrado, CodigosDoPost.PostNaoEncontrado, $"Post {postId} não existe.");

        var pagina = await consulta.ComentariosAsync(postId, posicao, ComentariosPorPagina, cancellationToken);
        return TypedResults.Ok(new PaginaDeComentariosResponse([.. pagina.Itens.Select(comentario => ComentarioResponse.De(comentario, armazenamento))], pagina.Proximo));
    }

    private static async Task<Created<ComentarioResponse>> ComentarAsync(
        Guid postId,
        ComentarioRequest pedido,
        ClaimsPrincipal principal,
        ComentarioHandler handler,
        IUsuarioRepository usuarios,
        IArmazenamentoDeFotos armazenamento,
        CancellationToken cancellationToken)
    {
        var autorId = PostEndpoints.UsuarioId(principal);
        var comentario = await handler.ComentarAsync(autorId, postId, pedido.Texto, cancellationToken);
        var autor = (await usuarios.BuscarAsync(autorId, cancellationToken))!;
        return TypedResults.Created(
            (string?)null,
            new ComentarioResponse(
                comentario.Id,
                new AutorDoComentarioResponse(
                    autor.Id, autor.NomeDeExibicao, autor.Arroba.ToString(), PostResponse.Leitura(armazenamento, autor.ChaveDaFotoDaGrade)),
                comentario.Texto,
                comentario.CriadoEm));
    }

    private static async Task<NoContent> RemoverComentarioAsync(
        Guid postId, Guid comentarioId, ClaimsPrincipal principal, ComentarioHandler handler, CancellationToken cancellationToken)
    {
        await handler.RemoverAsync(PostEndpoints.UsuarioId(principal), postId, comentarioId, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> SeguirAsync(
        Guid usuarioId, ClaimsPrincipal principal, SeguimentoHandler handler, CancellationToken cancellationToken)
    {
        await handler.SeguirAsync(PostEndpoints.UsuarioId(principal), usuarioId, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> DeixarDeSeguirAsync(
        Guid usuarioId, ClaimsPrincipal principal, SeguimentoHandler handler, CancellationToken cancellationToken)
    {
        await handler.DeixarDeSeguirAsync(PostEndpoints.UsuarioId(principal), usuarioId, cancellationToken);
        return TypedResults.NoContent();
    }
}

// O estado depois do pedido, para o app acertar o coração e o número.
public sealed record CurtidasResponse(int Curtidas, bool Curtiu)
{
    public static CurtidasResponse De(EstadoDaCurtida estado) => new(estado.Curtidas, estado.Curtiu);
}

// Vazio e o limite de 500 caracteres são do domínio, contando emoji como um, com códigos
// próprios; aqui, só um teto bruto.
public sealed record ComentarioRequest([property: StringLength(2000)] string? Texto);

public sealed record AutorDoComentarioResponse(Guid Id, string NomeDeExibicao, string Arroba, string? Foto);

public sealed record ComentarioResponse(Guid Id, AutorDoComentarioResponse Autor, string Texto, DateTimeOffset CriadoEm)
{
    public static ComentarioResponse De(ComentarioLido comentario, IArmazenamentoDeFotos armazenamento) => new(
        comentario.Id,
        new AutorDoComentarioResponse(
            comentario.Autor.Id, comentario.Autor.NomeDeExibicao, comentario.Autor.Arroba, PostResponse.Leitura(armazenamento, comentario.Autor.ChaveDaFoto)),
        comentario.Texto,
        comentario.CriadoEm);
}

// proximo vem nulo na última página.
public sealed record PaginaDeComentariosResponse(IReadOnlyList<ComentarioResponse> Itens, string? Proximo);
