namespace Maxx.Domain.Ranking;

// As quatro visões do ranking (produto.md 5): escopo geral ou por curso, período semestre
// letivo ou histórico.
public enum EscopoDoRanking
{
    Geral,
    Curso,
}

public enum PeriodoDoRanking
{
    Semestre,
    Historico,
}

// Um aluno com conta, como o ranking o vê: o curso atual, porque quem troca de curso leva
// os pontos para o ranking do curso novo, e se já é ex-aluno.
public sealed record AlunoDoRanking(Guid Id, Guid CursoId, bool ExAluno);

// Uma visão: o semestre só no período semestre, o curso só no escopo curso.
public sealed record VisaoDoRanking(EscopoDoRanking Escopo, PeriodoDoRanking Periodo, Guid? SemestreLetivoId, Guid? CursoId);

// Uma linha de uma visão: a posição, os pontos e o instante de desempate, que a tabela
// guarda como coluna (ADR 0005).
public sealed record LinhaDoRanking(VisaoDoRanking Visao, Guid AlunoId, int Posicao, int Pontos, DateTimeOffset UltimaConcessaoEm);

// O cálculo das visões a partir das parcelas. Regras (produto.md 5):
// - pontos são a soma das parcelas do aluno no período da visão;
// - aluno sem pontos na visão não tem linha nela;
// - desempate pelo instante da última concessão que compõe a pontuação: quem parou de
//   pontuar antes fica na frente; persistindo o empate, o id do aluno, para a posição não
//   oscilar entre dois recálculos;
// - ex-aluno só no histórico;
// - o escopo curso é o curso atual do aluno.
public static class Classificacao
{
    // O histórico, geral e por curso.
    public static IReadOnlyList<LinhaDoRanking> Historico(
        IEnumerable<AlunoDoRanking> alunos, IEnumerable<ParcelaDoRanking> parcelas)
    {
        ArgumentNullException.ThrowIfNull(alunos);
        ArgumentNullException.ThrowIfNull(parcelas);

        return Visoes(alunos.ToList(), parcelas, PeriodoDoRanking.Historico, semestreLetivoId: null);
    }

    // Um semestre letivo, geral e por curso: só as parcelas que contam nele e só alunos que
    // não são ex-alunos.
    public static IReadOnlyList<LinhaDoRanking> Semestre(
        Guid semestreLetivoId, IEnumerable<AlunoDoRanking> alunos, IEnumerable<ParcelaDoRanking> parcelas)
    {
        ArgumentNullException.ThrowIfNull(alunos);
        ArgumentNullException.ThrowIfNull(parcelas);

        return Visoes(
            [.. alunos.Where(aluno => !aluno.ExAluno)],
            parcelas.Where(parcela => parcela.SemestreLetivoId == semestreLetivoId),
            PeriodoDoRanking.Semestre,
            semestreLetivoId);
    }

    private static List<LinhaDoRanking> Visoes(
        List<AlunoDoRanking> alunos, IEnumerable<ParcelaDoRanking> parcelas, PeriodoDoRanking periodo, Guid? semestreLetivoId)
    {
        var porId = alunos.ToDictionary(aluno => aluno.Id);
        var somas = parcelas
            .Where(parcela => porId.ContainsKey(parcela.AlunoId))
            .GroupBy(parcela => parcela.AlunoId)
            .Select(doAluno => new Soma(
                porId[doAluno.Key],
                doAluno.Sum(parcela => parcela.Pontos),
                doAluno.Max(parcela => parcela.ConcedidaEm)))
            .Where(soma => soma.Pontos > 0)
            .ToList();

        List<LinhaDoRanking> linhas = [.. Ordenar(somas, new VisaoDoRanking(EscopoDoRanking.Geral, periodo, semestreLetivoId, null))];
        foreach (var doCurso in somas.GroupBy(soma => soma.Aluno.CursoId))
        {
            linhas.AddRange(Ordenar(doCurso, new VisaoDoRanking(EscopoDoRanking.Curso, periodo, semestreLetivoId, doCurso.Key)));
        }

        return linhas;
    }

    private static IEnumerable<LinhaDoRanking> Ordenar(IEnumerable<Soma> somas, VisaoDoRanking visao) => somas
        .OrderByDescending(soma => soma.Pontos)
        .ThenBy(soma => soma.UltimaConcessaoEm)
        .ThenBy(soma => soma.Aluno.Id)
        .Select((soma, indice) => new LinhaDoRanking(visao, soma.Aluno.Id, indice + 1, soma.Pontos, soma.UltimaConcessaoEm));
    private sealed record Soma(AlunoDoRanking Aluno, int Pontos, DateTimeOffset UltimaConcessaoEm);
}
