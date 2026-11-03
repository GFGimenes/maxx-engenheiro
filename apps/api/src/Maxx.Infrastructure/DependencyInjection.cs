using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Maxx.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddNpgsqlDataSource(connectionString);

        services.AddDbContext<MaxxDbContext>((provider, options) =>
            MaxxDbContext.Configure(options, builder => builder.UseNpgsql(
                provider.GetRequiredService<NpgsqlDataSource>(),
                npgsql => npgsql.MigrationsHistoryTable(MaxxDbContext.MigrationsHistoryTable))));

        services.AddScoped<DatabaseMigrator>();
        services.AddScoped<DatabaseSeeder>();

        // Toda verificação registrada aqui entra no /ready.
        services.AddHealthChecks()
            .AddCheck<DatabaseReadinessCheck>("database", timeout: TimeSpan.FromSeconds(3));

        return services;
    }
}
