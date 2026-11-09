using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;

namespace Maxx.Domain.Ranking;

// Quais semestres letivos o ranking mostra e reescreve (produto.md 5; ADR 0005).
public static class SemestresDoRanking
{
    // O semestre que a tela chama de atual: o último que já começou. No recesso continua o
    // que acabou de terminar; quando o seguinte começa, a visão passa para ele, do zero.
    // Nulo antes do primeiro semestre do calendário.
    public static SemestreLetivo? Atual(Calendario calendario, DateOnly hoje)
    {
        ArgumentNullException.ThrowIfNull(calendario);

        return calendario.Anos
            .SelectMany(ano => ano.Semestres)
            .Where(semestre => semestre.Inicio <= hoje)
            .MaxBy(semestre => semestre.Inicio);
    }

    // O ranking do semestre congela 30 dias depois do fim do semestre: no dia do fim mais
    // 30, já congelado. É a mesma leitura da reavaliação de conquista de fechamento
    // (InstanciaDePeriodo.Congelamento), e por isso a mesma constante.
    public static bool Congelado(SemestreLetivo semestre, Calendario calendario, DateOnly hoje)
    {
        ArgumentNullException.ThrowIfNull(semestre);
        ArgumentNullException.ThrowIfNull(calendario);

        return new InstanciaDePeriodo(Periodo.Semestre, semestre.Id).Congelamento(calendario) is { } congelamento
            && hoje >= congelamento;
    }

    // Os semestres cujas visões o worker ainda reescreve: todos os não congelados, inclusive
    // o que ainda não começou, porque o evento do recesso já conta para ele. O congelado
    // fica como estava: aprovação posterior entra só no histórico.
    public static IReadOnlyList<SemestreLetivo> Abertos(Calendario calendario, DateOnly hoje)
    {
        ArgumentNullException.ThrowIfNull(calendario);

        return [.. calendario.Anos
            .SelectMany(ano => ano.Semestres)
            .Where(semestre => !Congelado(semestre, calendario, hoje))];
    }
}
