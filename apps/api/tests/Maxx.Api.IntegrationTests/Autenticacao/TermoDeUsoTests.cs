using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Maxx.Api.Autenticacao;
using Maxx.Domain.Identidade;
using static Maxx.Api.IntegrationTests.BancoDeTeste;

namespace Maxx.Api.IntegrationTests.Autenticacao;

// A leitura do termo vigente. O aceite é testado no cadastro e na demonstração, e a
// publicação, no seed base.
public sealed class TermoDeUsoTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly Uri _termo = new("/api/v1/termo-de-uso", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sem_login_devolve_o_termo_publicado_pelo_seed()
    {
        await using var factory = await MigradoAsync(postgres);
        await factory.SeedBaseAsync(Ct);
        using var client = factory.CreateClient();

        var termo = await client.GetFromJsonAsync<TermoDeUsoResponse>(_termo, Ct);

        Assert.Equal(1, termo!.Versao);

        // O que produto.md 2.6 manda o termo cobrir.
        Assert.Contains("nome de exibição", termo.Texto, StringComparison.Ordinal);
        Assert.Contains("arroba", termo.Texto, StringComparison.Ordinal);
        Assert.Contains("curso", termo.Texto, StringComparison.Ordinal);
        Assert.Contains("prêmios", termo.Texto, StringComparison.Ordinal);
        Assert.Contains("posição no ranking", termo.Texto, StringComparison.Ordinal);
        Assert.Contains("pública para quem tem o link", termo.Texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_vigente_e_a_de_numero_mais_alto()
    {
        await using var factory = await MigradoAsync(postgres);
        await SalvarAsync(
            factory,
            new TermoDeUso(1, "Antigo.", DateTimeOffset.UtcNow),
            new TermoDeUso(3, "Vigente.", DateTimeOffset.UtcNow),
            new TermoDeUso(2, "Do meio.", DateTimeOffset.UtcNow));
        using var client = factory.CreateClient();

        Assert.Equal(new TermoDeUsoResponse(3, "Vigente."), await client.GetFromJsonAsync<TermoDeUsoResponse>(_termo, Ct));
    }

    [Fact]
    public async Task Sem_termo_publicado_responde_404_com_o_codigo_proprio()
    {
        await using var factory = await MigradoAsync(postgres);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(_termo, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var corpo = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal("termo-indisponivel", corpo.RootElement.GetProperty("codigo").GetString());
    }
}
