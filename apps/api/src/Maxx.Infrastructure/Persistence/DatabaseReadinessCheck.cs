using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Maxx.Infrastructure.Persistence;

internal sealed class DatabaseReadinessCheck(MaxxDbContext db, NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Exceção de conexão vira Unhealthy no HealthCheckService.
        // Pergunta ao Postgres antes de ler o histórico: ler uma tabela inexistente faz o
        // EF registrar erro a cada sondagem, até a primeira migração.
        if (!await HistoryTableExistsAsync(cancellationToken))
        {
            return HealthCheckResult.Unhealthy("Banco ainda não migrado.");
        }

        var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);

        return pending.Any()
            ? HealthCheckResult.Unhealthy("Há migration pendente.")
            : HealthCheckResult.Healthy();
    }

    private async Task<bool> HistoryTableExistsAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("SELECT to_regclass(@table) IS NOT NULL");
        command.Parameters.AddWithValue("table", MaxxDbContext.MigrationsHistoryTable);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
