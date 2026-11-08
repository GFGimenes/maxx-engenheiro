using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;

namespace Maxx.Domain.Conquistas;

// Uma instância do período de uma conquista (regras-conquista.md 4): um bimestre, um
// semestre letivo, um ano letivo ou, no período curso, o próprio aluno. A referência é o id
// do bimestre, do semestre, do ano ou do aluno. A conquista é concedida no máximo uma vez
// por instância, e a contagem nunca atravessa a instância.
public sealed record InstanciaDePeriodo
{
    public InstanciaDePeriodo(Periodo tipo, Guid referencia)
    {
        if (tipo == Periodo.Evento)
        {
            throw new DomainException("Conquista é de bimestre, semestre, ano ou curso, não de evento.");
        }

        if (referencia == Guid.Empty)
        {
            throw new DomainException($"Instância de {tipo} sem referência.");
        }

        Tipo = tipo;
        Referencia = referencia;
    }

    public Periodo Tipo { get; }

    public Guid Referencia { get; }

    // O último dia da instância, em America/Sao_Paulo. Nulo no período curso, que não tem
    // fronteira de data, e na referência que o calendário não conhece.
    public DateOnly? Fim(Calendario calendario)
    {
        ArgumentNullException.ThrowIfNull(calendario);

        return Tipo switch
        {
            Periodo.Bimestre => calendario.Anos.SelectMany(ano => ano.Bimestres).FirstOrDefault(b => b.Id == Referencia)?.Fim,
            Periodo.Semestre => calendario.Anos.SelectMany(ano => ano.Semestres).FirstOrDefault(s => s.Id == Referencia)?.Fim,
            Periodo.Ano => calendario.Anos.FirstOrDefault(ano => ano.Id == Referencia)?.Fim,
            _ => null,
        };
    }

    // Os semestres letivos que a instância cobre: é neles que se procuram as matrículas do
    // alvo dinâmico. O bimestre está num semestre só; o ano, nos dois. O período curso não
    // tem alvo dinâmico, e a lista sai vazia.
    public IReadOnlyList<SemestreLetivo> Semestres(Calendario calendario)
    {
        ArgumentNullException.ThrowIfNull(calendario);

        var semestres = calendario.Anos.SelectMany(ano => ano.Semestres);
        return Tipo switch
        {
            Periodo.Bimestre => [.. semestres.Where(semestre => semestre.Bimestres.Any(b => b.Id == Referencia))],
            Periodo.Semestre => [.. semestres.Where(semestre => semestre.Id == Referencia)],
            Periodo.Ano => [.. calendario.Anos.Where(ano => ano.Id == Referencia).SelectMany(ano => ano.Semestres)],
            _ => [],
        };
    }
}
