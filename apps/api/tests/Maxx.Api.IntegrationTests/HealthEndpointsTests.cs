using System.Net;
using System.Net.Http.Json;
using Maxx.Api.Health;

namespace Maxx.Api.IntegrationTests;

public class HealthEndpointsTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Health_responde_200_sem_tocar_no_banco()
    {
        await using var factory = new MaxxApiFactory(MaxxApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/health", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>(Ct);
        Assert.Equal("ok", body?.Status);
    }

    [Fact]
    public async Task Ready_responde_200_com_o_banco_migrado()
    {
        await using var factory = new MaxxApiFactory(await postgres.CreateDatabaseAsync());
        await factory.MigrateAsync(Ct);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/ready", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_responde_503_com_migration_pendente()
    {
        await using var factory = new MaxxApiFactory(await postgres.CreateDatabaseAsync());
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/ready", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Ready_responde_503_em_ProblemDetails_com_o_banco_fora()
    {
        await using var factory = new MaxxApiFactory(MaxxApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/ready", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
