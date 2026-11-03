using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Maxx.Api.IntegrationTests;

// diretorioDeDados: data/ do repositório, salvo quando o teste altera uma cópia.
public sealed class MaxxApiFactory(string connectionString, string environment = "Development", string? diretorioDeDados = null)
    : WebApplicationFactory<Program>
{
    // Porta 1 recusa conexão: banco fora do ar sem depender de contêiner.
    public const string UnreachableDatabase =
        "Host=127.0.0.1;Port=1;Database=maxx;Username=maxx;Password=maxx;Timeout=2";

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Maxx", connectionString)
            .UseSetting($"{SeedOptions.Section}:{nameof(SeedOptions.DiretorioDeDados)}", diretorioDeDados ?? Repositorio.DiretorioDeDados)
            .UseEnvironment(environment);

    // O mesmo caminho do subcomando migrate, num escopo próprio.
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(cancellationToken);
    }

    // O mesmo caminho do subcomando seed, num escopo próprio.
    public async Task<ResultadoDoSeed> SeedAsync(PerfilDeSeed perfil, CancellationToken cancellationToken)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync(perfil, cancellationToken);
    }
}
