using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Maxx.Infrastructure.Fila;

// Pega um trabalho por vez com FOR UPDATE SKIP LOCKED (ADR 0008): dois consumidores nunca
// pegam a mesma linha, e nenhum espera pelo outro. O lock vive na transação que executa o
// handler, então o que o handler grava e a saída do trabalho da fila valem juntos.
//
// Falha do handler volta ao ponto de salvamento anterior a ele: o que ele gravou some, e a
// tentativa é registrada ainda sob o lock, sem dar a outro consumidor a chance de pegar a
// linha antes do recuo. Queda do processo no meio desfaz a transação: o trabalho volta à
// fila sem contar tentativa, por isso o handler precisa ser idempotente.
public sealed partial class ConsumidorDeTrabalhos(
    IServiceScopeFactory scopes,
    TiposDeTrabalho tipos,
    TimeProvider clock,
    IOptions<FilaOptions> options,
    ILogger<ConsumidorDeTrabalhos> logger)
{
    public const int TamanhoMaximoDoErro = 2000;

    private const string _pontoAntesDoHandler = "antes_do_handler";

    // true: havia um trabalho disponível, e ele foi concluído ou teve a falha registrada.
    public async Task<bool> ProcessarProximoAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        await using var transacao = await db.Database.BeginTransactionAsync(cancellationToken);

        var agora = clock.GetUtcNow();
        var encontrados = await db.Set<Trabalho>()
            .FromSql($"""
                SELECT * FROM trabalho
                WHERE estado = 'pendente' AND disponivel_em <= {agora}
                ORDER BY disponivel_em, id
                LIMIT 1
                FOR UPDATE SKIP LOCKED
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        if (encontrados is not [var trabalho])
        {
            await transacao.CommitAsync(cancellationToken);
            return false;
        }

        await transacao.CreateSavepointAsync(_pontoAntesDoHandler, cancellationToken);
        try
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limite.CancelAfter(options.Value.TempoMaximoPorTrabalho);

            var tipo = tipos.Buscar(trabalho.Tipo)
                ?? throw new InvalidOperationException($"Nenhum handler para o tipo de trabalho \"{trabalho.Tipo}\".");
            await tipo.ExecutarAsync(scope.ServiceProvider, trabalho.Carga, limite.Token);

            // O que o handler deixou sem gravar também vale: grava antes de concluir.
            await db.SaveChangesAsync(cancellationToken);
            await db.Set<Trabalho>().Where(linha => linha.Id == trabalho.Id).ExecuteDeleteAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);
            LogConcluido(logger, trabalho.Tipo, trabalho.Id);
        }
#pragma warning disable CA1031 // Qualquer falha do handler é uma tentativa; só o desligamento passa.
        catch (Exception falha) when (!cancellationToken.IsCancellationRequested)
#pragma warning restore CA1031
        {
            await transacao.RollbackToSavepointAsync(_pontoAntesDoHandler, cancellationToken);
            db.ChangeTracker.Clear();
            await RegistrarFalhaAsync(db, trabalho, falha, cancellationToken);
            await transacao.CommitAsync(cancellationToken);
        }

        return true;
    }

    private async Task RegistrarFalhaAsync(MaxxDbContext db, Trabalho trabalho, Exception falha, CancellationToken cancellationToken)
    {
        var tentativas = trabalho.Tentativas + 1;
        var recuo = PoliticaDeRetentativa.RecuoDepoisDa(tentativas);
        var estado = recuo is null ? EstadoDoTrabalho.Falho : EstadoDoTrabalho.Pendente;
        var disponivelEm = clock.GetUtcNow() + (recuo ?? TimeSpan.Zero);
        var erro = Truncar($"{falha.GetType().Name}: {falha.Message}");

        await db.Set<Trabalho>()
            .Where(linha => linha.Id == trabalho.Id)
            .ExecuteUpdateAsync(
                linha => linha
                    .SetProperty(t => t.Tentativas, tentativas)
                    .SetProperty(t => t.Estado, estado)
                    .SetProperty(t => t.DisponivelEm, disponivelEm)
                    .SetProperty(t => t.UltimoErro, erro),
                cancellationToken);

        if (recuo is null)
        {
            LogFalhou(logger, trabalho.Tipo, trabalho.Id, tentativas, erro);
        }
        else
        {
            LogVaiTentarDeNovo(logger, trabalho.Tipo, trabalho.Id, tentativas, recuo.Value.TotalSeconds, erro);
        }
    }

    private static string Truncar(string texto) =>
        texto.Length <= TamanhoMaximoDoErro ? texto : texto[..TamanhoMaximoDoErro];

    [LoggerMessage(Level = LogLevel.Debug, Message = "Trabalho {Tipo} #{Id} concluído.")]
    private static partial void LogConcluido(ILogger logger, string tipo, long id);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Trabalho {Tipo} #{Id} falhou na tentativa {Tentativa}; volta em {Segundos} s. {Erro}")]
    private static partial void LogVaiTentarDeNovo(ILogger logger, string tipo, long id, int tentativa, double segundos, string erro);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Trabalho {Tipo} #{Id} falhou {Tentativas} vezes e ficou como falho. {Erro}")]
    private static partial void LogFalhou(ILogger logger, string tipo, long id, int tentativas, string erro);
}

// O laço do worker: processa enquanto houver trabalho e, com a fila vazia, espera o
// intervalo ocioso antes de olhar de novo. A cada volta, o batimento (no máximo um por
// intervalo), que a API lê em /health/worker: laço parado, batimento velho.
internal sealed partial class ServicoDoWorker(
    ConsumidorDeTrabalhos consumidor,
    RegistroDeBatimento batimento,
    IOptions<FilaOptions> options,
    ILogger<ServicoDoWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogIniciado(logger);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await batimento.BaterSeDevidoAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // Sem banco, sem batimento: é o sinal que o monitor procura.
            catch (Exception falha)
#pragma warning restore CA1031
            {
                LogFalhaNoBatimento(logger, falha.GetType().Name, falha.Message);
            }

            bool havia;
            try
            {
                havia = await consumidor.ProcessarProximoAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // Banco fora do ar não pode matar o worker: espera e tenta de novo.
            catch (Exception falha)
#pragma warning restore CA1031
            {
                LogFalhaNoConsumo(logger, falha.GetType().Name, falha.Message);
                havia = false;
            }

            if (!havia)
            {
                try
                {
                    await Task.Delay(options.Value.IntervaloOcioso, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Worker consumindo a fila de trabalhos.")]
    private static partial void LogIniciado(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao consumir a fila: {Tipo}: {Erro}")]
    private static partial void LogFalhaNoConsumo(ILogger logger, string tipo, string erro);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao gravar o batimento do worker: {Tipo}: {Erro}")]
    private static partial void LogFalhaNoBatimento(ILogger logger, string tipo, string erro);
}
