using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Maxx.Api.Autenticacao;
using Maxx.Api.IntegrationTests.Social;
using Maxx.Api.PremiosParticipacao;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Infrastructure.Autenticacao;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Maxx.Api.IntegrationTests.PremiosParticipacao;

// GET /v1/eventos contra o seed demo de verdade (data/demo/eventos.json) e o calendário de
// 2026. 2026/2 vai de 27/07 a 05/12. O login acontece no relógio de agora, porque o token é
// validado no relógio da máquina; depois o relógio da aplicação vai para o dia do teste.
public sealed class EventosTests(PostgresFixture postgres, Fotos.ArmazenamentoFixture armazenamento)
    : IClassFixture<PostgresFixture>, IClassFixture<Fotos.ArmazenamentoFixture>
{
    private const string _senha = "senha-boa-123";
    private const string _anaDeCc = "ana.souza@al.unieduk.invalid";
    private const string _gabrielDeAdm = "gabriel.martins@al.unieduk.invalid";
    private const string _renataProfessora = "renata.albuquerque@maxx-unimax.com";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // 12h em São Paulo do dia informado.
    private static DateTimeOffset Dia(int mes, int dia) => new(2026, mes, dia, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Aluno_ve_os_eventos_ja_comecados_do_semestre_abertos_ao_curso_do_mais_recente_ao_mais_antigo()
    {
        await using var mundo = await MundoAsync();
        using var ana = await mundo.ComoAsync(_anaDeCc);
        mundo.Em(Dia(11, 8));

        var eventos = await EventosAsync(ana);

        // Fora: os de Medicina, Administração e IA, os que ainda não começaram (feira, mostra) e
        // a iniciação científica, que começou em 2026/1, antes do fim do último semestre encerrado.
        Assert.Equal(
            [
                "maratona-de-programacao-2026",
                "congresso-regional-2026-2",
                "semana-academica-cc-2026-2",
                "workshop-carreira-2026-2",
                "doacao-de-sangue-2026-2",
                "minicurso-python-2026-2",
                "projeto-de-extensao-2026-2",
                "palestra-abertura-2026-2",
                "monitoria-2026-2",
            ],
            await mundo.CodigosAsync(eventos));
        var semana = eventos[2];
        Assert.Equal(
            ("Semana Acadêmica de Ciência da Computação", new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 18)),
            (semana.Nome, semana.Inicio, semana.Fim));
        Assert.Equal(["P06", "P08", "P09"], semana.Itens);
    }

    [Fact]
    public async Task Evento_de_curso_so_aparece_para_o_curso_dele()
    {
        await using var mundo = await MundoAsync();
        using var gabriel = await mundo.ComoAsync(_gabrielDeAdm);
        mundo.Em(Dia(11, 8));

        var codigos = await mundo.CodigosAsync(await EventosAsync(gabriel));

        Assert.Contains("visita-tecnica-adm-2026-2", codigos);
        Assert.DoesNotContain("semana-academica-cc-2026-2", codigos);
    }

    [Fact]
    public async Task Evento_ainda_nao_comecado_fica_de_fora_ate_o_dia_dele()
    {
        await using var mundo = await MundoAsync();
        using var ana = await mundo.ComoAsync(_anaDeCc);

        mundo.Em(Dia(10, 23));
        Assert.DoesNotContain("maratona-de-programacao-2026", await mundo.CodigosAsync(await EventosAsync(ana)));

        mundo.Em(Dia(10, 24));
        Assert.Contains("maratona-de-programacao-2026", await mundo.CodigosAsync(await EventosAsync(ana)));
    }

    [Theory]
    [InlineData("P06", new[] { "semana-academica-cc-2026-2" })]
    [InlineData(" P13 ", new[] { "maratona-de-programacao-2026" })]
    [InlineData("P11", new[] { "projeto-de-extensao-2026-2" })]

    // Apresentação em sala vai sempre por texto livre.
    [InlineData("P17", new string[0])]
    public async Task Com_o_item_so_os_eventos_que_o_aceitam(string item, string[] esperados)
    {
        await using var mundo = await MundoAsync();
        using var ana = await mundo.ComoAsync(_anaDeCc);
        mundo.Em(Dia(11, 8));

        Assert.Equal(esperados, await mundo.CodigosAsync(await EventosAsync(ana, item)));
    }

    [Theory]
    [InlineData("P99")]
    [InlineData("M01")]
    [InlineData("C02")]
    public async Task Item_que_nao_e_premio_de_participacao_e_404(string item)
    {
        await using var mundo = await MundoAsync();
        using var ana = await mundo.ComoAsync(_anaDeCc);
        mundo.Em(Dia(11, 8));

        var resposta = await ana.GetAsync(Caminho(item), Ct);

        await MundoSocial.AssertRecusaAsync(resposta, HttpStatusCode.NotFound, "item-nao-encontrado");
    }

    [Fact]
    public async Task No_recesso_os_eventos_do_semestre_que_acabou_saem_da_lista()
    {
        await using var mundo = await MundoAsync();
        using var ana = await mundo.ComoAsync(_anaDeCc);
        // 2026/2 acabou em 05/12, e todos os eventos semeados começaram dentro dele.
        mundo.Em(Dia(12, 10));

        Assert.Empty(await EventosAsync(ana));
    }

    [Fact]
    public async Task Professor_nao_ve_a_lista_e_sem_login_e_401()
    {
        await using var mundo = await MundoAsync();
        using var renata = await mundo.ComoAsync(_renataProfessora);
        mundo.Em(Dia(11, 8));

        Assert.Equal(HttpStatusCode.Forbidden, (await renata.GetAsync(Caminho(), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await mundo.Factory.CreateClient().GetAsync(Caminho(), Ct)).StatusCode);
    }

    private static Uri Caminho(string? item = null) =>
        new(item is null ? "/api/v1/eventos" : $"/api/v1/eventos?item={Uri.EscapeDataString(item)}", UriKind.Relative);

    private static async Task<List<EventoResponse>> EventosAsync(HttpClient client, string? item = null)
    {
        var resposta = await client.GetAsync(Caminho(item), Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<List<EventoResponse>>(Ct))!;
    }

    // Seed base e demo, e a senha de dois alunos e da professora, que o seed cria sem.
    private async Task<Mundo> MundoAsync()
    {
        var relogio = new RelogioDeTeste();
        var factory = new MaxxApiFactory(
            await postgres.CreateDatabaseAsync(),
            configuracao: armazenamento.Configuracao,
            servicos: colecao => colecao.Replace(ServiceDescriptor.Singleton<TimeProvider>(relogio)));
        await factory.MigrateAsync(Ct);
        await factory.SeedBaseAsync(Ct);
        await factory.SeedDemoAsync(Ct);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var hash = new Argon2idSenhaHasher().Hash(_senha);
        foreach (var email in new[] { _anaDeCc, _gabrielDeAdm, _renataProfessora })
        {
            var conta = await (
                    from usuario in db.Set<Usuario>()
                    join cadastro in db.Set<CadastroElegivel>() on usuario.CadastroElegivelId equals cadastro.Id
                    where cadastro.Email == email
                    select usuario)
                .SingleAsync(Ct);
            conta.DefinirSenha(hash);
        }
        await db.SaveChangesAsync(Ct);

        return new Mundo(factory, relogio);
    }

    private sealed class Mundo(MaxxApiFactory factory, RelogioDeTeste relogio) : IAsyncDisposable
    {
        public MaxxApiFactory Factory => factory;

        public void Em(DateTimeOffset instante) => relogio.Avancar(instante - relogio.GetUtcNow());

        public async Task<HttpClient> ComoAsync(string email)
        {
            var client = factory.CreateClient();
            var resposta = await client.PostAsJsonAsync(
                new Uri("/api/v1/auth/login", UriKind.Relative), new { identificador = email, senha = _senha }, Ct);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var token = (await resposta.Content.ReadFromJsonAsync<SessaoResponse>(Ct))!.AccessToken;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        // O código não vai na resposta (é chave do seed); aqui ele é lido do banco pelo id.
        public async Task<List<string>> CodigosAsync(List<EventoResponse> eventos)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var codigos = await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<Evento>()
                .ToDictionaryAsync(evento => evento.Id, evento => evento.Codigo!, Ct);
            return [.. eventos.Select(evento => codigos[evento.Id])];
        }

        public ValueTask DisposeAsync() => factory.DisposeAsync();
    }
}
