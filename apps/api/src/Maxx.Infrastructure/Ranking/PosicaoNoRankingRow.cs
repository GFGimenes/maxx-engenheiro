using Maxx.Domain.Ranking;

namespace Maxx.Infrastructure.Ranking;

// Uma linha de uma das quatro visões do ranking, escrita só pelo worker (ADR 0005). A
// leitura nunca calcula nada: lê esta linha pelo índice da visão e da posição. O semestre só
// no período semestre; o curso só no escopo curso.
public sealed class PosicaoNoRankingRow
{
    public long Id { get; set; }

    public EscopoDoRanking Escopo { get; set; }

    public PeriodoDoRanking Periodo { get; set; }

    public Guid? SemestreLetivoId { get; set; }

    public Guid? CursoId { get; set; }

    public Guid AlunoId { get; set; }

    public int Posicao { get; set; }

    public int Pontos { get; set; }

    // O desempate, materializado (ADR 0005): o instante da última concessão que compõe os
    // pontos desta visão.
    public DateTimeOffset UltimaConcessaoEm { get; set; }

    public static PosicaoNoRankingRow De(LinhaDoRanking linha)
    {
        ArgumentNullException.ThrowIfNull(linha);

        return new PosicaoNoRankingRow
        {
            Escopo = linha.Visao.Escopo,
            Periodo = linha.Visao.Periodo,
            SemestreLetivoId = linha.Visao.SemestreLetivoId,
            CursoId = linha.Visao.CursoId,
            AlunoId = linha.AlunoId,
            Posicao = linha.Posicao,
            Pontos = linha.Pontos,
            UltimaConcessaoEm = linha.UltimaConcessaoEm,
        };
    }
}
