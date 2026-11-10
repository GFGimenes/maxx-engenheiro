using Maxx.Application.Fila;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Maxx.Api.Health;

internal static class HealthEndpoints
{
    // Infraestrutura, não contrato: fica fora de /v1.
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/health", GetHealth)
            .WithName("GetHealth")
            .WithTags("Health");

        routes.MapGet("/ready", GetReadyAsync)
            .WithName("GetReady")
            .WithTags("Health")
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        // Para o monitor de fora (docs/operacao.md): 503 com o mesmo corpo quando há problema.
        // Só idades, contagens e códigos: nada de carga de trabalho, erro ou dado de alguém.
        routes.MapGet("/health/worker", GetWorkerAsync)
            .WithName("GetSaudeDoWorker")
            .WithTags("Health")
            .Produces<SaudeDoWorkerResponse>(StatusCodes.Status503ServiceUnavailable);

        return routes;
    }

    // Liveness: o processo responde. Não toca no banco.
    private static Ok<HealthResponse> GetHealth() => TypedResults.Ok(HealthResponse.Ok);

    private static async Task<Results<Ok<HealthResponse>, ProblemHttpResult>> GetReadyAsync(
        HealthCheckService healthChecks,
        CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(cancellationToken);

        if (report.Status != HealthStatus.Healthy)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Serviço indisponível.");
        }

        return TypedResults.Ok(HealthResponse.Ok);
    }

    private static async Task<Results<Ok<SaudeDoWorkerResponse>, JsonHttpResult<SaudeDoWorkerResponse>>> GetWorkerAsync(
        IConsultaDaSaudeDoWorker consulta,
        CancellationToken cancellationToken)
    {
        var saude = await consulta.ConsultarAsync(cancellationToken);
        var resposta = new SaudeDoWorkerResponse(
            saude.Saudavel ? "ok" : "problema",
            Segundos(saude.DesdeOBatimento),
            Segundos(saude.EsperaDoMaisAntigo),
            saude.Disponiveis,
            saude.FalhosNaJanela,
            saude.Problemas);

        return saude.Saudavel
            ? TypedResults.Ok(resposta)
            : TypedResults.Json(resposta, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    private static int? Segundos(TimeSpan? duracao) => duracao is { } valor ? (int)Math.Floor(valor.TotalSeconds) : null;
}

public sealed record HealthResponse(string Status)
{
    public static HealthResponse Ok { get; } = new("ok");
}

// Segundos inteiros; nulos quando não há batimento ou trabalho disponível.
public sealed record SaudeDoWorkerResponse(
    string Status,
    int? SegundosDesdeOBatimento,
    int? SegundosDeEsperaNaFila,
    int Disponiveis,
    int FalhosEm24h,
    IReadOnlyList<string> Problemas);
