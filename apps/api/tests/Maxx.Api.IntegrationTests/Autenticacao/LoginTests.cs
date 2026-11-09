using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Maxx.Api.Autenticacao;
using Maxx.Application.Autenticacao;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Infrastructure.Autenticacao;
using Maxx.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.BancoDeTeste;

namespace Maxx.Api.IntegrationTests.Autenticacao;

// Login, renovação e logout pela API, contra o Postgres real (fluxos/01-login.md).
public sealed class LoginTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string _senha = "senha-boa-123";
    private const string _senhaDaRenata = "renata-senha-456";

    private static readonly Uri _cadastro = new("/api/v1/auth/cadastro", UriKind.Relative);
    private static readonly Uri _login = new("/api/v1/auth/login", UriKind.Relative);
    private static readonly Uri _refresh = new("/api/v1/auth/refresh", UriKind.Relative);
    private static readonly Uri _logout = new("/api/v1/auth/logout", UriKind.Relative);
    private static readonly Uri _eu = new("/api/v1/eu", UriKind.Relative);
    private static readonly CadastroRequest _ana = new("20260101", "ana.souza@al.unieduk.com.br", "Ana", "anaSouza", _senha, VersaoDoTermo);
    private const string _emailDaRenata = "renata@prof.unieduk.com.br";
    private const string _raDaRenata = "PROF-1";
    private const string _emailDaSecretaria = "secretaria@unieduk.com.br";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Aluno_entra_pelo_ra_e_pelo_email_em_qualquer_caixa()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        var porRa = await EntrarAsync(client, _ana.Ra, _senha);
        var porEmail = await EntrarAsync(client, " ANA.Souza@AL.unieduk.com.br ", _senha);

        Assert.Equal(["aluno"], await PapeisAsync(client, porRa.AccessToken));
        Assert.Equal(["aluno"], await PapeisAsync(client, porEmail.AccessToken));
        Assert.False(string.IsNullOrEmpty(porRa.RefreshToken));
        Assert.NotEqual(porRa.RefreshToken, porEmail.RefreshToken);
    }

    [Fact]
    public async Task Senha_errada_usuario_inexistente_e_conta_sem_senha_tem_a_mesma_resposta()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        var respostas = new[]
        {
            await PedirLoginAsync(client, _ana.Ra, "senha-errada-1"),
            await PedirLoginAsync(client, _ana.Email, "senha-errada-1"),
            await PedirLoginAsync(client, "99999999", "senha-errada-1"),
            await PedirLoginAsync(client, "ninguem@al.unieduk.com.br", "senha-errada-1"),
            await PedirLoginAsync(client, _emailDaSecretaria, "senha-errada-1"),
        };

        var corpos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var response in respostas)
        {
            await AssertRecusaAsync(response, HttpStatusCode.Unauthorized, "credenciais-invalidas");
            corpos.Add(await CorpoSemTraceAsync(response));
        }

        Assert.Single(corpos);
    }

    [Fact]
    public async Task Usuario_inexistente_e_conta_sem_senha_pagam_o_mesmo_argon2id_da_senha_errada()
    {
        var hasher = new SenhaHasherContado(new Argon2idSenhaHasher());
        await using var factory = await MundoAsync(servicos => servicos.AddSingleton<ISenhaHasher>(hasher));
        using var client = factory.CreateClient();
        hasher.Zerar();

        await PedirLoginAsync(client, "99999999", "qualquer-senha");
        await PedirLoginAsync(client, "ninguem@al.unieduk.com.br", "qualquer-senha");
        await PedirLoginAsync(client, _emailDaSecretaria, "qualquer-senha");
        Assert.Equal((0, 3), (hasher.Verificacoes, hasher.Ficticias));

        await PedirLoginAsync(client, _ana.Ra, "senha-errada-1");
        Assert.Equal((1, 3), (hasher.Verificacoes, hasher.Ficticias));
    }

    [Fact]
    public async Task Professor_entra_pelo_email_e_nunca_pelo_ra()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        var porEmail = await EntrarAsync(client, _emailDaRenata, _senhaDaRenata);
        Assert.Equal(["professor"], await PapeisAsync(client, porEmail.AccessToken));

        // A linha dela tem RA, e a senha está certa: ainda assim, RA é só de aluno.
        await AssertRecusaAsync(
            await PedirLoginAsync(client, _raDaRenata, _senhaDaRenata), HttpStatusCode.Unauthorized, "credenciais-invalidas");
    }

    [Fact]
    public async Task Ex_aluno_continua_entrando()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
            var ana = await db.Set<Usuario>().SingleAsync(usuario => usuario.Papeis == Papeis.Aluno, Ct);
            ana.MarcarExAluno(ana.Id, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(Ct);
        }

        var sessao = await EntrarAsync(client, _ana.Ra, _senha);

        Assert.Equal(["aluno"], await PapeisAsync(client, sessao.AccessToken));
    }

    [Fact]
    public async Task Refresh_troca_o_token_e_devolve_um_access_novo()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();
        var r1 = (await EntrarAsync(client, _ana.Ra, _senha)).RefreshToken!;

        var response = await PedirRefreshAsync(client, r1);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var renovada = (await response.Content.ReadFromJsonAsync<SessaoResponse>(Ct))!;
        Assert.NotEqual(r1, renovada.RefreshToken);
        Assert.InRange(renovada.RefreshTokenExpiraEm - DateTimeOffset.UtcNow, TimeSpan.FromDays(29.9), TimeSpan.FromDays(30));
        Assert.Equal(["aluno"], await PapeisAsync(client, renovada.AccessToken));
    }

    [Fact]
    public async Task Reuso_de_r1_revoga_a_familia_e_derruba_tambem_o_r2()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();
        var r1 = (await EntrarAsync(client, _ana.Ra, _senha)).RefreshToken!;
        var r2 = await RenovarAsync(client, r1);

        await AssertRecusaAsync(await PedirRefreshAsync(client, r1), HttpStatusCode.Unauthorized, "sessao-invalida");
        await AssertRecusaAsync(await PedirRefreshAsync(client, r2), HttpStatusCode.Unauthorized, "sessao-invalida");

        var familia = await FamiliaAsync(factory);
        Assert.Equal(MotivoDaRevogacao.Reuso, familia.MotivoDaRevogacao);
        Assert.Contains(factory.Log.Linhas, linha => linha.StartsWith("Warning", StringComparison.Ordinal) && linha.Contains("Reuso de refresh", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reuso_derruba_so_a_familia_dele()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();
        var celular = (await EntrarAsync(client, _ana.Ra, _senha)).RefreshToken!;
        var notebook = (await EntrarAsync(client, _ana.Email, _senha)).RefreshToken!;
        await RenovarAsync(client, celular);

        await PedirRefreshAsync(client, celular);

        Assert.Equal(HttpStatusCode.OK, (await PedirRefreshAsync(client, notebook)).StatusCode);
    }

    [Fact]
    public async Task Renovacoes_simultaneas_com_o_mesmo_refresh_deixam_um_200_e_derrubam_a_familia()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        // Algumas rodadas, para a corrida de fato acontecer em pelo menos parte delas. Em
        // todas, o resultado é o do reuso em sequência.
        for (var rodada = 0; rodada < 5; rodada++)
        {
            var r1 = (await EntrarAsync(client, _ana.Ra, _senha)).RefreshToken!;

            var respostas = await Task.WhenAll(PedirRefreshAsync(client, r1), PedirRefreshAsync(client, r1));

            var vencedor = Assert.Single(respostas, response => response.StatusCode == HttpStatusCode.OK);
            var perdedor = Assert.Single(respostas, response => response.StatusCode != HttpStatusCode.OK);
            await AssertRecusaAsync(perdedor, HttpStatusCode.Unauthorized, "sessao-invalida");
            var r2 = (await vencedor.Content.ReadFromJsonAsync<SessaoResponse>(Ct))!.RefreshToken!;
            Assert.Equal(HttpStatusCode.Unauthorized, (await PedirRefreshAsync(client, r2)).StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        Assert.All(
            await db.Set<FamiliaDeSessao>().Include(familia => familia.Tokens).ToListAsync(Ct),
            familia =>
            {
                Assert.Equal(MotivoDaRevogacao.Reuso, familia.MotivoDaRevogacao);
                Assert.Equal(2, familia.Tokens.Count);
            });
    }

    [Fact]
    public async Task Refresh_expirado_e_recusado_sem_revogar_a_familia()
    {
        var relogio = new RelogioDeTeste();
        await using var factory = await MundoAsync(servicos => servicos.AddSingleton<TimeProvider>(relogio));
        using var client = factory.CreateClient();
        var r1 = (await EntrarAsync(client, _ana.Ra, _senha)).RefreshToken!;

        relogio.Avancar(FamiliaDeSessao.ValidadeDoRefresh + TimeSpan.FromMinutes(1));

        await AssertRecusaAsync(await PedirRefreshAsync(client, r1), HttpStatusCode.Unauthorized, "sessao-invalida");
        Assert.Null((await FamiliaAsync(factory)).RevogadaEm);
    }

    [Fact]
    public async Task Refresh_ausente_ou_desconhecido_e_recusado()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        await AssertRecusaAsync(
            await client.PostAsJsonAsync(_refresh, new { }, Ct), HttpStatusCode.Unauthorized, "sessao-invalida");
        await AssertRecusaAsync(
            await client.PostAsync(_refresh, content: null, Ct), HttpStatusCode.Unauthorized, "sessao-invalida");
        await AssertRecusaAsync(
            await PedirRefreshAsync(client, "nao-e-um-refresh"), HttpStatusCode.Unauthorized, "sessao-invalida");
    }

    [Fact]
    public async Task Depois_do_logout_o_refresh_da_401()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();
        var r1 = (await EntrarAsync(client, _ana.Ra, _senha)).RefreshToken!;

        var response = await client.PostAsJsonAsync(_logout, new { refreshToken = r1 }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AssertRecusaAsync(await PedirRefreshAsync(client, r1), HttpStatusCode.Unauthorized, "sessao-invalida");
        Assert.Equal(MotivoDaRevogacao.Logout, (await FamiliaAsync(factory)).MotivoDaRevogacao);
    }

    [Fact]
    public async Task Logout_repetido_ou_com_refresh_desconhecido_responde_204()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();
        var r1 = (await EntrarAsync(client, _ana.Ra, _senha)).RefreshToken!;
        await client.PostAsJsonAsync(_logout, new { refreshToken = r1 }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(_logout, new { refreshToken = r1 }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(_logout, new { refreshToken = "outro" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync(_logout, content: null, Ct)).StatusCode);
    }

    [Fact]
    public async Task Na_web_o_refresh_anda_so_em_cookie_do_login_ao_logout()
    {
        await using var factory = await MundoAsync();
        using var navegador = Navegador(factory);

        var login = await navegador.PostAsJsonAsync(_login, new { identificador = _ana.Ra, senha = _senha }, Ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Null((await login.Content.ReadFromJsonAsync<SessaoResponse>(Ct))!.RefreshToken);
        var r1 = ValorDoCookie(login);

        // O navegador manda o cookie sozinho; o corpo fica vazio.
        var refresh = await navegador.PostAsync(_refresh, content: null, Ct);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var renovada = (await refresh.Content.ReadFromJsonAsync<SessaoResponse>(Ct))!;
        Assert.Null(renovada.RefreshToken);
        Assert.NotEqual(r1, ValorDoCookie(refresh));
        Assert.Equal(["aluno"], await PapeisAsync(navegador, renovada.AccessToken));

        var logout = await navegador.PostAsync(_logout, content: null, Ct);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains(
            Assert.Single(logout.Headers.GetValues("Set-Cookie")).Split(';', StringSplitOptions.TrimEntries),
            atributo => atributo.StartsWith("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(MotivoDaRevogacao.Logout, (await FamiliaAsync(factory)).MotivoDaRevogacao);
    }

    [Fact]
    public async Task Na_web_o_cookie_sem_o_cabecalho_do_cliente_nao_renova()
    {
        await using var factory = await MundoAsync();
        using var navegador = Navegador(factory);
        await navegador.PostAsJsonAsync(_login, new { identificador = _ana.Ra, senha = _senha }, Ct);

        // Um pedido forjado por outro site leva o cookie (se o SameSite falhar), mas não
        // consegue pôr o cabeçalho.
        navegador.DefaultRequestHeaders.Remove(SessaoHttp.CabecalhoDoCliente);

        await AssertRecusaAsync(
            await navegador.PostAsync(_refresh, content: null, Ct), HttpStatusCode.Unauthorized, "sessao-invalida");
        Assert.Null((await FamiliaAsync(factory)).RevogadaEm);
    }

    [Fact]
    public async Task Na_web_refresh_recusado_apaga_o_cookie()
    {
        await using var factory = await MundoAsync();
        using var navegador = Navegador(factory);
        await navegador.PostAsJsonAsync(_login, new { identificador = _ana.Ra, senha = _senha }, Ct);
        await navegador.PostAsync(_logout, content: null, Ct);

        // Sem cookie nenhum no navegador, e o refresh recusado ainda manda apagar.
        var response = await navegador.PostAsync(_refresh, content: null, Ct);

        await AssertRecusaAsync(response, HttpStatusCode.Unauthorized, "sessao-invalida");
        Assert.StartsWith("maxx_refresh=;", Assert.Single(response.Headers.GetValues("Set-Cookie")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Senha_e_refresh_nunca_aparecem_no_log()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        await PedirLoginAsync(client, _ana.Ra, "senha-errada-que-nao-vai-ao-log");
        var sessao = await EntrarAsync(client, _ana.Email, _senha);
        var r2 = await RenovarAsync(client, sessao.RefreshToken!);
        await PedirRefreshAsync(client, sessao.RefreshToken!);
        await client.PostAsJsonAsync(_logout, new { refreshToken = r2 }, Ct);

        var log = string.Join('\n', factory.Log.Linhas);
        Assert.NotEmpty(factory.Log.Linhas);
        Assert.Contains("credenciais-invalidas", log, StringComparison.Ordinal);
        foreach (var segredo in new[] { _senha, "senha-errada-que-nao-vai-ao-log", sessao.RefreshToken!, r2, sessao.AccessToken })
        {
            Assert.DoesNotContain(segredo, log, StringComparison.Ordinal);
        }
    }

    private async Task<MaxxApiFactory> MundoAsync(Action<IServiceCollection>? servicos = null)
    {
        var factory = new MaxxApiFactory(await postgres.CreateDatabaseAsync(), servicos: servicos);
        await factory.MigrateAsync(Ct);

        var cc = new Curso("Ciência da Computação", "CC", 8);
        var renata = CadastroElegivel.Professor(_emailDaRenata, "Renata Albuquerque", ra: _raDaRenata);
        var secretaria = CadastroElegivel.Instituicao(_emailDaSecretaria, "Secretaria UniMax");
        var contaDaRenata = Usuario.CriarPeloSeed(renata, "renataA", DateTimeOffset.UtcNow);
        contaDaRenata.DefinirSenha(new Argon2idSenhaHasher().Hash(_senhaDaRenata));
        await SalvarAsync(
            factory,
            TermoVigente(),
            cc,
            CadastroElegivel.Aluno(_ana.Ra, _ana.Email, "Ana Souza", cc, 3),
            renata,
            secretaria,
            contaDaRenata,
            Usuario.CriarPeloSeed(secretaria, "secretaria", DateTimeOffset.UtcNow));

        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(_cadastro, _ana, Ct)).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<FamiliaDeSessao>().ExecuteDeleteAsync(Ct);
        return factory;
    }

    // Cliente que guarda cookies como o navegador. HTTPS, porque o cookie é Secure.
    private static HttpClient Navegador(MaxxApiFactory factory)
    {
        var navegador = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });
        navegador.DefaultRequestHeaders.Add(SessaoHttp.CabecalhoDoCliente, SessaoHttp.ClienteWeb);
        return navegador;
    }

    private static string ValorDoCookie(HttpResponseMessage response)
    {
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("maxx_refresh=", cookie, StringComparison.Ordinal);
        return cookie.Split(';')[0]["maxx_refresh=".Length..];
    }

    private static Task<HttpResponseMessage> PedirLoginAsync(HttpClient client, string identificador, string senha) =>
        client.PostAsJsonAsync(_login, new { identificador, senha }, Ct);

    private static async Task<SessaoResponse> EntrarAsync(HttpClient client, string identificador, string senha)
    {
        var response = await PedirLoginAsync(client, identificador, senha);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SessaoResponse>(Ct))!;
    }

    private static Task<HttpResponseMessage> PedirRefreshAsync(HttpClient client, string refresh) =>
        client.PostAsJsonAsync(_refresh, new { refreshToken = refresh }, Ct);

    private static async Task<string> RenovarAsync(HttpClient client, string refresh)
    {
        var response = await PedirRefreshAsync(client, refresh);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SessaoResponse>(Ct))!.RefreshToken!;
    }

    private static async Task<IReadOnlyList<string>> PapeisAsync(HttpClient client, string accessToken)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Get, _eu);
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(pedido, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EuResponse>(Ct))!.Papeis;
    }

    private static async Task<FamiliaDeSessao> FamiliaAsync(MaxxApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<FamiliaDeSessao>().SingleAsync(Ct);
    }

    private static async Task AssertRecusaAsync(HttpResponseMessage response, HttpStatusCode status, string codigo)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var corpo = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(codigo, corpo.RootElement.GetProperty("codigo").GetString());
    }

    private static async Task<string> CorpoSemTraceAsync(HttpResponseMessage response)
    {
        using var corpo = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return string.Join(
            ',',
            corpo.RootElement.EnumerateObject().Where(campo => campo.Name != "traceId").Select(campo => $"{campo.Name}={campo.Value}"));
    }

    // Conta as chamadas, para provar que toda falha de login paga um Argon2id.
    private sealed class SenhaHasherContado(ISenhaHasher real) : ISenhaHasher
    {
        private int _verificacoes;
        private int _ficticias;

        public int Verificacoes => _verificacoes;

        public int Ficticias => _ficticias;

        public void Zerar() => (_verificacoes, _ficticias) = (0, 0);

        public string Hash(string senha) => real.Hash(senha);

        public bool Verify(string senha, string hash)
        {
            Interlocked.Increment(ref _verificacoes);
            return real.Verify(senha, hash);
        }

        public void VerifyDummy(string senha)
        {
            Interlocked.Increment(ref _ficticias);
            real.VerifyDummy(senha);
        }
    }

    private sealed class RelogioDeTeste : TimeProvider
    {
        private DateTimeOffset _agora = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan quanto) => _agora += quanto;
    }
}
