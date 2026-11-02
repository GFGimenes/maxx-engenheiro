using Maxx.Infrastructure.Health;
using Microsoft.Extensions.DependencyInjection;

namespace Maxx.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddNpgsqlDataSource(connectionString);

        // Toda verificação registrada aqui entra no /ready.
        services.AddHealthChecks()
            .AddCheck<PostgresReadinessCheck>("postgres", timeout: TimeSpan.FromSeconds(3));

        return services;
    }
}
