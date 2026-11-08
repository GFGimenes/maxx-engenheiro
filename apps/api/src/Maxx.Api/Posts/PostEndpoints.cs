using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Maxx.Api.Autenticacao;
using Maxx.Api.Medalhas;
using Maxx.Api.Premios;
using Maxx.Application;
using Maxx.Application.Fotos;
using Maxx.Application.Posts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maxx.Api.Posts;

// O post (produto.md 6.1): publicar, ler e remover. A leitura é de qualquer usuário logado;
// é ela que o deep link /post/:id abre.
internal static class PostEndpoints
{
    public static IEndpointRouteBuilder MapPostEndpoints(this IEndpointRouteBuilder v1)
    {
        var posts = v1.MapGroup("/posts").WithTags("Posts");

        posts.MapPost("/", PublicarAsync)
            .WithName("PublicarPost")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        posts.MapGet("/{postId:guid}", LerAsync)
            .WithName("GetPost")
            .ProducesProblem(StatusCodes.Status404NotFound);

        posts.MapDelete("/{postId:guid}", RemoverAsync)
            .WithName("RemoverPost")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return v1;
    }

    private static async Task<Created<PostResponse>> PublicarAsync(
        PublicarPostRequest pedido,
        ClaimsPrincipal principal,
        PublicarPostHandler handler,
        IConsultaDePosts consulta,
        IArmazenamentoDeFotos armazenamento,
        CancellationToken cancellationToken)
    {
        var post = await handler.PublicarAsync(UsuarioId(principal), pedido.Legenda, pedido.UploadId, pedido.MedalhaId, cancellationToken);
        var lido = await consulta.PostAsync(post.Id, UsuarioId(principal), cancellationToken)
            ?? throw new InvalidOperationException($"O post {post.Id} acabou de ser gravado e não foi lido.");
        return TypedResults.Created($"/api/v1/posts/{post.Id}", PostResponse.De(lido, armazenamento));
    }

    private static async Task<Ok<PostResponse>> LerAsync(
        Guid postId,
        ClaimsPrincipal principal,
        IConsultaDePosts consulta,
        IArmazenamentoDeFotos armazenamento,
        CancellationToken cancellationToken)
    {
        var lido = await consulta.PostAsync(postId, UsuarioId(principal), cancellationToken)
            ?? throw new RecusaException(TipoDeRecusa.NaoEncontrado, CodigosDoPost.PostNaoEncontrado, $"Post {postId} não existe.");
        return TypedResults.Ok(PostResponse.De(lido, armazenamento));
    }

    private static async Task<NoContent> RemoverAsync(
        Guid postId, ClaimsPrincipal principal, RemoverPostHandler handler, CancellationToken cancellationToken)
    {
        await handler.RemoverAsync(UsuarioId(principal), postId, cancellationToken);
        return TypedResults.NoContent();
    }

    // O sub do token; o grupo /v1 já exigiu um usuário autenticado.
    internal static Guid UsuarioId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(MaxxClaims.Sub), out var id)
            ? id
            : throw new InvalidOperationException("Token autenticado sem sub.");
}

// Ao menos um dos três; quem decide é o domínio. O limite da legenda é em caracteres,
// contando um emoji como um, e também do domínio: aqui só um teto bruto para o corpo.
public sealed record PublicarPostRequest([property: StringLength(4000)] string? Legenda, Guid? UploadId, Guid? MedalhaId);

public sealed record PostResponse(
    Guid Id,
    AutorDoPostResponse Autor,
    string? Legenda,
    FotoDoPostResponse? Foto,
    PremioDoPostResponse? Premio,
    DateTimeOffset CriadoEm,
    int Curtidas,
    int Comentarios,
    bool Curtiu)
{
    public static PostResponse De(PostLido post, IArmazenamentoDeFotos armazenamento) => new(
        post.Id,
        new AutorDoPostResponse(post.Autor.Id, post.Autor.NomeDeExibicao, post.Autor.Arroba, post.Autor.Curso, post.Autor.Semestre),
        post.Legenda,
        post is { ChaveDaFotoDoFeed: { } feed, ChaveDaFotoDaGrade: { } grade }
            ? new FotoDoPostResponse(armazenamento.UrlDeLeitura(feed).AbsoluteUri, armazenamento.UrlDeLeitura(grade).AbsoluteUri)
            : null,
        post.Premio is { } premio
            ? new PremioDoPostResponse(
                ProfessorEndpoints.Nome(premio.Tipo),
                premio.Id,
                premio.Item,
                premio.Nome,
                ProfessorEndpoints.Nome(premio.Tier),
                premio.Pontos,
                premio.Raridade is { } raridade ? new RaridadeResponse(ProfessorEndpoints.Nome(raridade.Nivel), raridade.Percentual) : null,
                premio.Materia,
                premio.Bimestre)
            : null,
        post.CriadoEm,
        post.Curtidas,
        post.Comentarios,
        post.Curtiu);
}

// Curso pela sigla e semestre do aluno (1º ao 8º), como na linha do autor no feed.
public sealed record AutorDoPostResponse(Guid Id, string NomeDeExibicao, string Arroba, string? Curso, int? Semestre);

// A derivada do feed (~1080²) e a da grade (~320²), lidas direto da CDN.
public sealed record FotoDoPostResponse(string Feed, string Grade);

// Tipo, tier e raridade em camelCase, como no catálogo ("medalha", "bronze", "raro").
// Matéria e bimestre só para medalha.
public sealed record PremioDoPostResponse(
    string Tipo,
    Guid Id,
    string Item,
    string Nome,
    string Tier,
    int Pontos,
    RaridadeResponse? Raridade,
    string? Materia,
    int? Bimestre);
