using System.Collections.Concurrent;
using Maxx.Application;
using Maxx.Application.Fila;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Maxx.Api.IntegrationTests.Fila;

// A fila no Postgres (ADR 0008): enfileiramento na transação de quem escreve, consumo com
// SKIP LOCKED, retentativa com recuo e trabalho envenenado parado no limite.
public class FilaDeTrabalhosTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Trabalho_enfileirado_grava_junto_com_a_escrita()
    {
        await using var factory = await CriarAsync();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IFilaDeTrabalhos>().Enfileirar(new CargaDeTeste("um"));
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SalvarAsync(Ct);
        }

        var trabalho = Assert.Single(await TrabalhosAsync(factory));
        Assert.Equal(CargaDeTeste.Tipo, trabalho.Tipo);
        Assert.Equal(EstadoDoTrabalho.Pendente, trabalho.Estado);
        Assert.Equal(0, trabalho.Tentativas);
        Assert.Contains("\"texto\":\"um\"", trabalho.Carga.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Escrita_desfeita_nao_deixa_trabalho()
    {
        await using var factory = await CriarAsync();

        // Sem gravar: o escopo termina e o trabalho não existe.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IFilaDeTrabalhos>().Enfileirar(new CargaDeTeste("sem gravar"));
        }

        // Gravado numa transação revertida.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
            await using var transacao = await db.Database.BeginTransactionAsync(Ct);
            scope.ServiceProvider.GetRequiredService<IFilaDeTrabalhos>().Enfileirar(new CargaDeTeste("revertido"));
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SalvarAsync(Ct);
            await transacao.RollbackAsync(Ct);
        }

        Assert.Empty(await TrabalhosAsync(factory));
    }

    [Fact]
    public async Task Consumidor_executa_o_trabalho_e_o_tira_da_fila()
    {
        var registro = new Registro();
        await using var factory = await CriarAsync(registro);
        await EnfileirarAsync(factory, new CargaDeTeste("um"));

        Assert.True(await ProcessarAsync(factory));
        Assert.False(await ProcessarAsync(factory));

        Assert.Equal(["um"], registro.Executados);
        Assert.Empty(await TrabalhosAsync(factory));
    }

    [Fact]
    public async Task Consumidores_em_paralelo_nunca_executam_o_mesmo_trabalho()
    {
        var registro = new Registro { Espera = TimeSpan.FromMilliseconds(20) };
        await using var factory = await CriarAsync(registro);
        var textos = Enumerable.Range(1, 40).Select(n => $"t{n}").ToList();
        await EnfileirarAsync(factory, [.. textos.Select(texto => new CargaDeTeste(texto))]);

        // Quatro consumidores esvaziando a fila ao mesmo tempo, como réplicas de worker.
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(
            async () =>
            {
                while (await ProcessarAsync(factory))
                {
                }
            },
            Ct)));

        Assert.Equal(textos.Order(StringComparer.Ordinal), registro.Executados.Order(StringComparer.Ordinal));
        Assert.Empty(await TrabalhosAsync(factory));
    }

    [Fact]
    public async Task Falha_desfaz_o_que_o_handler_gravou_e_volta_com_recuo()
    {
        var relogio = new RelogioDeTeste();
        var registro = new Registro { Falhar = true, EnfileirarAoExecutar = true };
        await using var factory = await CriarAsync(registro, relogio);
        await EnfileirarAsync(factory, new CargaDeTeste("falha"));

        Assert.True(await ProcessarAsync(factory));

        // O trabalho que o handler enfileirou e gravou antes de falhar sumiu com a falha.
        var trabalho = Assert.Single(await TrabalhosAsync(factory));
        Assert.Equal(EstadoDoTrabalho.Pendente, trabalho.Estado);
        Assert.Equal(1, trabalho.Tentativas);
        Assert.Equal(relogio.Agora + TimeSpan.FromSeconds(10), trabalho.DisponivelEm);
        Assert.Equal("InvalidOperationException: Falha de teste.", trabalho.UltimoErro);

        // Antes do recuo, ninguém pega.
        relogio.Avancar(TimeSpan.FromSeconds(9));
        Assert.False(await ProcessarAsync(factory));

        relogio.Avancar(TimeSpan.FromSeconds(1));
        Assert.True(await ProcessarAsync(factory));
        trabalho = Assert.Single(await TrabalhosAsync(factory));
        Assert.Equal(2, trabalho.Tentativas);
        Assert.Equal(relogio.Agora + TimeSpan.FromSeconds(20), trabalho.DisponivelEm);
    }

    [Fact]
    public async Task Trabalho_que_sempre_falha_para_no_limite_de_tentativas()
    {
        var relogio = new RelogioDeTeste();
        var registro = new Registro { Falhar = true };
        await using var factory = await CriarAsync(registro, relogio);
        await EnfileirarAsync(factory, new CargaDeTeste("envenenado"));

        for (var tentativa = 1; tentativa <= PoliticaDeRetentativa.MaximoDeTentativas; tentativa++)
        {
            Assert.True(await ProcessarAsync(factory));
            relogio.Avancar(TimeSpan.FromMinutes(10));
        }

        var trabalho = Assert.Single(await TrabalhosAsync(factory));
        Assert.Equal(EstadoDoTrabalho.Falho, trabalho.Estado);
        Assert.Equal(PoliticaDeRetentativa.MaximoDeTentativas, trabalho.Tentativas);

        relogio.Avancar(TimeSpan.FromDays(30));
        Assert.False(await ProcessarAsync(factory));
        Assert.Equal(PoliticaDeRetentativa.MaximoDeTentativas, registro.Executados.Count);
        Assert.Contains(factory.Log.Linhas, linha => linha.Contains("ficou como falho", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Tipo_sem_handler_conta_como_falha()
    {
        await using var factory = await CriarAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO trabalho (tipo, carga, estado, tentativas, criado_em, disponivel_em) " +
                "VALUES ('inexistente', '{{}}', 'pendente', 0, now(), now())",
                Ct);
        }

        Assert.True(await ProcessarAsync(factory));

        var trabalho = Assert.Single(await TrabalhosAsync(factory));
        Assert.Equal(1, trabalho.Tentativas);
        Assert.Contains("inexistente", trabalho.UltimoErro, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handler_que_passa_do_tempo_maximo_e_cancelado_e_conta_como_falha()
    {
        var registro = new Registro { Espera = Timeout.InfiniteTimeSpan };
        await using var factory = await CriarAsync(
            registro, configurar: options => options.TempoMaximoPorTrabalho = TimeSpan.FromMilliseconds(200));
        await EnfileirarAsync(factory, new CargaDeTeste("travado"));

        Assert.True(await ProcessarAsync(factory).WaitAsync(TimeSpan.FromSeconds(30), Ct));

        var trabalho = Assert.Single(await TrabalhosAsync(factory));
        Assert.Equal(1, trabalho.Tentativas);
        Assert.StartsWith("TaskCanceledException", trabalho.UltimoErro, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Worker_consome_sozinho_o_que_chega_na_fila()
    {
        var registro = new Registro();
        await using var factory = await CriarAsync(
            registro,
            configurar: options => options.IntervaloOcioso = TimeSpan.FromMilliseconds(50),
            comWorker: true);

        await EnfileirarAsync(factory, new CargaDeTeste("sozinho"));

        Assert.Equal("sozinho", await registro.Primeiro.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct));
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 20)]
    [InlineData(3, 40)]
    [InlineData(4, 80)]
    public void Recuo_dobra_a_cada_falha(int tentativas, int segundos) =>
        Assert.Equal(TimeSpan.FromSeconds(segundos), PoliticaDeRetentativa.RecuoDepoisDa(tentativas));

    [Fact]
    public void Quinta_falha_esgota_as_tentativas() =>
        Assert.Null(PoliticaDeRetentativa.RecuoDepoisDa(PoliticaDeRetentativa.MaximoDeTentativas));

    private async Task<MaxxApiFactory> CriarAsync(
        Registro? registro = null,
        RelogioDeTeste? relogio = null,
        Action<FilaOptions>? configurar = null,
        bool comWorker = false)
    {
        var factory = new MaxxApiFactory(await postgres.CreateDatabaseAsync(), servicos: services =>
        {
            services.AddSingleton(registro ?? new Registro());
            services.AddTrabalho<CargaDeTeste, HandlerDeTeste>();
            if (relogio is not null)
            {
                services.Replace(ServiceDescriptor.Singleton<TimeProvider>(relogio));
            }

            if (configurar is not null)
            {
                services.Configure(configurar);
            }

            if (comWorker)
            {
                services.AddWorker();
            }
        });
        await factory.MigrateAsync(Ct);
        return factory;
    }

    private static async Task EnfileirarAsync(MaxxApiFactory factory, params CargaDeTeste[] cargas)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var fila = scope.ServiceProvider.GetRequiredService<IFilaDeTrabalhos>();
        foreach (var carga in cargas)
        {
            fila.Enfileirar(carga);
        }

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SalvarAsync(Ct);
    }

    private static Task<bool> ProcessarAsync(MaxxApiFactory factory) =>
        factory.Services.GetRequiredService<ConsumidorDeTrabalhos>().ProcessarProximoAsync(Ct);

    private static async Task<List<Trabalho>> TrabalhosAsync(MaxxApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MaxxDbContext>()
            .Set<Trabalho>().AsNoTracking().OrderBy(trabalho => trabalho.Id).ToListAsync(Ct);
    }

    public sealed record CargaDeTeste(string Texto) : ICargaDeTrabalho
    {
        public static string Tipo => "teste";
    }

    public sealed record OutraCarga(string Texto) : ICargaDeTrabalho
    {
        public static string Tipo => "teste-outro";
    }

    // O que o handler de teste fez, e como deve se comportar.
    private sealed class Registro
    {
        public TimeSpan Espera { get; init; } = TimeSpan.Zero;

        public bool Falhar { get; init; }

        public bool EnfileirarAoExecutar { get; init; }

        public ConcurrentQueue<string> Executados { get; } = new();

        public TaskCompletionSource<string> Primeiro { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Anotar(string texto)
        {
            Executados.Enqueue(texto);
            Primeiro.TrySetResult(texto);
        }
    }

    private sealed class HandlerDeTeste(Registro registro, IFilaDeTrabalhos fila, IUnitOfWork unitOfWork)
        : ITrabalhoHandler<CargaDeTeste>
    {
        public async Task ExecutarAsync(CargaDeTeste carga, CancellationToken cancellationToken)
        {
            registro.Anotar(carga.Texto);
            if (registro.Espera != TimeSpan.Zero)
            {
                await Task.Delay(registro.Espera, cancellationToken);
            }

            if (registro.EnfileirarAoExecutar)
            {
                fila.Enfileirar(new OutraCarga(carga.Texto));
                await unitOfWork.SalvarAsync(cancellationToken);
            }

            if (registro.Falhar)
            {
                throw new InvalidOperationException("Falha de teste.");
            }
        }
    }

    private sealed class RelogioDeTeste : TimeProvider
    {
        // Em segundo cheio: o Postgres guarda microssegundos, e a comparação é exata.
        private DateTimeOffset _agora = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        public DateTimeOffset Agora => _agora;

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan quanto) => _agora += quanto;
    }
}
