using System.Net;

namespace Maxx.Api.IntegrationTests;

public class OpenApiDocumentTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Documento_e_servido_em_desenvolvimento()
    {
        await using var factory = new MaxxApiFactory(MaxxApiFactory.UnreachableDatabase, "Development");
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/openapi/v1.json", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"/api/health\"", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Documento_declara_o_bearer_e_o_exige_so_onde_ha_autenticacao()
    {
        await using var factory = new MaxxApiFactory(MaxxApiFactory.UnreachableDatabase, "Development");
        using var client = factory.CreateClient();

        using var documento = System.Text.Json.JsonDocument.Parse(
            await client.GetStringAsync(new Uri("/api/openapi/v1.json", UriKind.Relative), Ct));
        var raiz = documento.RootElement;

        var bearer = raiz.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());

        var eu = raiz.GetProperty("paths").GetProperty("/api/v1/eu").GetProperty("get");
        Assert.True(eu.GetProperty("security")[0].TryGetProperty("Bearer", out _));

        var health = raiz.GetProperty("paths").GetProperty("/api/health").GetProperty("get");
        Assert.False(health.TryGetProperty("security", out _));
    }

    [Fact]
    public async Task Documento_nao_existe_em_producao()
    {
        await using var factory = new MaxxApiFactory(
            MaxxApiFactory.UnreachableDatabase, "Production", configuracao: MaxxApiFactory.ChaveDeTeste);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/openapi/v1.json", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
