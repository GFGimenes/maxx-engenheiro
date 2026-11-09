namespace Maxx.Domain.Academico;

// Responde em qual bimestre e semestre cai um instante. Fora deles (férias, recesso),
// a resposta é nula: nenhum fato acadêmico acontece ali.
public sealed class Calendario
{
    private readonly List<AnoLetivo> _anos;

    public Calendario(IEnumerable<AnoLetivo> anos)
    {
        ArgumentNullException.ThrowIfNull(anos);

        _anos = [.. anos.OrderBy(ano => ano.Ano)];
        for (var i = 1; i < _anos.Count; i++)
        {
            if (_anos[i].Ano == _anos[i - 1].Ano)
            {
                throw new DomainException($"O ano letivo {_anos[i].Ano} aparece duas vezes no calendário.");
            }

            if (_anos[i].Inicio <= _anos[i - 1].Fim)
            {
                throw new DomainException(
                    $"O ano letivo {_anos[i].Ano} começa antes de o {_anos[i - 1].Ano} terminar.");
            }
        }
    }

    public IReadOnlyList<AnoLetivo> Anos => _anos;

    public SemestreLetivo? SemestreEm(DateTimeOffset instante)
    {
        var data = FusoHorario.DataLocal(instante);
        return _anos.SelectMany(ano => ano.Semestres).FirstOrDefault(semestre => semestre.Contem(data));
    }

    public Bimestre? BimestreEm(DateTimeOffset instante)
    {
        var data = FusoHorario.DataLocal(instante);
        return _anos.SelectMany(ano => ano.Bimestres).FirstOrDefault(bimestre => bimestre.Contem(data));
    }

    // O semestre em que conta um prêmio de participação (produto.md 4.9): o que contém a data
    // de início do evento; no recesso, o semestre seguinte. Nulo quando o calendário ainda não
    // tem o semestre seguinte.
    public SemestreLetivo? SemestreDoEvento(DateOnly inicioDoEvento) =>
        _anos.SelectMany(ano => ano.Semestres).FirstOrDefault(semestre => semestre.Fim >= inicioDoEvento);

    public AnoLetivo? AnoDoSemestre(Guid semestreId) =>
        _anos.FirstOrDefault(ano => ano.Semestres.Any(semestre => semestre.Id == semestreId));
}
