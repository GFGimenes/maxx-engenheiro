using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Maxx.Api.IntegrationTests;

public class DatabaseMigratorTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrador_espera_o_lock_de_outro_migrador()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var factory = new MaxxApiFactory(connectionString);

        // Outro migrador segurando o lock.
        await using var holder = new NpgsqlConnection(connectionString);
        await holder.OpenAsync(Ct);
        await ExecuteLockAsync(holder, "pg_advisory_lock");

        var migration = factory.MigrateAsync(Ct);
        var finished = await Task.WhenAny(migration, Task.Delay(TimeSpan.FromSeconds(2), Ct));
        Assert.NotSame(migration, finished);
        Assert.NotEmpty(await PendingMigrationsAsync(factory));

        await ExecuteLockAsync(holder, "pg_advisory_unlock");
        await migration.WaitAsync(TimeSpan.FromSeconds(30), Ct);

        Assert.Empty(await PendingMigrationsAsync(factory));
    }

    [Fact]
    public async Task Dois_migradores_em_paralelo_aplicam_cada_migration_uma_vez()
    {
        await using var factory = new MaxxApiFactory(await postgres.CreateDatabaseAsync());

        await Task.WhenAll(factory.MigrateAsync(Ct), factory.MigrateAsync(Ct));

        Assert.Equal(await KnownMigrationsCountAsync(factory), await AppliedMigrationsCountAsync(factory));
    }

    [Fact]
    public async Task Rodar_de_novo_nao_aplica_nada()
    {
        await using var factory = new MaxxApiFactory(await postgres.CreateDatabaseAsync());
        await factory.MigrateAsync(Ct);

        await factory.MigrateAsync(Ct);

        Assert.Equal(await KnownMigrationsCountAsync(factory), await AppliedMigrationsCountAsync(factory));
        Assert.Empty(await PendingMigrationsAsync(factory));
    }

    private static async Task ExecuteLockAsync(NpgsqlConnection connection, string function)
    {
        await using var command = new NpgsqlCommand($"SELECT {function}(@key)", connection);
        command.Parameters.AddWithValue("key", DatabaseMigrator.AdvisoryLockKey);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<List<string>> PendingMigrationsAsync(MaxxApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        return [.. await db.Database.GetPendingMigrationsAsync(Ct)];
    }

    private static async Task<int> KnownMigrationsCountAsync(MaxxApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Database.GetMigrations().Count();
    }

    // Lê a tabela de histórico direto: conta linhas, não o que o EF acha que aplicou.
    private static async Task<long> AppliedMigrationsCountAsync(MaxxApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
        await using var command = dataSource.CreateCommand(
            $"SELECT count(*) FROM {MaxxDbContext.MigrationsHistoryTable}");
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }
}
