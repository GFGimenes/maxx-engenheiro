using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maxx.Api.Autenticacao;

internal static class EuEndpoints
{
    // Quem é o dono do access token. Nasce mínimo na E5 para exercitar a autenticação;
    // cresce só por acréscimo.
    public static IEndpointRouteBuilder MapEuEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/eu", GetEu)
            .WithName("GetEu")
            .WithTags("Autenticacao")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return routes;
    }

    private static Ok<EuResponse> GetEu(ClaimsPrincipal usuario) =>
        TypedResults.Ok(new EuResponse(
            Guid.Parse(usuario.FindFirstValue(MaxxClaims.Sub)!),
            [.. usuario.FindAll(MaxxClaims.Papel).Select(claim => claim.Value)]));
}

public sealed record EuResponse(Guid Id, IReadOnlyList<string> Papeis);
