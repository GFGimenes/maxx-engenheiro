using System.Security.Claims;
using Maxx.Api.Posts;
using Maxx.Application.Feed;
using Maxx.Application.Fotos;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maxx.Api.Feed;

// O feed (produto.md 6.2): sem cursor, a primeira página de uma sessão nova; com o cursor da
// resposta anterior, a seguinte, na mesma ordem de recomendações.
internal static class FeedEndpoints
{
    public static IEndpointRouteBuilder MapFeedEndpoints(this IEndpointRouteBuilder v1)
    {
        v1.MapGet("/feed", PaginaAsync)
            .WithName("GetFeed")
            .WithTags("Feed")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return v1;
    }

    private static async Task<Ok<PaginaDoFeedResponse>> PaginaAsync(
        string? cursor,
        ClaimsPrincipal principal,
        FeedHandler handler,
        IArmazenamentoDeFotos armazenamento,
        CancellationToken cancellationToken)
    {
        var pagina = await handler.PaginaAsync(PostEndpoints.UsuarioId(principal), cursor, cancellationToken);
        return TypedResults.Ok(new PaginaDoFeedResponse([.. pagina.Itens.Select(post => PostResponse.De(post, armazenamento))], pagina.Proximo));
    }
}

// proximo vem nulo quando os dois trechos, seguidos e recomendações, acabaram.
public sealed record PaginaDoFeedResponse(IReadOnlyList<PostResponse> Itens, string? Proximo);
