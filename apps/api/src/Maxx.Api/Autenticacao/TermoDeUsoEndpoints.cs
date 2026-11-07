using Maxx.Application.Autenticacao.Termos;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maxx.Api.Autenticacao;

// O texto vem da API, e não do bundle: o app instalado atrasa, e o aceite tem de ser do
// texto vigente. Público, porque quem o lê ainda não tem conta.
internal static class TermoDeUsoEndpoints
{
    public static IEndpointRouteBuilder MapTermoDeUsoEndpoints(this IEndpointRouteBuilder v1)
    {
        v1.MapGet("/termo-de-uso", VigenteAsync)
            .WithName("GetTermoDeUso")
            .WithTags("Autenticacao")
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return v1;
    }

    private static async Task<Ok<TermoDeUsoResponse>> VigenteAsync(TermoDeUsoHandler handler, CancellationToken cancellationToken)
    {
        var termo = await handler.VigenteAsync(cancellationToken);
        return TypedResults.Ok(new TermoDeUsoResponse(termo.Versao, termo.Conteudo));
    }
}

public sealed record TermoDeUsoResponse(int Versao, string Texto);
