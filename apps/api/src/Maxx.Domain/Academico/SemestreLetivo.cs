namespace Maxx.Domain.Academico;

// O semestre letivo (2026/2) corta ranking e conquistas. Não confundir com o semestre do
// aluno, que é só perfil. Começa no primeiro dia do primeiro bimestre e termina no último
// dia do segundo: férias e recesso ficam fora.
public sealed class SemestreLetivo
{
    public const int PorAno = 2;
    public const int BimestresPorSemestre = 2;

    private readonly List<Bimestre> _bimestres;

    public SemestreLetivo(int ano, int numero, Bimestre primeiro, Bimestre segundo)
    {
        ArgumentNullException.ThrowIfNull(primeiro);
        ArgumentNullException.ThrowIfNull(segundo);

        if (ano < 1)
        {
            throw new DomainException($"Ano {ano} inválido.");
        }

        if (numero is < 1 or > PorAno)
        {
            throw new DomainException($"Semestre {numero} não existe: o ano letivo tem {PorAno}.");
        }

        var esperado = ((numero - 1) * BimestresPorSemestre) + 1;
        if (primeiro.Numero != esperado || segundo.Numero != esperado + 1)
        {
            throw new DomainException(
                $"O semestre {ano}/{numero} tem os bimestres {esperado} e {esperado + 1}, " +
                $"não {primeiro.Numero} e {segundo.Numero}.");
        }

        if (segundo.Inicio <= primeiro.Fim)
        {
            throw new DomainException(
                $"No semestre {ano}/{numero}, o bimestre {segundo.Numero} começa antes de o {primeiro.Numero} terminar.");
        }

        Id = Guid.CreateVersion7();
        Ano = ano;
        Numero = numero;
        _bimestres = [primeiro, segundo];
    }

    public Guid Id { get; private set; }

    public int Ano { get; private set; }

    public int Numero { get; private set; }

    public string Codigo => $"{Ano}/{Numero}";

    public IReadOnlyList<Bimestre> Bimestres => _bimestres;

    public DateOnly Inicio => _bimestres[0].Inicio;

    public DateOnly Fim => _bimestres[^1].Fim;

    public bool Contem(DateOnly data) => data >= Inicio && data <= Fim;
}
