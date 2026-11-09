using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Premios;

namespace Maxx.Domain.Ranking;

// Um prêmio que pontua, já reduzido ao que o ranking lê (produto.md 5): de quem é, quantos
// pontos vale (congelados na concessão, ADR 0007), em que semestre letivo conta e o
// instante da concessão, que é o desempate.
//
// É aqui, e não no cálculo, que fica de fora o que não pontua: a medalha anulada, o prêmio
// de participação reivindicado, reprovado, em averiguação ou revogado, e a conquista
// removida.
public sealed record ParcelaDoRanking
{
    public required Guid AlunoId { get; init; }

    public required int Pontos { get; init; }

    // Nulo: o calendário ainda não tem o semestre em que a parcela conta. Ela entra no
    // histórico e em nenhum semestre, até o calendário ganhar esse semestre.
    public required Guid? SemestreLetivoId { get; init; }

    // O instante da concessão: da medalha, da aprovação do prêmio de participação, da
    // conquista. A contestação confirmada não muda o instante do prêmio.
    public required DateTimeOffset ConcedidaEm { get; init; }

    // A medalha conta no semestre em que foi concedida.
    public static ParcelaDoRanking? DaMedalha(Medalha medalha)
    {
        ArgumentNullException.ThrowIfNull(medalha);

        return medalha.EstaAtiva
            ? new ParcelaDoRanking
            {
                AlunoId = medalha.AlunoId,
                Pontos = medalha.Pontos,
                SemestreLetivoId = medalha.SemestreLetivoId,
                ConcedidaEm = medalha.ConcedidaEm,
            }
            : null;
    }

    // O prêmio de participação conta no semestre do evento, não no da aprovação: o que
    // contém a data de início; no recesso, o seguinte (produto.md 4.9).
    public static ParcelaDoRanking? DoPremio(PremioParticipacao premio, Evento evento, Calendario calendario)
    {
        ArgumentNullException.ThrowIfNull(premio);
        ArgumentNullException.ThrowIfNull(evento);
        ArgumentNullException.ThrowIfNull(calendario);

        if (premio.EventoId != evento.Id)
        {
            throw new ArgumentException("O evento não é o do prêmio.", nameof(evento));
        }

        if (!premio.Pontua)
        {
            return null;
        }

        return new ParcelaDoRanking
        {
            AlunoId = premio.AlunoId,
            Pontos = premio.Pontos,
            SemestreLetivoId = calendario.SemestreDoEvento(evento.Inicio)?.Id,
            ConcedidaEm = premio.AprovadoEm
                ?? throw new DomainException($"O prêmio {premio.Id} pontua sem ter sido aprovado."),
        };
    }

    // A conquista conta no semestre da instância dela (produto.md 4.9). A de bimestre, no
    // semestre do bimestre; a de ano, no segundo semestre do ano, o mesmo que decide o
    // congelamento (regras-conquista.md 5). A de curso não tem fronteira de data: conta no
    // semestre da concessão, e a concedida no recesso, no seguinte, como o evento.
    public static ParcelaDoRanking? DaConquista(Conquista conquista, Calendario calendario)
    {
        ArgumentNullException.ThrowIfNull(conquista);
        ArgumentNullException.ThrowIfNull(calendario);

        if (!conquista.EstaAtiva)
        {
            return null;
        }

        var semestre = conquista.TipoDePeriodo == Periodo.Curso
            ? calendario.SemestreDoEvento(FusoHorario.DataLocal(conquista.ConcedidaEm))
            : conquista.Instancia.Semestres(calendario).MaxBy(candidato => candidato.Fim);

        return new ParcelaDoRanking
        {
            AlunoId = conquista.AlunoId,
            Pontos = conquista.Pontos,
            SemestreLetivoId = semestre?.Id,
            ConcedidaEm = conquista.ConcedidaEm,
        };
    }
}
