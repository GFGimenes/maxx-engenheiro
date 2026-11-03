namespace Maxx.Domain.Academico;

// Numerado de 1 a 4 dentro do ano letivo: o semestre 1 tem os bimestres 1 e 2, o
// semestre 2 tem o 3 e o 4. Datas inclusivas nas duas pontas.
public sealed class Bimestre
{
    public const int PorAno = SemestreLetivo.PorAno * SemestreLetivo.BimestresPorSemestre;

    // Para o EF Core materializar do banco.
    private Bimestre()
    {
    }

    public Bimestre(int numero, DateOnly inicio, DateOnly fim)
    {
        if (numero is < 1 or > PorAno)
        {
            throw new DomainException($"Bimestre {numero} não existe: o ano letivo tem {PorAno}.");
        }

        if (fim < inicio)
        {
            throw new DomainException($"Bimestre {numero} termina ({fim}) antes de começar ({inicio}).");
        }

        Id = Guid.CreateVersion7();
        Numero = numero;
        Inicio = inicio;
        Fim = fim;
    }

    public Guid Id { get; private set; }

    public int Numero { get; private set; }

    public DateOnly Inicio { get; private set; }

    public DateOnly Fim { get; private set; }

    public bool Contem(DateOnly data) => data >= Inicio && data <= Fim;

    // Só pelo AnoLetivo, que valida o ano corrigido inteiro antes de copiar as datas.
    internal bool Redatar(DateOnly inicio, DateOnly fim)
    {
        if (inicio == Inicio && fim == Fim)
        {
            return false;
        }

        Inicio = inicio;
        Fim = fim;
        return true;
    }
}
