namespace Maxx.Domain.Academico;

public sealed class AnoLetivo
{
    private readonly List<SemestreLetivo> _semestres;

    // Para o EF Core materializar do banco.
    private AnoLetivo() => _semestres = [];

    public AnoLetivo(int ano, SemestreLetivo primeiro, SemestreLetivo segundo)
    {
        ArgumentNullException.ThrowIfNull(primeiro);
        ArgumentNullException.ThrowIfNull(segundo);

        if (primeiro.Ano != ano || segundo.Ano != ano)
        {
            throw new DomainException(
                $"O ano letivo {ano} recebeu os semestres {primeiro.Codigo} e {segundo.Codigo}.");
        }

        if (primeiro.Numero != 1 || segundo.Numero != 2)
        {
            throw new DomainException($"O ano letivo {ano} tem os semestres 1 e 2, nessa ordem.");
        }

        if (segundo.Inicio <= primeiro.Fim)
        {
            throw new DomainException($"O semestre {segundo.Codigo} começa antes de o {primeiro.Codigo} terminar.");
        }

        Id = Guid.CreateVersion7();
        Ano = ano;
        _semestres = [primeiro, segundo];
    }

    public Guid Id { get; private set; }

    public int Ano { get; private set; }

    // Ordenado na leitura: vindo do banco, a coleção não garante ordem.
    public IReadOnlyList<SemestreLetivo> Semestres => [.. _semestres.OrderBy(semestre => semestre.Numero)];

    public IEnumerable<Bimestre> Bimestres => Semestres.SelectMany(semestre => semestre.Bimestres);

    public DateOnly Inicio => _semestres.Min(semestre => semestre.Inicio);

    public DateOnly Fim => _semestres.Max(semestre => semestre.Fim);

    // Corrige as datas a partir de um ano já construído, e portanto já validado inteiro.
    // Os ids ficam: turmas e prêmios apontam para estes semestres e bimestres. Devolve se
    // alguma data mudou.
    public bool AtualizarDatas(AnoLetivo corrigido)
    {
        ArgumentNullException.ThrowIfNull(corrigido);

        if (corrigido.Ano != Ano)
        {
            throw new DomainException($"O ano letivo {Ano} não pode receber as datas de {corrigido.Ano}.");
        }

        var mudou = false;
        foreach (var (atual, novo) in Bimestres.Zip(corrigido.Bimestres))
        {
            mudou |= atual.Redatar(novo.Inicio, novo.Fim);
        }

        return mudou;
    }
}
