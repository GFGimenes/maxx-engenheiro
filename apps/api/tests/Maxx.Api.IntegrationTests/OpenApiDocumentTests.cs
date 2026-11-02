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
    public async Task Documento_nao_existe_em_producao()
    {
        await using var factory = new MaxxApiFactory(MaxxApiFactory.UnreachableDatabase, "Production");
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/openapi/v1.json", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
