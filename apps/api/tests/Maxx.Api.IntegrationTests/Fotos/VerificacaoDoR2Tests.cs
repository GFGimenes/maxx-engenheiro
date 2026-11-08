using System.Net;
using System.Net.Http.Headers;
using Maxx.Application.Fotos;
using Microsoft.Extensions.DependencyInjection;

namespace Maxx.Api.IntegrationTests.Fotos;

// Verificação manual do R2 de verdade, antes de a produção depender dele. Fora da CI e
// pulada por padrão: só roda com MAXX_VERIFICAR_R2=1 e as MAXX_ARMAZENAMENTO_* do bucket
// real no ambiente, do mesmo jeito que o .env da VPS as tem. O que o emulador já prova
// (ArmazenamentoDeFotosTests) precisa valer também no R2: a assinatura prende tipo e
// tamanho, a leitura pela CDN é pública sem listagem, e a remoção apaga. Com
// MAXX_VERIFICAR_R2_ORIGEM (a URL pública do app), confere também o CORS do PUT.
//
//   MAXX_VERIFICAR_R2=1 dotnet test --solution Maxx.sln --filter-class *VerificacaoDoR2Tests
//
// Cada teste apaga o que enviou. Nenhum segredo é escrito na saída.
public sealed class VerificacaoDoR2Tests : IAsyncDisposable
{
    private const string _jpeg = "image/jpeg";
    private const int _tamanho = 1000;

    private static readonly bool _ligada = Environment.GetEnvironmentVariable("MAXX_VERIFICAR_R2") == "1";

    private readonly HttpClient _http = new();
    private readonly List<string> _enviadas = [];
    private MaxxApiFactory? _factory;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Envio_exato_e_aceito_e_lido_pela_cdn_sem_login()
    {
        var bytes = Bytes(_tamanho);
        var envio = Assinar();

        Assert.Equal(HttpStatusCode.OK, (await PutAsync(envio.Url, _jpeg, bytes)).StatusCode);
        Assert.True(await Armazenamento().ExisteAsync(envio.Chave, Ct));

        var leitura = await _http.GetAsync(Armazenamento().UrlDeLeitura(envio.Chave), Ct);
        Assert.Equal(HttpStatusCode.OK, leitura.StatusCode);
        Assert.Equal(bytes, await leitura.Content.ReadAsByteArrayAsync(Ct));
    }

    [Fact]
    public async Task Envio_com_outro_tipo_e_recusado()
    {
        var envio = Assinar();

        var resposta = await PutAsync(envio.Url, "text/html", Bytes(_tamanho));

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.False(await Armazenamento().ExisteAsync(envio.Chave, Ct));
    }

    // O ponto que só o R2 de verdade responde: se ele confere o content-length assinado.
    [Theory]
    [InlineData(_tamanho + 1)]
    [InlineData(_tamanho * 50)]
    public async Task Envio_maior_que_o_assinado_e_recusado(int outroTamanho)
    {
        var envio = Assinar();

        var resposta = await PutAsync(envio.Url, _jpeg, Bytes(outroTamanho));

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.False(await Armazenamento().ExisteAsync(envio.Chave, Ct));
    }

    [Fact]
    public async Task Remover_apaga_e_a_cdn_deixa_de_servir()
    {
        var envio = Assinar();
        Assert.Equal(HttpStatusCode.OK, (await PutAsync(envio.Url, _jpeg, Bytes(_tamanho))).StatusCode);

        await Armazenamento().RemoverAsync([envio.Chave], Ct);

        Assert.False(await Armazenamento().ExisteAsync(envio.Chave, Ct));
        using var pedido = new HttpRequestMessage(HttpMethod.Get, Armazenamento().UrlDeLeitura(envio.Chave));
        pedido.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        var leitura = await _http.SendAsync(pedido, Ct);
        Assert.NotEqual(HttpStatusCode.OK, leitura.StatusCode);
    }

    [Fact]
    public async Task Cors_libera_o_put_do_app_com_content_type()
    {
        var origem = Environment.GetEnvironmentVariable("MAXX_VERIFICAR_R2_ORIGEM");
        Assert.SkipUnless(_ligada && !string.IsNullOrWhiteSpace(origem), "Defina MAXX_VERIFICAR_R2=1 e MAXX_VERIFICAR_R2_ORIGEM.");
        var envio = Assinar();

        using var preflight = new HttpRequestMessage(HttpMethod.Options, envio.Url);
        preflight.Headers.Add("Origin", origem);
        preflight.Headers.Add("Access-Control-Request-Method", "PUT");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type");
        var resposta = await _http.SendAsync(preflight, Ct);

        Assert.True(resposta.IsSuccessStatusCode, $"Preflight respondeu {(int)resposta.StatusCode}.");
        Assert.True(resposta.Headers.TryGetValues("Access-Control-Allow-Origin", out var origens));
        Assert.Contains(origens, valor => valor == origem || valor == "*");
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            if (_enviadas.Count > 0)
            {
                await Armazenamento().RemoverAsync(_enviadas, CancellationToken.None);
            }

            await _factory.DisposeAsync();
        }

        _http.Dispose();
    }

    private EnvioAssinado Assinar()
    {
        var envio = Armazenamento().AssinarEnvio(
            $"verificacao/{Guid.NewGuid():N}.jpg", _jpeg, _tamanho, DateTimeOffset.UtcNow.AddMinutes(15));
        _enviadas.Add(envio.Chave);
        return envio;
    }

    // O adaptador como a API o monta, com a configuração do R2 vinda do ambiente.
    private IArmazenamentoDeFotos Armazenamento()
    {
        Assert.SkipUnless(_ligada, "Verificação manual do R2: defina MAXX_VERIFICAR_R2=1 e as MAXX_ARMAZENAMENTO_*.");
        _factory ??= new MaxxApiFactory(MaxxApiFactory.UnreachableDatabase, configuracao: ConfiguracaoDoAmbiente());
        return _factory.Services.GetRequiredService<IArmazenamentoDeFotos>();
    }

    private static Dictionary<string, string> ConfiguracaoDoAmbiente()
    {
        var configuracao = new Dictionary<string, string>();
        Copiar("BUCKET", "Bucket");
        Copiar("ENDPOINT", "Endpoint");
        Copiar("ENDPOINT_PUBLICO", "EndpointPublico");
        Copiar("URL_DE_LEITURA", "UrlDeLeitura");
        Copiar("REGIAO", "Regiao");
        Copiar("CHAVE_DE_ACESSO", "ChaveDeAcesso");
        Copiar("SEGREDO", "Segredo");
        return configuracao;

        void Copiar(string variavel, string chave)
        {
            var valor = Environment.GetEnvironmentVariable($"MAXX_ARMAZENAMENTO_{variavel}");
            if (!string.IsNullOrWhiteSpace(valor))
            {
                configuracao[$"Armazenamento:{chave}"] = valor;
            }
        }
    }

    private async Task<HttpResponseMessage> PutAsync(Uri url, string tipo, byte[] corpo)
    {
        using var conteudo = new ByteArrayContent(corpo);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue(tipo);
        return await _http.PutAsync(url, conteudo, Ct);
    }

    private static byte[] Bytes(int tamanho)
    {
        var bytes = new byte[tamanho];
        Random.Shared.NextBytes(bytes);
        return bytes;
    }
}
