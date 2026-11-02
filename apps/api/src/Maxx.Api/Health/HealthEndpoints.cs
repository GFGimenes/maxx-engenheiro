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
}

public sealed record HealthResponse(string Status)
{
    public static HealthResponse Ok { get; } = new("ok");
}
