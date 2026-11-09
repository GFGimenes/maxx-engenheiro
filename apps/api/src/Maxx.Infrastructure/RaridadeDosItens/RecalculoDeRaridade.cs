using Maxx.Application.Fila;
using Maxx.Application.Medalhas;
using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Maxx.Infrastructure.RaridadeDosItens;

// A raridade atual de um item do catálogo, escrita só pelo worker (ADR 0005). A leitura
// nunca calcula nada: lê esta linha.
public sealed class RaridadeDoItemRow
{
    public required string ItemCodigo { get; set; }

    public int AlunosComItem { get; set; }

    public int AlunosElegiveis { get; set; }

    public required string Raridade { get; set; }

    public DateTimeOffset CalculadaEm { get; set; }
}

// Quando cada recálculo do worker rodou pela última vez: é o que limita a coalescência.
public sealed class RecalculoRow
{
    public required string Nome { get; set; }

    public DateTimeOffset? UltimaExecucaoEm { get; set; }
}

// No worker: recalcula a raridade de todos os itens do catálogo (produto.md 4.3), com
// coalescência (ADR 0005). Os outros pedidos pendentes de recálculo viram este, porque a
// conta lê o estado de agora; e se a última execução foi há menos de dez segundos, este não
// calcula e deixa um pedido só para quando os dez segundos fecharem.
//
// O numerador são os alunos distintos com ao menos uma medalha ativa ou um prêmio de
// participação válido ou definitivo do item (passo 8), ou uma conquista não removida (passo 9),
// ativos ou ex-alunos. O divisor são os alunos com conta, ativos e ex-alunos, os de demonstração
// inclusive: o campus inteiro para item aberto, o curso atual para item restrito.
internal sealed partial class RecalculoDeRaridade(
    MaxxDbContext db, IFilaDeTrabalhos fila, TimeProvider relogio, ILogger<RecalculoDeRaridade> logger)
    : ITrabalhoHandler<RecalcularRaridade>
{
    public const string Nome = "raridade";

    public static readonly TimeSpan IntervaloMinimo = TimeSpan.FromSeconds(10);

    public async Task ExecutarAsync(RecalcularRaridade carga, CancellationToken cancellationToken)
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
                SELECT id FROM trabalho WHERE tipo = {RecalcularRaridade.Tipo} AND estado = 'pendente'
                FOR UPDATE SKIP LOCKED)
            """,
            cancellationToken);

        var agora = relogio.GetUtcNow();
        if (estado.UltimaExecucaoEm is { } ultima && agora - ultima < IntervaloMinimo)
        {
            fila.Enfileirar(new RecalcularRaridade(), ultima + IntervaloMinimo);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var itens = await RecalcularAsync(agora, cancellationToken);
        estado.UltimaExecucaoEm = agora;
        await db.SaveChangesAsync(cancellationToken);
        LogRecalculada(logger, itens);
    }

    private async Task<int> RecalcularAsync(DateTimeOffset agora, CancellationToken cancellationToken)
    {
        var alunosPorCurso = await (
                from usuario in db.Set<Usuario>()
                join cadastro in db.Set<CadastroElegivel>() on usuario.CadastroElegivelId equals cadastro.Id
                join curso in db.Set<Curso>() on cadastro.CursoId equals curso.Id
                where usuario.Papeis == Papeis.Aluno
                group usuario by curso.Sigla into porCurso
                select new { Sigla = porCurso.Key, Alunos = porCurso.Count() })
            .ToDictionaryAsync(curso => curso.Sigla, curso => curso.Alunos, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var todos = alunosPorCurso.Values.Sum();

        var comItem = await db.Set<Medalha>()
            .Where(medalha => medalha.AnuladaEm == null)
            .GroupBy(medalha => medalha.ItemCodigo)
            .Select(item => new { Codigo = item.Key, Alunos = item.Select(medalha => medalha.AlunoId).Distinct().Count() })
            .ToDictionaryAsync(item => item.Codigo, item => item.Alunos, cancellationToken);

        // Prêmio de participação conta quando pontua, válido ou definitivo (D6 do passo 8): o
        // reivindicado ainda não foi aprovado, e o em averiguação só o aluno e a instituição
        // veem. Os códigos não se repetem entre os tipos, então os dois somam sem colidir.
        var comParticipacao = await db.Set<PremioParticipacao>()
            .Where(premio => premio.Estado == EstadoDoPremioParticipacao.Valido || premio.Estado == EstadoDoPremioParticipacao.Definitivo)
            .GroupBy(premio => premio.ItemCodigo)
            .Select(item => new { Codigo = item.Key, Alunos = item.Select(premio => premio.AlunoId).Distinct().Count() })
            .ToListAsync(cancellationToken);
        foreach (var item in comParticipacao)
        {
            comItem[item.Codigo] = item.Alunos;
        }

        // Conquista conta enquanto não removida (passo 9). Repetível por instância do
        // período, conta uma vez por aluno (produto.md 4.3).
        var comConquista = await db.Set<Conquista>()
            .Where(conquista => conquista.RemovidaEm == null)
            .GroupBy(conquista => conquista.ItemCodigo)
            .Select(item => new { Codigo = item.Key, Alunos = item.Select(conquista => conquista.AlunoId).Distinct().Count() })
            .ToListAsync(cancellationToken);
        foreach (var item in comConquista)
        {
            comItem[item.Codigo] = item.Alunos;
        }

        var itens = await db.Set<ItemCatalogoRow>().AsNoTracking()
            .Select(item => new { item.Codigo, item.RestricaoCurso })
            .ToListAsync(cancellationToken);
        var atuais = await db.Set<RaridadeDoItemRow>().ToDictionaryAsync(linha => linha.ItemCodigo, cancellationToken);

        foreach (var item in itens)
        {
            var elegiveis = item.RestricaoCurso is null ? todos : alunosPorCurso.GetValueOrDefault(item.RestricaoCurso);
            // Quem trocou de curso leva a medalha de item restrito ao curso antigo: no curso novo
            // ele não é elegível, e o numerador não passa do divisor.
            var com = Math.Min(comItem.GetValueOrDefault(item.Codigo), elegiveis);
            if (!atuais.TryGetValue(item.Codigo, out var linha))
            {
                linha = new RaridadeDoItemRow { ItemCodigo = item.Codigo, Raridade = string.Empty };
                db.Add(linha);
            }

            linha.AlunosComItem = com;
            linha.AlunosElegiveis = elegiveis;
            linha.Raridade = NomeJson.De(Raridades.De(com, elegiveis));
            linha.CalculadaEm = agora;
        }

        return itens.Count;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Raridade recalculada para {Itens} itens.")]
    private static partial void LogRecalculada(ILogger logger, int itens);
}
