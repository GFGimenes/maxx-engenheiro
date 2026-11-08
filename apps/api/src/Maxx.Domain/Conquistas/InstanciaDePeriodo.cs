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
}
