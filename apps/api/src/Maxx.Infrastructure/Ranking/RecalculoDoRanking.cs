using Maxx.Application.Fila;
using Maxx.Application.Ranking;
using Maxx.Domain.Academico;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Domain.Ranking;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.RaridadeDosItens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Maxx.Infrastructure.Ranking;

// No worker: reescreve as quatro visões do ranking (produto.md 5), com coalescência (ADR
// 0005), como o recálculo de raridade. Os outros pedidos pendentes viram este, porque a conta
// lê o estado de agora; e se a última execução foi há menos de dez segundos, este não calcula
// e deixa um pedido só para quando os dez segundos fecharem. Uma execução reescreve todas as
// visões, então a janela vale para cada uma.
//
// O que pontua e em que semestre conta é regra do domínio (ParcelaDoRanking), e a ordem
// também (Classificacao). Aqui só se lê o banco e se grava a tabela: o histórico sempre, e o
// semestre só enquanto não congela. O congelado fica como estava (SemestresDoRanking).
internal sealed partial class RecalculoDoRanking(
    MaxxDbContext db, IFilaDeTrabalhos fila, TimeProvider relogio, ILogger<RecalculoDoRanking> logger)
    : ITrabalhoHandler<RecalcularRanking>
{
    public const string Nome = "ranking";

    public static readonly TimeSpan IntervaloMinimo = TimeSpan.FromSeconds(10);

    public async Task ExecutarAsync(RecalcularRanking carga, CancellationToken cancellationToken)
    {
        // A linha do recálculo, travada: dois workers nunca calculam ao mesmo tempo.
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO recalculo (nome) VALUES ({Nome}) ON CONFLICT (nome) DO NOTHING", cancellationToken);
        var estado = await db.Set<RecalculoRow>()
            .FromSql($"SELECT * FROM recalculo WHERE nome = {Nome} FOR UPDATE")
            .SingleAsync(cancellationToken);

        // Os pedidos pendentes, este inclusive, viram uma execução só. O que outro worker já
        // segura fica de fora: ele está rodando.
        await db.Database.ExecuteSqlAsync(
            $"""
            DELETE FROM trabalho WHERE id IN (
                SELECT id FROM trabalho WHERE tipo = {RecalcularRanking.Tipo} AND estado = 'pendente'
                FOR UPDATE SKIP LOCKED)
            """,
            cancellationToken);

        var agora = relogio.GetUtcNow();
        if (estado.UltimaExecucaoEm is { } ultima && agora - ultima < IntervaloMinimo)
        {
            fila.Enfileirar(new RecalcularRanking(), ultima + IntervaloMinimo);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var (linhas, semestres) = await RecalcularAsync(FusoHorario.DataLocal(agora), cancellationToken);
        estado.UltimaExecucaoEm = agora;
        await db.SaveChangesAsync(cancellationToken);
        LogRecalculado(logger, linhas, semestres);
    }

    private async Task<(int Linhas, int Semestres)> RecalcularAsync(DateOnly hoje, CancellationToken cancellationToken)
    {
        var calendario = new Calendario(await db.Set<AnoLetivo>().AsNoTracking()
            .Include(ano => ano.Semestres)
            .ThenInclude(semestre => semestre.Bimestres)
            .ToListAsync(cancellationToken));
        var abertos = SemestresDoRanking.Abertos(calendario, hoje).Select(semestre => semestre.Id).ToList();

        // Os alunos com conta, ativos e ex-alunos, os de demonstração inclusive, com o curso
        // atual, que vem do cadastro elegível.
        var alunos = await (
                from usuario in db.Set<Usuario>()
                join cadastro in db.Set<CadastroElegivel>() on usuario.CadastroElegivelId equals cadastro.Id
                where usuario.Papeis == Papeis.Aluno
                select new { usuario.Id, cadastro.CursoId, usuario.ExAlunoDesde })
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var doRanking = alunos
            .Where(aluno => aluno.CursoId is not null)
            .Select(aluno => new AlunoDoRanking(aluno.Id, aluno.CursoId!.Value, aluno.ExAlunoDesde is not null))
            .ToList();

        var parcelas = await ParcelasAsync(calendario, cancellationToken);
        List<LinhaDoRanking> novas = [.. Classificacao.Historico(doRanking, parcelas)];
        foreach (var semestre in abertos)
        {
            novas.AddRange(Classificacao.Semestre(semestre, doRanking, parcelas));
        }

        await db.Set<PosicaoNoRankingRow>()
            .Where(linha => linha.Periodo == PeriodoDoRanking.Historico
                || (linha.SemestreLetivoId != null && abertos.Contains(linha.SemestreLetivoId.Value)))
            .ExecuteDeleteAsync(cancellationToken);
        db.AddRange(novas.Select(PosicaoNoRankingRow.De));

        return (novas.Count, abertos.Count);
    }

    // As três fontes passam pelas portas do domínio (ParcelaDoRanking): é lá que mora o que
    // pontua e em que semestre. Só se carrega o que pode pontuar.
    private async Task<List<ParcelaDoRanking>> ParcelasAsync(Calendario calendario, CancellationToken cancellationToken)
    {
        var medalhas = await db.Set<Medalha>().AsNoTracking()
            .Where(medalha => medalha.AnuladaEm == null)
            .ToListAsync(cancellationToken);

        var premios = await (
                from premio in db.Set<PremioParticipacao>()
                join evento in db.Set<Evento>() on premio.EventoId equals evento.Id
                where premio.Estado == EstadoDoPremioParticipacao.Valido || premio.Estado == EstadoDoPremioParticipacao.Definitivo
                select new { Premio = premio, Evento = evento })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var conquistas = await db.Set<Conquista>().AsNoTracking()
            .Where(conquista => conquista.RemovidaEm == null)
            .ToListAsync(cancellationToken);

        return
        [
            .. medalhas.Select(ParcelaDoRanking.DaMedalha).OfType<ParcelaDoRanking>(),
            .. premios.Select(par => ParcelaDoRanking.DoPremio(par.Premio, par.Evento, calendario)).OfType<ParcelaDoRanking>(),
            .. conquistas.Select(conquista => ParcelaDoRanking.DaConquista(conquista, calendario)).OfType<ParcelaDoRanking>(),
        ];
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ranking recalculado: {Linhas} linhas, {Semestres} semestres abertos.")]
    private static partial void LogRecalculado(ILogger logger, int linhas, int semestres);
}
