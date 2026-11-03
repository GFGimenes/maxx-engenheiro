using Maxx.Domain.Academico;

namespace Maxx.Domain.Tests.Academico;

// As datas de 2026 declaradas pelo autor para o seed, aqui só como cenário de teste.
internal static class Calendario2026
{
    public static DateOnly Data(int mes, int dia) => new(2026, mes, dia);

    public static SemestreLetivo Primeiro() => new(
        2026, 1, new Bimestre(1, Data(1, 26), Data(4, 3)), new Bimestre(2, Data(4, 6), Data(6, 27)));

    public static SemestreLetivo Segundo() => new(
        2026, 2, new Bimestre(3, Data(7, 27), Data(9, 26)), new Bimestre(4, Data(9, 28), Data(12, 5)));

    public static AnoLetivo Ano() => new(2026, Primeiro(), Segundo());
}
