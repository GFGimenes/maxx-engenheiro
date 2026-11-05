using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Maxx.Application;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Infrastructure.Autenticacao;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.BancoDeTeste;

namespace Maxx.Api.IntegrationTests.Autenticacao;

// Os limites de taxa (decisoes/README.md, Autenticação) e o IP que eles enxergam. O host de
// teste faz o papel da rede: o cabeçalho X-Teste-Conexao diz de que IP veio a conexão TCP,
// e o X-Forwarded-For é o que o Caddy (ou um cliente forjando) mandou.
public sealed class LimiteDeTaxaTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string _senha = "senha-boa-123";
    private const string _ana = "ana.souza@al.unieduk.com.br";
    private const string _caddy = "172.30.0.10";

    private static readonly Uri _login = new("/api/v1/auth/login", UriKind.Relative);
    private static readonly Uri _cadastro = new("/api/v1/auth/cadastro", UriKind.Relative);
    private static readonly Uri _sugestao = new("/api/v1/auth/cadastro/sugestao", UriKind.Relative);
    private static readonly Uri _disponibilidade = new("/api/v1/auth/cadastro/disponibilidade", UriKind.Relative);
    private static readonly Uri _recuperacao = new("/api/v1/auth/recuperacao", UriKind.Relative);
    private static readonly Uri _demo = new("/api/v1/demo/alunos", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Login_aceita_5_por_minuto_do_mesmo_identificador_e_o_sexto_e_429()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        for (var tentativa = 0; tentativa < 5; tentativa++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await LoginAsync(client, _ana, "senha-errada-1")).StatusCode);
        }

        // Outra caixa e espaços: o mesmo identificador.
        await Assert429Async(await LoginAsync(client, " ANA.Souza@al.unieduk.com.br", _senha), TimeSpan.FromMinutes(1));

        // Outro identificador segue entrando.
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, "20260101", _senha)).StatusCode);
    }

    [Fact]
    public async Task Login_aceita_30_por_minuto_do_mesmo_ip_real_e_o_ip_vem_do_caddy()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        for (var tentativa = 0; tentativa < 30; tentativa++)
        {
            var response = await LoginAsync(client, $"ninguem{tentativa}@al.unieduk.com.br", "x", conexao: _caddy, encaminhado: "203.0.113.7");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        await Assert429Async(
            await LoginAsync(client, "outro@al.unieduk.com.br", "x", conexao: _caddy, encaminhado: "203.0.113.7"), TimeSpan.FromMinutes(1));

        // Outra pessoa atrás do mesmo Caddy tem o próprio limite.
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await LoginAsync(client, "outro@al.unieduk.com.br", "x", conexao: _caddy, encaminhado: "203.0.113.8")).StatusCode);
    }

    [Fact]
    public async Task X_forwarded_for_de_quem_nao_e_o_caddy_nao_muda_o_ip()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        // De fora da rede confiável, trocar o X-Forwarded-For a cada pedido não ganha
        // limite novo: o IP é o da conexão.
        for (var tentativa = 0; tentativa < 30; tentativa++)
        {
            await LoginAsync(
                client, $"ninguem{tentativa}@al.unieduk.com.br", "x", conexao: "198.51.100.9", encaminhado: $"203.0.113.{tentativa}");
        }

        await Assert429Async(
            await LoginAsync(client, "outro@al.unieduk.com.br", "x", conexao: "198.51.100.9", encaminhado: "203.0.113.200"),
            TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Cadastro_aceita_3_a_cada_15_minutos_do_mesmo_ra()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();
        var pedido = new { ra = "20260102", email = "bruno.lima@al.unieduk.com.br", nomeDeExibicao = "Bruno", prefixo = "brunoLima", senha = "curta" };

        for (var tentativa = 0; tentativa < 3; tentativa++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(_cadastro, pedido, Ct)).StatusCode);
        }

        await Assert429Async(await client.PostAsJsonAsync(_cadastro, pedido with { senha = _senha }, Ct), TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task Sugestao_e_disponibilidade_somam_60_por_minuto_do_mesmo_ip_e_a_61a_e_429()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();
        var bruno = new { ra = "20260102", email = "bruno.lima@al.unieduk.com.br" };

        // As duas rotas gastam o mesmo limite: meio a meio, como na digitação do arroba.
        for (var consulta = 0; consulta < 60; consulta++)
        {
            var response = consulta % 2 == 0
                ? await PostAsync(client, _sugestao, bruno, _caddy, "203.0.113.20")
                : await PostAsync(client, _disponibilidade, new { bruno.ra, bruno.email, prefixo = $"bruno{consulta}" }, _caddy, "203.0.113.20");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await Assert429Async(await PostAsync(client, _sugestao, bruno, _caddy, "203.0.113.20"), TimeSpan.FromMinutes(1));
        await Assert429Async(
            await PostAsync(client, _disponibilidade, new { bruno.ra, bruno.email, prefixo = "brunoLima" }, _caddy, "203.0.113.20"),
            TimeSpan.FromMinutes(1));

        // Outro IP segue consultando, e o cadastro tem limite próprio.
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(client, _sugestao, bruno, _caddy, "203.0.113.21")).StatusCode);
    }

    [Fact]
    public async Task Recuperacao_aceita_3_a_cada_15_minutos_do_mesmo_email()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        for (var tentativa = 0; tentativa < 3; tentativa++)
        {
            Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync(_recuperacao, new { email = _ana }, Ct)).StatusCode);
        }

        await Assert429Async(await client.PostAsJsonAsync(_recuperacao, new { email = _ana.ToUpperInvariant() }, Ct), TimeSpan.FromMinutes(15));

        // Exista o e-mail ou não, o limite é o mesmo: o 429 não revela conta.
        for (var tentativa = 0; tentativa < 3; tentativa++)
        {
            await client.PostAsJsonAsync(_recuperacao, new { email = "ninguem@al.unieduk.com.br" }, Ct);
        }

        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            (await client.PostAsJsonAsync(_recuperacao, new { email = "ninguem@al.unieduk.com.br" }, Ct)).StatusCode);
    }

    [Fact]
    public async Task Uma_banca_inteira_atras_do_mesmo_ip_registra_60_alunos_de_demonstracao_por_hora()
    {
        await using var factory = await MundoAsync(demonstracao: true);
        using var client = factory.CreateClient();

        for (var pessoa = 0; pessoa < 60; pessoa++)
        {
            var response = await PostAsync(client, _demo, new { nome = $"Banca {pessoa}", curso = "CC" }, _caddy, "203.0.113.50");
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        await Assert429Async(
            await PostAsync(client, _demo, new { nome = "Banca 61", curso = "CC" }, _caddy, "203.0.113.50"), TimeSpan.FromHours(1));
    }

    [Fact]
    public async Task O_429_nao_leva_identificador_para_o_log()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();
        for (var tentativa = 0; tentativa < 6; tentativa++)
        {
            await LoginAsync(client, "identificador-sigiloso@al.unieduk.com.br", "x");
        }

        var log = string.Join('\n', factory.Log.Linhas);
        Assert.Contains("Limite de taxa LoginPorIdentificador estourado", log, StringComparison.Ordinal);
        Assert.DoesNotContain("identificador-sigiloso", log, StringComparison.Ordinal);
    }

    private async Task<MaxxApiFactory> MundoAsync(bool demonstracao = false)
    {
        var configuracao = new Dictionary<string, string> { ["Proxy:RedesConfiaveis:0"] = "172.30.0.0/24" };
        if (demonstracao)
        {
            configuracao["Demo:Habilitada"] = "true";
            configuracao["Demo:Lote"] = "banca-teste";
        }

        var factory = new MaxxApiFactory(
            await postgres.CreateDatabaseAsync(),
            configuracao: configuracao,
            servicos: servicos =>
            {
                servicos.AddSingleton<IStartupFilter, ConexaoDeTeste>();
                servicos.AddSingleton<IEmailSender, SemEnvio>();
            });
        await factory.MigrateAsync(Ct);

        var hoje = FusoHorario.DataLocal(DateTimeOffset.UtcNow);
        Bimestre Bimestre(int numero) => new(numero, hoje.AddDays(-75 + ((numero - 1) * 51)), hoje.AddDays(-75 + ((numero - 1) * 51) + 49));
        var ano = new AnoLetivo(
            hoje.Year, new SemestreLetivo(hoje.Year, 1, Bimestre(1), Bimestre(2)), new SemestreLetivo(hoje.Year, 2, Bimestre(3), Bimestre(4)));
        var cc = new Curso("Ciência da Computação", "CC", 8);
        var bd = new Materia(cc, "Banco de Dados");
        var ana = CadastroElegivel.Aluno("20260101", _ana, "Ana Souza", cc, 3);
        await SalvarAsync(
            factory,
            ano,
            cc,
            bd,
            new Turma(bd, ano.Semestres[0]),
            ana,
            CadastroElegivel.Aluno("20260102", "bruno.lima@al.unieduk.com.br", "Bruno Lima", cc, 3),
            Usuario.CadastrarAluno(ana, cc, "Ana", "anaSouza", new Argon2idSenhaHasher().Hash(_senha), DateTimeOffset.UtcNow));
        return factory;
    }

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client, string identificador, string senha, string? conexao = null, string? encaminhado = null) =>
        PostAsync(client, _login, new { identificador, senha }, conexao, encaminhado);

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, Uri rota, object corpo, string? conexao, string? encaminhado)
    {
        var pedido = new HttpRequestMessage(HttpMethod.Post, rota) { Content = JsonContent.Create(corpo) };
        if (conexao is not null)
        {
            pedido.Headers.Add(ConexaoDeTeste.Cabecalho, conexao);
        }

        if (encaminhado is not null)
        {
            pedido.Headers.Add("X-Forwarded-For", encaminhado);
        }

        return client.SendAsync(pedido, Ct);
    }

    private static async Task Assert429Async(HttpResponseMessage response, TimeSpan janela)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var corpo = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal("limite-de-taxa", corpo.RootElement.GetProperty("codigo").GetString());
        Assert.Equal(429, corpo.RootElement.GetProperty("status").GetInt32());

        var retryAfter = Assert.Single(response.Headers.GetValues("Retry-After"));
        Assert.InRange(int.Parse(retryAfter, System.Globalization.CultureInfo.InvariantCulture), 1, (int)janela.TotalSeconds);
    }

    // A conexão TCP vinda do IP que o teste escolheu, antes de todo o pipeline: é o que o
    // ForwardedHeaders vê como remetente.
    private sealed class ConexaoDeTeste : IStartupFilter
    {
        public const string Cabecalho = "X-Teste-Conexao";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (contexto, proximo) =>
            {
                if (contexto.Request.Headers.TryGetValue(Cabecalho, out var ip))
                {
                    contexto.Connection.RemoteIpAddress = IPAddress.Parse(ip.ToString());
                }

                await proximo(contexto);
            });
            next(app);
        };
    }

    private sealed class SemEnvio : IEmailSender
    {
        public Task EnviarAsync(MensagemDeEmail mensagem, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
