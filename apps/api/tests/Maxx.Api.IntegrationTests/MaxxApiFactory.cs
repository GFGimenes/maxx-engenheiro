using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Maxx.Api.IntegrationTests;

// diretorioDeDados: data/ do repositório, salvo quando o teste altera uma cópia.
// configuracao: chaves a mais, no formato do IConfiguration ("Jwt:Chave").
public sealed class MaxxApiFactory(
    string connectionString,
    string environment = "Development",
    string? diretorioDeDados = null,
    IReadOnlyDictionary<string, string>? configuracao = null)
    : WebApplicationFactory<Program>
{
    // Porta 1 recusa conexão: banco fora do ar sem depender de contêiner.
    public const string UnreachableDatabase =
        "Host=127.0.0.1;Port=1;Database=maxx;Username=maxx;Password=maxx;Timeout=2";

    // Fora de desenvolvimento a API não sobe sem chave de assinatura. Gerada por execução.
    public static IReadOnlyDictionary<string, string> ChaveDeTeste { get; } = new Dictionary<string, string>
    {
        ["Jwt:Chave"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)),
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Maxx", connectionString)
            .UseSetting($"{SeedOptions.Section}:{nameof(SeedOptions.DiretorioDeDados)}", diretorioDeDados ?? Repositorio.DiretorioDeDados)
            .UseEnvironment(environment);

        foreach (var (chave, valor) in configuracao ?? new Dictionary<string, string>())
        {
            builder.UseSetting(chave, valor);
        }
    }

    // O mesmo caminho do subcomando migrate, num escopo próprio.
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(cancellationToken);
    }

    // O mesmo caminho do subcomando seed, num escopo próprio.
    public async Task<ResultadoDoSeedBase> SeedBaseAsync(CancellationToken cancellationToken)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedBaseAsync(cancellationToken);
    }

    public async Task<ResultadoDoSeedDemo> SeedDemoAsync(CancellationToken cancellationToken)
    {
        await using var scope = Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedDemoAsync(cancellationToken);
    }
}
