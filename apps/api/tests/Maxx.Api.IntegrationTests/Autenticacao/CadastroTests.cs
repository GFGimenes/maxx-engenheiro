using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Maxx.Api.Autenticacao;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.BancoDeTeste;

namespace Maxx.Api.IntegrationTests.Autenticacao;

// O cadastro do aluno pela API, contra o Postgres real: a conferência de RA e e-mail na
// mesma linha, um RA uma conta, o prefixo único e a sessão que já sai do cadastro.
public sealed class CadastroTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string _senha = "senha-boa-123";

    private static readonly Uri _sugestao = new("/api/v1/auth/cadastro/sugestao", UriKind.Relative);
    private static readonly Uri _disponibilidade = new("/api/v1/auth/cadastro/disponibilidade", UriKind.Relative);
    private static readonly Uri _cadastro = new("/api/v1/auth/cadastro", UriKind.Relative);
    private static readonly CadastroRequest _ana = new("20260101", "ana.souza@al.unieduk.com.br", "Ana", "anaSouza", _senha);
    private static readonly CadastroRequest _bruno = new("20260102", "bruno.lima@al.unieduk.com.br", "Bruno", "brunoLima", _senha);
    private static readonly CadastroRequest _maria = new("20260103", "maria.silva@al.unieduk.com.br", "Maria", "mariaS", _senha);
    private static readonly CadastroRequest _outraMaria = new("20260104", "maria.c.silva@al.unieduk.com.br", "Maria C.", "mariaC", _senha);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sugestao_parte_do_nome_do_cadastro_com_o_sufixo_do_curso()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(_sugestao, new { ra = _ana.Ra, email = _ana.Email }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sugestao = await response.Content.ReadFromJsonAsync<SugestaoResponse>(Ct);
        Assert.Equal(new SugestaoResponse("anaSouza", "CC", "@anaSouza-CC"), sugestao);
    }

    [Fact]
    public async Task Sugestao_desvia_de_prefixo_ocupado_pelo_nome_do_meio()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();
        await CadastrarAsync(client, _maria with { Prefixo = "mariaSilva" });
        await CadastrarAsync(client, _bruno with { Prefixo = "MARIAclaraSILVA" });

        // Outra Maria Clara da Silva, que só sobra com o número.
        var response = await client.PostAsJsonAsync(_sugestao, new { ra = _outraMaria.Ra, email = _outraMaria.Email }, Ct);

        var sugestao = await response.Content.ReadFromJsonAsync<SugestaoResponse>(Ct);
        Assert.Equal("mariaSilva2", sugestao!.Prefixo);
    }

    [Fact]
    public async Task Ra_e_email_de_linhas_diferentes_tem_a_mesma_resposta_que_um_ra_inexistente()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        var trocado = await client.PostAsJsonAsync(_sugestao, new { ra = _ana.Ra, email = _bruno.Email }, Ct);
        var inexistente = await client.PostAsJsonAsync(_sugestao, new { ra = "99999999", email = "ninguem@al.unieduk.com.br" }, Ct);

        await AssertRecusaAsync(trocado, HttpStatusCode.NotFound, "cadastro-nao-encontrado");
        await AssertRecusaAsync(inexistente, HttpStatusCode.NotFound, "cadastro-nao-encontrado");
        Assert.Equal(await CorpoSemTraceAsync(trocado), await CorpoSemTraceAsync(inexistente));
    }

    [Fact]
    public async Task Email_em_outra_caixa_ainda_e_a_mesma_linha()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(_sugestao, new { ra = _ana.Ra, email = " ANA.Souza@AL.unieduk.com.br " }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Professor_nao_se_cadastra_pelo_app()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            _cadastro, _ana with { Ra = "PROF-1", Email = "renata@prof.unieduk.com.br", Prefixo = "renataA" }, Ct);

        await AssertRecusaAsync(response, HttpStatusCode.NotFound, "cadastro-nao-encontrado");
    }

    [Fact]
    public async Task Disponibilidade_responde_pelo_prefixo_sem_diferenciar_maiusculas()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();
        await CadastrarAsync(client, _ana);

        Assert.True(await DisponivelAsync(client, "brunoLima"));
        Assert.False(await DisponivelAsync(client, "ANASOUZA"));
    }

    [Fact]
    public async Task Disponibilidade_com_prefixo_fora_do_formato_e_recusada()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            _disponibilidade, new { ra = _bruno.Ra, email = _bruno.Email, prefixo = "bruno-lima" }, Ct);

        await AssertRecusaAsync(response, HttpStatusCode.BadRequest, "prefixo-invalido");
    }

    [Fact]
    public async Task Prefixo_que_o_dono_deixou_continua_indisponivel()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();
        await CadastrarAsync(client, _ana);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
            var ana = await db.Set<Usuario>().Include(usuario => usuario.PrefixosReservados).SingleAsync(Ct);
            ana.EditarPrefixo("aninha", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(Ct);
        }

        Assert.False(await DisponivelAsync(client, "anaSouza"));
        await AssertRecusaAsync(
            await client.PostAsJsonAsync(_cadastro, _bruno with { Prefixo = "anaSouza" }, Ct),
            HttpStatusCode.Conflict,
            "prefixo-indisponivel");
    }

    [Fact]
    public async Task Cadastro_no_nativo_devolve_access_e_refresh_no_corpo()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(_cadastro, _ana, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        var sessao = (await response.Content.ReadFromJsonAsync<SessaoResponse>(Ct))!;
        Assert.False(string.IsNullOrEmpty(sessao.RefreshToken));
        Assert.InRange(sessao.AccessTokenExpiraEm - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));
        Assert.InRange(sessao.RefreshTokenExpiraEm - DateTimeOffset.UtcNow, TimeSpan.FromDays(29.9), TimeSpan.FromDays(30));

        // O access token do cadastro já abre a API.
        using var eu = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/eu", UriKind.Relative));
        eu.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        var euResponse = await client.SendAsync(eu, Ct);
        Assert.Equal(["aluno"], (await euResponse.Content.ReadFromJsonAsync<EuResponse>(Ct))!.Papeis);

        // O banco guarda hash, nunca o valor: nem a senha, nem o refresh.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var usuario = await db.Set<Usuario>().SingleAsync(Ct);
        Assert.Equal("@anaSouza-CC", usuario.Arroba.ToString());
        Assert.Equal("Ana", usuario.NomeDeExibicao);
        Assert.StartsWith("$argon2id$", usuario.HashDaSenha, StringComparison.Ordinal);
        Assert.DoesNotContain(_senha, usuario.HashDaSenha!, StringComparison.Ordinal);
        var familia = await db.Set<FamiliaDeSessao>().Include(familia => familia.Tokens).SingleAsync(Ct);
        Assert.Equal(usuario.Id, familia.UsuarioId);
        Assert.Equal(Sha256(sessao.RefreshToken!), familia.Tokens.Single().Hash);
    }

    [Fact]
    public async Task Cadastro_na_web_poe_o_refresh_em_cookie_httponly_e_nao_no_corpo()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();
        using var pedido = new HttpRequestMessage(HttpMethod.Post, _cadastro) { Content = JsonContent.Create(_ana) };
        pedido.Headers.Add("X-Maxx-Cliente", "web");

        var response = await client.SendAsync(pedido, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var sessao = (await response.Content.ReadFromJsonAsync<SessaoResponse>(Ct))!;
        Assert.Null(sessao.RefreshToken);
        Assert.False(string.IsNullOrEmpty(sessao.AccessToken));

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("maxx_refresh=", cookie, StringComparison.Ordinal);
        var atributos = cookie.Split(';', StringSplitOptions.TrimEntries).Select(atributo => atributo.ToLowerInvariant()).ToList();
        Assert.Contains("httponly", atributos);
        Assert.Contains("secure", atributos);
        Assert.Contains("samesite=strict", atributos);
        Assert.Contains("path=/api/v1/auth", atributos);
        Assert.Contains(atributos, atributo => atributo.StartsWith("expires=", StringComparison.Ordinal));

        // O valor do cookie é o refresh da família gravada.
        var valor = cookie.Split(';')[0]["maxx_refresh=".Length..];
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        Assert.Equal(Sha256(Uri.UnescapeDataString(valor)), (await db.Set<RefreshToken>().SingleAsync(Ct)).Hash);
    }

    [Fact]
    public async Task Segunda_conta_para_o_mesmo_ra_leva_a_conta_existente()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();
        await CadastrarAsync(client, _ana);

        await AssertRecusaAsync(
            await client.PostAsJsonAsync(_cadastro, _ana with { Prefixo = "outraAna" }, Ct),
            HttpStatusCode.Conflict,
            "conta-existente");
        await AssertRecusaAsync(
            await client.PostAsJsonAsync(_sugestao, new { ra = _ana.Ra, email = _ana.Email }, Ct),
            HttpStatusCode.Conflict,
            "conta-existente");
    }

    [Fact]
    public async Task Prefixo_ocupado_por_outro_e_recusado_sem_diferenciar_maiusculas()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();
        await CadastrarAsync(client, _ana);

        await AssertRecusaAsync(
            await client.PostAsJsonAsync(_cadastro, _bruno with { Prefixo = "AnaSouza" }, Ct),
            HttpStatusCode.Conflict,
            "prefixo-indisponivel");
    }

    [Theory]
    [InlineData("curta", "anaSouza", "Ana", "senha-fora-da-politica")]
    [InlineData(_senha, "ana-souza", "Ana", "prefixo-invalido")]
    [InlineData(_senha, "an", "Ana", "prefixo-invalido")]
    [InlineData(_senha, "anaSouza", "   ", "nome-de-exibicao-invalido")]
    public async Task Regra_violada_volta_com_o_codigo_dela(string senha, string prefixo, string nome, string codigo)
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            _cadastro, _ana with { Senha = senha, Prefixo = prefixo, NomeDeExibicao = nome }, Ct);

        await AssertRecusaAsync(response, HttpStatusCode.BadRequest, codigo);
    }

    [Fact]
    public async Task Nome_de_exibicao_com_mais_de_60_caracteres_e_recusado()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(_cadastro, _ana with { NomeDeExibicao = new string('a', 61) }, Ct);

        await AssertRecusaAsync(response, HttpStatusCode.BadRequest, "nome-de-exibicao-invalido");
    }

    [Fact]
    public async Task Campo_ausente_e_recusado_na_borda_com_dados_invalidos()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(_cadastro, new { ra = _ana.Ra, email = _ana.Email }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var corpo = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal("dados-invalidos", corpo.RootElement.GetProperty("codigo").GetString());
        Assert.True(corpo.RootElement.GetProperty("errors").TryGetProperty("Senha", out _));
    }

    [Fact]
    public async Task Recusa_nunca_ecoa_a_senha()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();
        await CadastrarAsync(client, _ana);

        var response = await client.PostAsJsonAsync(_cadastro, _ana with { Senha = "segredo-que-nao-volta" }, Ct);

        Assert.DoesNotContain("segredo-que-nao-volta", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dois_cadastros_simultaneos_pelo_mesmo_prefixo_deixam_um_201_e_um_409()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        var respostas = await Task.WhenAll(
            client.PostAsJsonAsync(_cadastro, _ana with { Prefixo = "disputado" }, Ct),
            client.PostAsJsonAsync(_cadastro, _bruno with { Prefixo = "disputado" }, Ct));

        Assert.Single(respostas, response => response.StatusCode == HttpStatusCode.Created);
        var perdedor = Assert.Single(respostas, response => response.StatusCode != HttpStatusCode.Created);
        await AssertRecusaAsync(perdedor, HttpStatusCode.Conflict, "prefixo-indisponivel");
    }

    [Fact]
    public async Task Dois_cadastros_simultaneos_do_mesmo_ra_deixam_um_201_e_um_409()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        var respostas = await Task.WhenAll(
            client.PostAsJsonAsync(_cadastro, _ana with { Prefixo = "anaUm" }, Ct),
            client.PostAsJsonAsync(_cadastro, _ana with { Prefixo = "anaDois" }, Ct));

        Assert.Single(respostas, response => response.StatusCode == HttpStatusCode.Created);
        var perdedor = Assert.Single(respostas, response => response.StatusCode != HttpStatusCode.Created);
        await AssertRecusaAsync(perdedor, HttpStatusCode.Conflict, "conta-existente");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        Assert.Equal(1, await db.Set<Usuario>().CountAsync(Ct));
        Assert.Equal(1, await db.Set<FamiliaDeSessao>().CountAsync(Ct));
    }


    private async Task<MaxxApiFactory> MundoAsync()
    {
        var factory = await MigradoAsync(postgres);
        var cc = new Curso("Ciência da Computação", "CC", 8);
        await SalvarAsync(
            factory,
            cc,
            CadastroElegivel.Aluno(_ana.Ra, _ana.Email, "Ana Souza", cc, 3),
            CadastroElegivel.Aluno(_bruno.Ra, _bruno.Email, "Bruno Lima", cc, 3),
            CadastroElegivel.Aluno(_maria.Ra, _maria.Email, "Maria Clara Fernandes da Silva", cc, 1),
            CadastroElegivel.Aluno(_outraMaria.Ra, _outraMaria.Email, "Maria Clara da Silva", cc, 2),
            CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque", ra: "PROF-1"));
        return factory;
    }

    private static async Task CadastrarAsync(HttpClient client, CadastroRequest pedido)
    {
        var response = await client.PostAsJsonAsync(_cadastro, pedido, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<bool> DisponivelAsync(HttpClient client, string prefixo)
    {
        var response = await client.PostAsJsonAsync(_disponibilidade, new { ra = _bruno.Ra, email = _bruno.Email, prefixo }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DisponibilidadeResponse>(Ct))!.Disponivel;
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

    private static string Sha256(string valor) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));
}
