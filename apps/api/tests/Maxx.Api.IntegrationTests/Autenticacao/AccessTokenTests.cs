using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Maxx.Api.Autenticacao;
using Maxx.Application.Autenticacao;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Maxx.Api.IntegrationTests.Autenticacao;

// O access token de ponta a ponta, sem banco: emitido pela API, conferido pela API, e
// recusado em 401 no formato único quando vencido, mal assinado ou ausente.
public class AccessTokenTests
{
    private static readonly Uri _eu = new("/api/v1/eu", UriKind.Relative);

    private static readonly string[] _soInstituicao = ["instituicao"];

    private static readonly string[] _soAluno = ["aluno"];

    private static readonly string _chave = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Token_emitido_pela_api_abre_o_eu_com_id_e_papeis()
    {
        await using var factory = ComChave(_chave);
        var renata = Professora(tambemInstituicao: true);
        var token = Emitir(factory, renata);
        using var client = factory.CreateClient();

        var response = await client.SendAsync(Get(token.Valor), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var eu = await response.Content.ReadFromJsonAsync<EuResponse>(Ct);
        Assert.Equal(renata.Id, eu!.Id);
        Assert.Equal(["professor", "instituicao"], eu.Papeis);
    }

    [Fact]
    public async Task Token_tem_15_minutos_e_so_sub_e_papel_alem_das_claims_padrao()
    {
        await using var factory = ComChave(_chave);
        var token = Emitir(factory, Professora(tambemInstituicao: false));

        var jwt = new JsonWebToken(token.Valor);

        Assert.Equal(TimeSpan.FromMinutes(15), jwt.ValidTo - jwt.IssuedAt);
        Assert.Equal("HS256", jwt.Alg);
        Assert.Equal(
            new HashSet<string> { "sub", "papel", "iss", "aud", "iat", "nbf", "exp" },
            jwt.Claims.Select(claim => claim.Type).ToHashSet());
        Assert.Equal(["professor"], jwt.Claims.Where(claim => claim.Type == "papel").Select(claim => claim.Value));
    }

    [Fact]
    public async Task Sem_token_responde_401_em_ProblemDetails()
    {
        await using var factory = ComChave(_chave);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(_eu, Ct);

        await AssertRecusadoAsync(response);
    }

    [Fact]
    public async Task Token_vencido_responde_401()
    {
        await using var factory = ComChave(_chave);
        using var client = factory.CreateClient();

        // Venceu há um minuto: além da tolerância de 30 segundos.
        var vencido = Assinar(_chave, DateTime.UtcNow.AddMinutes(-16), DateTime.UtcNow.AddMinutes(-1));

        await AssertRecusadoAsync(await client.SendAsync(Get(vencido), Ct));
    }

    [Fact]
    public async Task Token_assinado_com_outra_chave_responde_401()
    {
        await using var factory = ComChave(_chave);
        using var client = factory.CreateClient();

        var forjado = Assinar(Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)), DateTime.UtcNow, DateTime.UtcNow.AddMinutes(15));

        await AssertRecusadoAsync(await client.SendAsync(Get(forjado), Ct));
    }

    [Fact]
    public async Task Token_sem_assinatura_responde_401()
    {
        await using var factory = ComChave(_chave);
        using var client = factory.CreateClient();

        var semAssinatura = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = JwtOptions.Emissor,
            Audience = JwtOptions.Audiencia,
            Claims = new Dictionary<string, object> { ["sub"] = Guid.NewGuid().ToString(), ["papel"] = _soInstituicao },
            Expires = DateTime.UtcNow.AddMinutes(15),
        });

        await AssertRecusadoAsync(await client.SendAsync(Get(semAssinatura), Ct));
    }

    [Fact]
    public async Task Token_de_outra_audiencia_responde_401()
    {
        await using var factory = ComChave(_chave);
        using var client = factory.CreateClient();

        var outraAudiencia = Assinar(_chave, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(15), audiencia: "outro-sistema");

        await AssertRecusadoAsync(await client.SendAsync(Get(outraAudiencia), Ct));
    }

    [Fact]
    public async Task Fora_de_desenvolvimento_sem_chave_a_api_nao_sobe()
    {
        await using var factory = new MaxxApiFactory(MaxxApiFactory.UnreachableDatabase, "Production");

        var falha = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Jwt__Chave não configurada", falha.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fora_de_desenvolvimento_com_chave_curta_a_api_nao_sobe()
    {
        await using var factory = ComChave(Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)), "Production");

        var falha = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Jwt__Chave inválida", falha.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fora_de_desenvolvimento_com_chave_valida_a_api_sobe()
    {
        await using var factory = ComChave(_chave, "Production");
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/health", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Em_desenvolvimento_sem_chave_a_api_sobe_com_chave_do_processo()
    {
        await using var factory = new MaxxApiFactory(MaxxApiFactory.UnreachableDatabase, "Development");
        var token = Emitir(factory, Professora(tambemInstituicao: false));
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Get(token.Valor), Ct)).StatusCode);
    }

    private static MaxxApiFactory ComChave(string chave, string ambiente = "Development") =>
        new(MaxxApiFactory.UnreachableDatabase, ambiente, configuracao: new Dictionary<string, string> { ["Jwt:Chave"] = chave });

    private static AccessToken Emitir(MaxxApiFactory factory, Usuario usuario) =>
        factory.Services.GetRequiredService<IAccessTokenIssuer>().Issue(usuario);

    private static Usuario Professora(bool tambemInstituicao) =>
        Usuario.CriarPeloSeed(
            CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque", tambemInstituicao),
            "renataAlbuquerque",
            DateTimeOffset.UtcNow);

    private static HttpRequestMessage Get(string token) =>
        new(HttpMethod.Get, _eu) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) } };

    private static string Assinar(string chave, DateTime emitidoEm, DateTime expiraEm, string audiencia = JwtOptions.Audiencia) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = JwtOptions.Emissor,
            Audience = audiencia,
            Claims = new Dictionary<string, object> { ["sub"] = Guid.NewGuid().ToString(), ["papel"] = _soAluno },
            IssuedAt = emitidoEm,
            NotBefore = emitidoEm,
            Expires = expiraEm,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Convert.FromBase64String(chave)), SecurityAlgorithms.HmacSha256),
        });

    private static async Task AssertRecusadoAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var corpo = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(401, corpo.RootElement.GetProperty("status").GetInt32());
    }
}
