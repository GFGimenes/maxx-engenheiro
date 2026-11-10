using System.Net;
using System.Net.Http.Json;
using Maxx.Api.Health;
using Maxx.Application.Fila;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.BancoDeTeste;

namespace Maxx.Api.IntegrationTests.Fila;

// GET /api/health/worker (ADR 0005, passo 13): o monitor de fora pergunta a cada 5 minutos,
// e o 503 é o alerta. Worker parado, fila parada e trabalho falho, cada um com o seu código.
public sealed class SaudeDoWorkerTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly Uri _rota = new("/api/health/worker", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Worker_que_nunca_bateu_responde_503_worker_parado()
    {
        await using var factory = await MigradoAsync(postgres);

        var (status, saude) = await PerguntarAsync(factory);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal("problema", saude.Status);
        Assert.Equal([ProblemasDoWorker.WorkerParado], saude.Problemas);
        Assert.Null(saude.SegundosDesdeOBatimento);
    }

    [Fact]
    public async Task Batimento_recente_e_fila_vazia_responde_200()
    {
        await using var factory = await MigradoAsync(postgres);
        await factory.Services.GetRequiredService<RegistroDeBatimento>().BaterSeDevidoAsync(Ct);

        var (status, saude) = await PerguntarAsync(factory);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("ok", saude.Status);
        Assert.Empty(saude.Problemas);
        Assert.Equal(0, saude.Disponiveis);
        Assert.Null(saude.SegundosDeEsperaNaFila);
    }

    [Fact]
    public async Task Batimento_de_tres_minutos_atras_e_worker_parado()
    {
        await using var factory = await MigradoAsync(postgres);
        await SalvarAsync(factory, new BatimentoDoWorker { BatidoEm = DateTimeOffset.UtcNow.AddMinutes(-3) });

        var (status, saude) = await PerguntarAsync(factory);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal([ProblemasDoWorker.WorkerParado], saude.Problemas);
        Assert.InRange(saude.SegundosDesdeOBatimento!.Value, 179, 240);
    }

    [Fact]
    public async Task Trabalho_disponivel_esperando_dez_minutos_e_fila_parada()
    {
        await using var factory = await MigradoAsync(postgres);
        await factory.Services.GetRequiredService<RegistroDeBatimento>().BaterSeDevidoAsync(Ct);
        var dezMinutos = DateTimeOffset.UtcNow.AddMinutes(-10);
        await SalvarAsync(factory, Trabalho(EstadoDoTrabalho.Pendente, criadoEm: dezMinutos, disponivelEm: dezMinutos));

        var (status, saude) = await PerguntarAsync(factory);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal([ProblemasDoWorker.FilaParada], saude.Problemas);
        Assert.Equal(1, saude.Disponiveis);
        Assert.InRange(saude.SegundosDeEsperaNaFila!.Value, 599, 660);
    }

    // O seed enfileira trabalhos com datas passadas: o que acabou de chegar não está parado.
    [Fact]
    public async Task Trabalho_com_data_passada_que_acabou_de_chegar_nao_e_fila_parada()
    {
        await using var factory = await MigradoAsync(postgres);
        await factory.Services.GetRequiredService<RegistroDeBatimento>().BaterSeDevidoAsync(Ct);
        await SalvarAsync(
            factory,
            Trabalho(EstadoDoTrabalho.Pendente, criadoEm: DateTimeOffset.UtcNow, disponivelEm: DateTimeOffset.UtcNow.AddDays(-30)));

        var (status, saude) = await PerguntarAsync(factory);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(1, saude.Disponiveis);
    }

    // O instante da falha fica em disponivel_em (recuo zero na última tentativa).
    [Fact]
    public async Task Trabalho_falho_nas_ultimas_24_horas_e_alerta_e_o_mais_antigo_nao()
    {
        await using var factory = await MigradoAsync(postgres);
        await factory.Services.GetRequiredService<RegistroDeBatimento>().BaterSeDevidoAsync(Ct);
        var antigo = DateTimeOffset.UtcNow.AddDays(-2);
        await SalvarAsync(factory, Trabalho(EstadoDoTrabalho.Falho, criadoEm: antigo, disponivelEm: antigo));

        Assert.Equal(HttpStatusCode.OK, (await PerguntarAsync(factory)).Status);

        var recente = DateTimeOffset.UtcNow.AddHours(-1);
        await SalvarAsync(factory, Trabalho(EstadoDoTrabalho.Falho, criadoEm: recente, disponivelEm: recente));
        var (status, saude) = await PerguntarAsync(factory);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal([ProblemasDoWorker.TrabalhoFalho], saude.Problemas);
        Assert.Equal(1, saude.FalhosEm24h);
    }

    // Prova reversa do laço: sem a chamada ao batimento no ServicoDoWorker, a linha nunca
    // aparece e o teste falha.
    [Fact]
    public async Task O_laco_do_worker_grava_o_batimento_e_a_saude_fica_ok()
    {
        await using var factory = new MaxxApiFactory(await postgres.CreateDatabaseAsync(), servicos: services =>
        {
            services.Configure<FilaOptions>(options => options.IntervaloOcioso = TimeSpan.FromMilliseconds(50));
            services.AddWorker();
        });
        await factory.MigrateAsync(Ct);

        var batidoEm = await EsperarBatimentoAsync(factory);

        Assert.InRange(DateTimeOffset.UtcNow - batidoEm, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.OK, (await PerguntarAsync(factory)).Status);
    }

    [Fact]
    public async Task O_batimento_grava_no_maximo_uma_vez_por_intervalo()
    {
        await using var factory = await MigradoAsync(postgres);
        var registro = factory.Services.GetRequiredService<RegistroDeBatimento>();

        await registro.BaterSeDevidoAsync(Ct);
        var primeiro = await BatimentoAsync(factory);
        await registro.BaterSeDevidoAsync(Ct);

        Assert.Equal(primeiro, await BatimentoAsync(factory));
    }

    private static async Task<(HttpStatusCode Status, SaudeDoWorkerResponse Saude)> PerguntarAsync(MaxxApiFactory factory)
    {
        using var client = factory.CreateClient();
        var resposta = await client.GetAsync(_rota, Ct);
        return (resposta.StatusCode, (await resposta.Content.ReadFromJsonAsync<SaudeDoWorkerResponse>(Ct))!);
    }

    private static Trabalho Trabalho(EstadoDoTrabalho estado, DateTimeOffset criadoEm, DateTimeOffset disponivelEm) => new()
    {
        Tipo = "teste",
        Carga = "{}",
        Estado = estado,
        Tentativas = estado == EstadoDoTrabalho.Falho ? PoliticaDeRetentativa.MaximoDeTentativas : 0,
        CriadoEm = criadoEm,
        DisponivelEm = disponivelEm,
    };

    private static async Task<DateTimeOffset?> BatimentoAsync(MaxxApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        return (await db.Set<BatimentoDoWorker>().AsNoTracking().SingleOrDefaultAsync(Ct))?.BatidoEm;
    }

    private static async Task<DateTimeOffset> EsperarBatimentoAsync(MaxxApiFactory factory)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        limite.CancelAfter(TimeSpan.FromSeconds(30));
        while (true)
        {
            if (await BatimentoAsync(factory) is { } batidoEm)
            {
                return batidoEm;
            }

            await Task.Delay(100, limite.Token);
        }
    }
}
