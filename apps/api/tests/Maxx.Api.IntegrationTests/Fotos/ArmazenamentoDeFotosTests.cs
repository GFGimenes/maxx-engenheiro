using System.Net;
using System.Net.Http.Headers;
using System.Web;
using Maxx.Application.Fotos;
using Microsoft.Extensions.DependencyInjection;

namespace Maxx.Api.IntegrationTests.Fotos;

// O bucket de verdade (o emulador), pelo adaptador montado pela API. O que se prova aqui é
// a garantia do ADR 0002: a assinatura prende tipo e tamanho, e quem recusa o envio fora
// dela é o próprio bucket, não o app.
public sealed class ArmazenamentoDeFotosTests(ArmazenamentoFixture armazenamento) : IClassFixture<ArmazenamentoFixture>, IAsyncDisposable
{
    private const string _jpeg = "image/jpeg";
    private const int _tamanho = 1000;

    private static readonly TimeSpan _validade = TimeSpan.FromMinutes(15);

    private readonly HttpClient _http = new();
    private MaxxApiFactory? _factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Envio_exato_e_aceito_existe_e_e_lido_sem_login()
    {
        var bytes = Bytes(_tamanho);
        var envio = Armazenamento().AssinarEnvio(Chave(), _jpeg, _tamanho, Expira());

        var resposta = await PutAsync(envio.Url, _jpeg, bytes);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.True(await Armazenamento().ExisteAsync(envio.Chave, Ct));

        // A leitura é pública para quem tem o link (ADR 0002): GET sem credencial nenhuma.
        var leitura = await _http.GetAsync(Armazenamento().UrlDeLeitura(envio.Chave), Ct);
        Assert.Equal(HttpStatusCode.OK, leitura.StatusCode);
        Assert.Equal(bytes, await leitura.Content.ReadAsByteArrayAsync(Ct));
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("text/html")]
    public async Task Envio_com_outro_tipo_e_recusado_pelo_bucket(string outroTipo)
    {
        var envio = Armazenamento().AssinarEnvio(Chave(), _jpeg, _tamanho, Expira());

        var resposta = await PutAsync(envio.Url, outroTipo, Bytes(_tamanho));

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.False(await Armazenamento().ExisteAsync(envio.Chave, Ct));
    }

    [Theory]
    [InlineData(_tamanho - 1)]
    [InlineData(_tamanho + 1)]
    [InlineData(_tamanho * 50)]
    public async Task Envio_com_outro_tamanho_e_recusado_pelo_bucket(int outroTamanho)
    {
        var envio = Armazenamento().AssinarEnvio(Chave(), _jpeg, _tamanho, Expira());

        var resposta = await PutAsync(envio.Url, _jpeg, Bytes(outroTamanho));

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.False(await Armazenamento().ExisteAsync(envio.Chave, Ct));
    }

    [Fact]
    public async Task Envio_depois_da_validade_e_recusado_pelo_bucket()
    {
        var envio = Armazenamento().AssinarEnvio(Chave(), _jpeg, _tamanho, DateTimeOffset.UtcNow.AddSeconds(1));
        await Task.Delay(TimeSpan.FromSeconds(2.5), Ct);

        var resposta = await PutAsync(envio.Url, _jpeg, Bytes(_tamanho));

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public void Assinatura_prende_tipo_e_tamanho_nos_cabecalhos_assinados()
    {
        var envio = Armazenamento().AssinarEnvio(Chave(), _jpeg, _tamanho, Expira());

        var assinados = HttpUtility.ParseQueryString(envio.Url.Query)["X-Amz-SignedHeaders"]?.Split(';');

        Assert.NotNull(assinados);
        Assert.Contains("content-type", assinados);
        Assert.Contains("content-length", assinados);
        Assert.Equal(_jpeg, envio.TipoDeConteudo);
        Assert.Equal(_tamanho, envio.Tamanho);
    }

    [Fact]
    public async Task Remover_apaga_o_objeto_e_tolera_chave_inexistente()
    {
        var envio = Armazenamento().AssinarEnvio(Chave(), _jpeg, _tamanho, Expira());
        Assert.Equal(HttpStatusCode.OK, (await PutAsync(envio.Url, _jpeg, Bytes(_tamanho))).StatusCode);

        await Armazenamento().RemoverAsync([envio.Chave, Chave()], Ct);

        Assert.False(await Armazenamento().ExisteAsync(envio.Chave, Ct));
        Assert.Equal(HttpStatusCode.NotFound, (await _http.GetAsync(Armazenamento().UrlDeLeitura(envio.Chave), Ct)).StatusCode);

        // De novo, já sem nada: a limpeza pela fila pode rodar mais de uma vez.
        await Armazenamento().RemoverAsync([envio.Chave], Ct);
    }

    [Fact]
    public async Task Fora_de_desenvolvimento_sem_armazenamento_a_api_nao_sobe()
    {
        var semBucket = MaxxApiFactory.ChaveDeTeste
            .Where(par => !par.Key.StartsWith("Armazenamento:", StringComparison.Ordinal))
            .ToDictionary();
        await using var factory = new MaxxApiFactory(MaxxApiFactory.UnreachableDatabase, "Production", configuracao: semBucket);

        var falha = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Armazenamento__Bucket", falha.Message, StringComparison.Ordinal);
        Assert.Contains("Armazenamento__Segredo", falha.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Em_desenvolvimento_sem_armazenamento_a_api_sobe_e_so_o_uso_falha()
    {
        await using var factory = new MaxxApiFactory(MaxxApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri("/api/health", UriKind.Relative), Ct)).StatusCode);

        var armazenamento = factory.Services.GetRequiredService<IArmazenamentoDeFotos>();

        var falha = Assert.Throws<InvalidOperationException>(() => armazenamento.AssinarEnvio(Chave(), _jpeg, _tamanho, Expira()));
        Assert.Contains("Armazenamento__Endpoint", falha.Message, StringComparison.Ordinal);
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    // O adaptador como a API o monta: pela injeção de dependência, com a configuração do
    // emulador. A API em si não precisa de banco para isto.
    private IArmazenamentoDeFotos Armazenamento()
    {
        _factory ??= new MaxxApiFactory(MaxxApiFactory.UnreachableDatabase, configuracao: armazenamento.Configuracao);
        return _factory.Services.GetRequiredService<IArmazenamentoDeFotos>();
    }

    // Como o app fará: PUT direto na URL assinada, com o tipo e o corpo, e o
    // Content-Length que o HttpClient tira do corpo.
    private async Task<HttpResponseMessage> PutAsync(Uri url, string tipo, byte[] corpo)
    {
        using var conteudo = new ByteArrayContent(corpo);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue(tipo);
        return await _http.PutAsync(url, conteudo, Ct);
    }

    private static string Chave() => $"teste/{Guid.NewGuid():N}.jpg";

    private static DateTimeOffset Expira() => DateTimeOffset.UtcNow.Add(_validade);

    private static byte[] Bytes(int tamanho)
    {
        var bytes = new byte[tamanho];
        Random.Shared.NextBytes(bytes);
        return bytes;
    }
}
