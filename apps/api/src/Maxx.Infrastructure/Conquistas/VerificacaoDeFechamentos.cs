using Maxx.Application.Conquistas;
using Maxx.Application.Fila;
using Maxx.Domain.Academico;
using Maxx.Domain.Conquistas;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Maxx.Infrastructure.Conquistas;

// Uma linha de fechamento_de_periodo: a instância que o worker já fechou, e quando.
public sealed class FechamentoDePeriodoRow
{
    public required string TipoDePeriodo { get; set; }

    public Guid ReferenciaDoPeriodo { get; set; }

    public DateTimeOffset FechadoEm { get; set; }
}

// O agendador do fechamento de período (ADR 0008, D7 do passo 9). Fecha cada instância
// encerrada que ainda não tem registro de fechamento: grava o registro e enfileira o
// fechamento na mesma transação, então uma instância nunca fecha duas vezes e nunca fica
// registrada sem o trabalho. Na primeira execução, fecha também as que ficaram para trás.
//
// Recorrente: no fim, deixa a próxima verificação para daqui a uma hora, e apaga as outras
// pendentes, para a corrente nunca se multiplicar (o seed pode ter pedido uma a mais).
internal sealed partial class VerificacaoDeFechamentos(
    MaxxDbContext db,
    IConquistaRepository conquistas,
    IFilaDeTrabalhos fila,
    TimeProvider relogio,
    ILogger<VerificacaoDeFechamentos> logger) : ITrabalhoHandler<VerificarFechamentos>
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);

    // Mesma função de trava do aluno, outra classe: duas verificações nunca rodam juntas.
    private const int _classeDaTrava = 10;

    public async Task ExecutarAsync(VerificarFechamentos carga, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({_classeDaTrava}, 0)", cancellationToken);
        await db.Database.ExecuteSqlAsync(
            $"""
            DELETE FROM trabalho WHERE id IN (
                SELECT id FROM trabalho WHERE tipo = {VerificarFechamentos.Tipo} AND estado = 'pendente'
                FOR UPDATE SKIP LOCKED)
            """,
            cancellationToken);

        var agora = relogio.GetUtcNow();
        var calendario = await conquistas.CalendarioAsync(cancellationToken);
        var fechadas = (await db.Set<FechamentoDePeriodoRow>().AsNoTracking().ToListAsync(cancellationToken))
            .Select(linha => (linha.TipoDePeriodo, linha.ReferenciaDoPeriodo))
            .ToHashSet();

        var novas = 0;
        foreach (var instancia in InstanciaDePeriodo.EncerradasAte(calendario, FusoHorario.DataLocal(agora)))
        {
            var tipo = NomeJson.De(instancia.Tipo);
            if (fechadas.Contains((tipo, instancia.Referencia)))
            {
                continue;
            }

            db.Add(new FechamentoDePeriodoRow { TipoDePeriodo = tipo, ReferenciaDoPeriodo = instancia.Referencia, FechadoEm = agora });
            fila.Enfileirar(new FecharPeriodo(instancia.Tipo, instancia.Referencia));
            novas++;
        }

        fila.Enfileirar(new VerificarFechamentos(), agora + Intervalo);
        await db.SaveChangesAsync(cancellationToken);
        if (novas > 0)
        {
            LogFechadas(logger, novas);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Instancias} instâncias de período fechadas.")]
    private static partial void LogFechadas(ILogger logger, int instancias);
}
