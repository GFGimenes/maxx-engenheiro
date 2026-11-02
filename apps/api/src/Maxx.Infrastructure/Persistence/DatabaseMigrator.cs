using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Maxx.Infrastructure.Persistence;

// Roda no contêiner de migração (`Maxx.Api migrate`), nunca no startup da aplicação.
public sealed partial class DatabaseMigrator(MaxxDbContext db, ILogger<DatabaseMigrator> logger)
{
    // Chave fixa do advisory lock de migração. Qualquer valor serve, desde que seja
    // sempre o mesmo e nenhum outro lock do Maxx o use.
    public const long AdvisoryLockKey = 0x4D41_5858_0001;

    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        // O lock é da sessão: a mesma conexão precisa segurá-lo, migrar e soltá-lo.
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (NpgsqlConnection)db.Database.GetDbConnection();

            LogWaitingForLock(logger);
            await ExecuteAsync(connection, "SELECT pg_advisory_lock(@key)", cancellationToken);
            try
            {
                // Sem a tabela de histórico, o EF consulta uma tabela inexistente e registra erro.
                await db.GetService<IHistoryRepository>().CreateIfNotExistsAsync(cancellationToken);

                var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                LogPending(logger, pending.Count);
                await db.Database.MigrateAsync(cancellationToken);
                LogDone(logger);
            }
            finally
            {
                await ExecuteAsync(connection, "SELECT pg_advisory_unlock(@key)", CancellationToken.None);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("key", AdvisoryLockKey);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Aguardando o lock de migração.")]
    private static partial void LogWaitingForLock(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Lock obtido; {Count} migration(s) pendente(s).")]
    private static partial void LogPending(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Migração concluída.")]
    private static partial void LogDone(ILogger logger);
}
