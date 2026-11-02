using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Maxx.Infrastructure.Health;

internal sealed class PostgresReadinessCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Exceção vira Unhealthy no HealthCheckService.
        await using var command = dataSource.CreateCommand("SELECT 1");
        await command.ExecuteScalarAsync(cancellationToken);
        return HealthCheckResult.Healthy();
    }
}
