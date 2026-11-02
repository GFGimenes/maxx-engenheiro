using Maxx.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Maxx.Api.IntegrationTests;

public sealed class MaxxApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    // Porta 1 recusa conexão: banco fora do ar sem depender de contêiner.
    public const string UnreachableDatabase =
        "Host=127.0.0.1;Port=1;Database=maxx;Username=maxx;Password=maxx;Timeout=2";

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Maxx", connectionString);

    // O mesmo caminho do subcomando migrate, num escopo próprio.
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(cancellationToken);
    }
}
