using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Maxx.Api.Autenticacao;
using Maxx.Application.Autorizacao;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Infrastructure.Autenticacao;
using Maxx.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.BancoDeTeste;

namespace Maxx.Api.IntegrationTests.Autenticacao;

// Papéis e escopo de turma, contra o Postgres real. Ainda não há rota de professor nem de
// instituição no produto (passos 6 e 12): as deste teste existem só no host de teste e
// usam as policies e o IEscopoDeTurma de verdade.
public sealed class AutorizacaoTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string _senha = "senha-boa-123";
    private const string _ana = "ana.souza@al.unieduk.com.br";
    private const string _renata = "renata@prof.unieduk.com.br";
    private const string _secretaria = "secretaria@unieduk.com.br";
    private const string _coordenacao = "coordenacao@prof.unieduk.com.br";

    private static readonly Uri _login = new("/api/v1/auth/login", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Hoje => FusoHorario.DataLocal(DateTimeOffset.UtcNow);

    [Fact]
    public async Task Professor_com_vinculo_na_turma_do_semestre_vigente_tem_escopo()
    {
        var mundo = await MundoAsync(Hoje);
        await using var factory = mundo.Factory;
        using var client = factory.CreateClient();
        var token = await EntrarAsync(client, _renata);

        Assert.Equal(HttpStatusCode.OK, (await GetAsync(client, $"/teste/turmas/{mundo.TurmaVigente}", token)).StatusCode);
    }

    [Fact]
    public async Task Turma_de_outro_semestre_ou_sem_vinculo_nao_da_escopo()
    {
        var mundo = await MundoAsync(Hoje);
        await using var factory = mundo.Factory;
        using var client = factory.CreateClient();
        var token = await EntrarAsync(client, _renata);

        // Vinculada, mas a turma é do ano passado, ou do semestre que ainda não começou.
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync(client, $"/teste/turmas/{mundo.TurmaPassada}", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync(client, $"/teste/turmas/{mundo.TurmaFutura}", token)).StatusCode);

        // Turma vigente, mas de outro professor.
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync(client, $"/teste/turmas/{mundo.TurmaDeOutro}", token)).StatusCode);
    }

    [Fact]
    public async Task Vinculo_removido_vale_no_pedido_seguinte_com_o_mesmo_token()
    {
        var mundo = await MundoAsync(Hoje);
        await using var factory = mundo.Factory;
        using var client = factory.CreateClient();
        var token = await EntrarAsync(client, _renata);
        var rota = $"/teste/turmas/{mundo.TurmaVigente}";
        Assert.Equal(HttpStatusCode.OK, (await GetAsync(client, rota, token)).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<VinculoProfessor>()
                .Where(vinculo => vinculo.TurmaId == mundo.TurmaVigente)
                .ExecuteDeleteAsync(Ct);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync(client, rota, token)).StatusCode);
    }

    [Fact]
    public async Task Semestre_vigente_e_o_de_hoje_em_sao_paulo_e_nao_em_utc()
    {
        // O semestre vigente termina em `fim`. 23h30 em São Paulo já é o dia seguinte em UTC.
        var hoje = new DateOnly(2026, 10, 15);
        var fim = FimDoSemestreVigente(hoje);
        var relogio = new RelogioDeTeste(NoFuso(fim, 23, 30));
        var mundo = await MundoAsync(hoje, relogio);
        await using var factory = mundo.Factory;
        Assert.Equal(fim.AddDays(1), DateOnly.FromDateTime(relogio.GetUtcNow().UtcDateTime));

        Assert.True(await LecionaAsync(factory, mundo.Renata, mundo.TurmaVigente));

        relogio.Agora = NoFuso(fim.AddDays(1), 0, 30);
        Assert.False(await LecionaAsync(factory, mundo.Renata, mundo.TurmaVigente));
    }

    [Fact]
    public async Task O_token_do_professor_nao_carrega_turma_nenhuma()
    {
        var mundo = await MundoAsync(Hoje);
        await using var factory = mundo.Factory;
        using var client = factory.CreateClient();

        var token = await EntrarAsync(client, _renata);

        var payload = token.Split('.')[1];
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(
            payload.Replace('-', '+').Replace('_', '/').PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=')));
        using var claims = JsonDocument.Parse(json);
        Assert.Equal(
            ["aud", "exp", "iat", "iss", "nbf", "papel", "sub"],
            claims.RootElement.EnumerateObject().Select(claim => claim.Name).Order(StringComparer.Ordinal));
        Assert.Equal("professor", claims.RootElement.GetProperty("papel")[0].GetString());
    }

    [Theory]
    [InlineData(_ana, HttpStatusCode.Forbidden)]
    [InlineData(_renata, HttpStatusCode.Forbidden)]
    [InlineData(_secretaria, HttpStatusCode.OK)]
    [InlineData(_coordenacao, HttpStatusCode.OK)]
    public async Task Rota_de_instituicao_so_abre_para_quem_tem_o_papel(string email, HttpStatusCode esperado)
    {
        var mundo = await MundoAsync(Hoje);
        await using var factory = mundo.Factory;
        using var client = factory.CreateClient();

        var response = await GetAsync(client, "/teste/instituicao", await EntrarAsync(client, email));

        Assert.Equal(esperado, response.StatusCode);
    }

    [Fact]
    public async Task Aluno_nao_passa_pela_policy_de_professor()
    {
        var mundo = await MundoAsync(Hoje);
        await using var factory = mundo.Factory;
        using var client = factory.CreateClient();

        var response = await GetAsync(client, $"/teste/turmas/{mundo.TurmaVigente}", await EntrarAsync(client, _ana));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Toda_rota_de_v1_exige_login_salvo_as_de_entrada_na_conta()
    {
        await using var factory = new MaxxApiFactory(MaxxApiFactory.UnreachableDatabase);
        var rotas = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(rota => rota.RoutePattern.RawText?.StartsWith("/api/v1", StringComparison.Ordinal) == true)
            .ToList();

        Assert.All(rotas, rota => Assert.NotEmpty(rota.Metadata.GetOrderedMetadata<IAuthorizeData>()));

        // Rota pública nova entra nesta lista de propósito, nunca por esquecimento.
        Assert.Equal(
            [
                "/api/v1/auth/cadastro",
                "/api/v1/auth/cadastro/disponibilidade",
                "/api/v1/auth/cadastro/sugestao",
                "/api/v1/auth/login",
                "/api/v1/auth/logout",
                "/api/v1/auth/refresh",
            ],
            rotas.Where(rota => rota.Metadata.GetMetadata<IAllowAnonymous>() is not null)
                .Select(rota => rota.RoutePattern.RawText!.TrimEnd('/'))
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Rota_inexistente_continua_404_sem_token()
    {
        await using var factory = new MaxxApiFactory(MaxxApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(new Uri("/api/v1/nada-aqui", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(new Uri("/api/v1/eu", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task Eu_devolve_papeis_e_arroba_do_banco()
    {
        var mundo = await MundoAsync(Hoje);
        await using var factory = mundo.Factory;
        using var client = factory.CreateClient();

        using var pedido = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/eu", UriKind.Relative));
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await EntrarAsync(client, _coordenacao));
        var eu = (await (await client.SendAsync(pedido, Ct)).Content.ReadFromJsonAsync<EuResponse>(Ct))!;

        Assert.Equal(["professor", "instituicao"], eu.Papeis);
        Assert.Equal("@coordenacao-PROF", eu.Arroba);
    }

    private async Task<Mundo> MundoAsync(DateOnly hoje, TimeProvider? relogio = null)
    {
        var factory = new MaxxApiFactory(
            await postgres.CreateDatabaseAsync(),
            servicos: servicos =>
            {
                servicos.AddSingleton<IStartupFilter, RotasDeTeste>();
                if (relogio is not null)
                {
                    servicos.AddSingleton(relogio);
                }
            });
        await factory.MigrateAsync(Ct);

        // Ano anterior inteiro no passado; no ano de agora, o semestre 1 contém hoje e o 2
        // ainda não começou.
        var anterior = Ano(hoje.Year - 1, hoje.AddDays(-400));
        var atual = Ano(hoje.Year, hoje.AddDays(-75));
        var vigente = atual.Semestres[0];

        var cc = new Curso("Ciência da Computação", "CC", 8);
        var bd = new Materia(cc, "Banco de Dados");
        var redes = new Materia(cc, "Redes");
        var turmaVigente = new Turma(bd, vigente);
        var turmaPassada = new Turma(bd, anterior.Semestres[1]);
        var turmaFutura = new Turma(bd, atual.Semestres[1]);
        var turmaDeOutro = new Turma(redes, vigente);

        var hash = new Argon2idSenhaHasher().Hash(_senha);
        var ana = CadastroElegivel.Aluno("20260101", _ana, "Ana Souza", cc, 3);
        var renata = CadastroElegivel.Professor(_renata, "Renata Albuquerque");
        var paulo = CadastroElegivel.Professor("paulo@prof.unieduk.com.br", "Paulo Mendes");
        var secretaria = CadastroElegivel.Instituicao(_secretaria, "Secretaria UniMax");
        var coordenacao = CadastroElegivel.Professor(_coordenacao, "Coordenação", tambemInstituicao: true);
        var contaDaRenata = ComSenha(Usuario.CriarPeloSeed(renata, "renataA", DateTimeOffset.UtcNow), hash);

        await SalvarAsync(
            factory,
            anterior,
            atual,
            cc,
            bd,
            redes,
            turmaVigente,
            turmaPassada,
            turmaFutura,
            turmaDeOutro,
            ana,
            renata,
            paulo,
            secretaria,
            coordenacao,
            new VinculoProfessor(renata, turmaVigente),
            new VinculoProfessor(renata, turmaPassada),
            new VinculoProfessor(renata, turmaFutura),
            new VinculoProfessor(paulo, turmaDeOutro),
            Usuario.CadastrarAluno(ana, cc, "Ana", "anaSouza", hash, DateTimeOffset.UtcNow),
            contaDaRenata,
            ComSenha(Usuario.CriarPeloSeed(secretaria, "secretaria", DateTimeOffset.UtcNow), hash),
            ComSenha(Usuario.CriarPeloSeed(coordenacao, "coordenacao", DateTimeOffset.UtcNow), hash));

        return new Mundo(factory, contaDaRenata.Id, turmaVigente.Id, turmaPassada.Id, turmaFutura.Id, turmaDeOutro.Id);
    }

    // Quatro bimestres seguidos de 50 dias a partir de `inicio`, com um dia de folga entre eles.
    private static AnoLetivo Ano(int ano, DateOnly inicio)
    {
        Bimestre Bimestre(int numero) =>
            new(numero, inicio.AddDays((numero - 1) * 51), inicio.AddDays(((numero - 1) * 51) + 49));

        return new AnoLetivo(
            ano,
            new SemestreLetivo(ano, 1, Bimestre(1), Bimestre(2)),
            new SemestreLetivo(ano, 2, Bimestre(3), Bimestre(4)));
    }

    // O mesmo cálculo de Ano: fim do bimestre 2 do ano de `hoje`.
    private static DateOnly FimDoSemestreVigente(DateOnly hoje) => hoje.AddDays(-75 + 51 + 49);

    private static DateTimeOffset NoFuso(DateOnly data, int hora, int minuto)
    {
        var local = data.ToDateTime(new TimeOnly(hora, minuto));
        return new DateTimeOffset(local, FusoHorario.SaoPaulo.GetUtcOffset(local));
    }

    private static Usuario ComSenha(Usuario usuario, string hash)
    {
        usuario.DefinirSenha(hash);
        return usuario;
    }

    private static async Task<bool> LecionaAsync(MaxxApiFactory factory, Guid usuarioId, Guid turmaId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IEscopoDeTurma>().ProfessorLecionaAsync(usuarioId, turmaId, Ct);
    }

    private static async Task<string> EntrarAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync(_login, new { identificador = email, senha = _senha }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SessaoResponse>(Ct))!.AccessToken;
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string rota, string token)
    {
        var pedido = new HttpRequestMessage(HttpMethod.Get, new Uri(rota, UriKind.Relative));
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.SendAsync(pedido, Ct);
    }

    private sealed record Mundo(
        MaxxApiFactory Factory, Guid Renata, Guid TurmaVigente, Guid TurmaPassada, Guid TurmaFutura, Guid TurmaDeOutro);

    // As rotas que o produto ainda não tem, num ramo próprio do pipeline, com as policies
    // e o IEscopoDeTurma registrados pela aplicação.
    private sealed class RotasDeTeste : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Map("/teste", teste =>
            {
                teste.UseRouting();
                teste.UseAuthentication();
                teste.UseAuthorization();
                teste.UseEndpoints(rotas =>
                {
                    rotas.MapGet("/instituicao", () => "ok").RequireAuthorization(Politicas.Instituicao);
                    rotas.MapGet(
                            "/turmas/{turmaId:guid}",
                            async (Guid turmaId, ClaimsPrincipal usuario, IEscopoDeTurma escopo, CancellationToken cancellationToken) =>
                                await escopo.ProfessorLecionaAsync(
                                    Guid.Parse(usuario.FindFirstValue(MaxxClaims.Sub)!), turmaId, cancellationToken)
                                    ? Results.Ok()
                                    : Results.Forbid())
                        .RequireAuthorization(Politicas.Professor);
                });
            });

            next(app);
        };
    }

    private sealed class RelogioDeTeste(DateTimeOffset agora) : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = agora;

        public override DateTimeOffset GetUtcNow() => Agora;
    }
}
