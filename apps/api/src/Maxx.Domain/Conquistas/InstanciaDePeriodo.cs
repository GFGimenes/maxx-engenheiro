using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;

namespace Maxx.Domain.Conquistas;

// Uma instância do período de uma conquista (regras-conquista.md 4): um bimestre, um
// semestre letivo, um ano letivo ou, no período curso, o próprio aluno. A referência é o id
// do bimestre, do semestre, do ano ou do aluno. A conquista é concedida no máximo uma vez
// por instância, e a contagem nunca atravessa a instância.
public sealed record InstanciaDePeriodo
{
    // O ranking do semestre congela 30 dias depois do fim do semestre (produto.md 5), e é
    // até ali que a conquista de fechamento é reavaliada (regras-conquista.md 5).
    public const int DiasAteOCongelamento = 30;

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

    // As instâncias que já encerraram em hoje (America/Sao_Paulo): a instância fecha à
    // meia-noite do dia seguinte ao fim. Bimestres, depois semestres, depois anos. O período
    // curso nunca fecha.
    public static IReadOnlyList<InstanciaDePeriodo> EncerradasAte(Calendario calendario, DateOnly hoje)
    {
        ArgumentNullException.ThrowIfNull(calendario);

        return
        [
            .. calendario.Anos.SelectMany(ano => ano.Bimestres)
                .Where(bimestre => bimestre.Fim < hoje)
                .Select(bimestre => new InstanciaDePeriodo(Periodo.Bimestre, bimestre.Id)),
            .. calendario.Anos.SelectMany(ano => ano.Semestres)
                .Where(semestre => semestre.Fim < hoje)
                .Select(semestre => new InstanciaDePeriodo(Periodo.Semestre, semestre.Id)),
            .. calendario.Anos
                .Where(ano => ano.Fim < hoje)
                .Select(ano => new InstanciaDePeriodo(Periodo.Ano, ano.Id)),
        ];
    }

    // O dia em que a instância congela: 30 dias depois do fim do último semestre que ela
    // cobre (o do bimestre, o próprio semestre, o segundo do ano). Daquele dia em diante, um
    // fato que chega já não reavalia a conquista de fechamento. Nulo no período curso.
    public DateOnly? Congelamento(Calendario calendario)
    {
        var semestres = Semestres(calendario);
        return semestres.Count == 0 ? null : semestres.Max(semestre => semestre.Fim).AddDays(DiasAteOCongelamento);
    }

    // Fechada e ainda antes do congelamento: o fato que chega agora ainda concede a
    // conquista de fechamento (D8 do passo 9). Quem sabe se a instância já fechou é o
    // registro do fechamento, não a data: o fechamento é um ato do worker.
    public bool AceitaReavaliacaoDeFechamento(Calendario calendario, DateOnly hoje) =>
        Congelamento(calendario) is { } congelamento && hoje < congelamento;

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
