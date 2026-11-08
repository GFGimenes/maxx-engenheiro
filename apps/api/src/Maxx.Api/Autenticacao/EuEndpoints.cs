using System.Security.Claims;
using Maxx.Application.Autenticacao;
using Maxx.Application.Fotos;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maxx.Api.Autenticacao;

internal static class EuEndpoints
{
    // Quem é o dono do access token, lido do banco: o token só diz quem é, e o arroba pode
    // ter mudado depois da emissão. Cresce só por acréscimo.
    public static IEndpointRouteBuilder MapEuEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/eu", GetEuAsync)
            .WithName("GetEu")
            .WithTags("Autenticacao")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return routes;
    }

    private static async Task<Results<Ok<EuResponse>, UnauthorizedHttpResult>> GetEuAsync(
        ClaimsPrincipal principal, IUsuarioRepository usuarios, IArmazenamentoDeFotos armazenamento, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(MaxxClaims.Sub), out var id)
            || await usuarios.BuscarAsync(id, cancellationToken) is not { } usuario)
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(new EuResponse(
            usuario.Id,
            JwtAccessTokenIssuer.NomesDosPapeis(usuario.Papeis),
            usuario.Arroba.ToString(),
            usuario.NomeDeExibicao,
            Posts.PostResponse.Leitura(armazenamento, usuario.ChaveDaFotoDaGrade)));
    }
}

// NomeDeExibicao entrou no passo 6, para as iniciais do avatar na navegação: só acréscimo.
// Foto entrou no passo 7, a derivada da grade para o avatar: também só acréscimo.
public sealed record EuResponse(Guid Id, IReadOnlyList<string> Papeis, string Arroba, string NomeDeExibicao, string? Foto);
