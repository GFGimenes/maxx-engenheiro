using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Premios;

namespace Maxx.Domain.Conquistas;

// Um prêmio que pode entrar na contagem de uma conquista (regras-conquista.md 3), já
// reduzido ao que o avaliador lê: o item, as instâncias de período a que pertence e os
// campos de agrupamento que o próprio prêmio carrega. Conquista nunca é fato: conquista não
// conta conquista.
//
// Só existe fato de medalha ativa e de prêmio de participação definitivo. É aqui, e não no
// avaliador, que a medalha anulada e o prêmio ainda contestável ficam de fora.
public sealed record FatoContavel
{
    public required OrigemDoFato Origem { get; init; }

    // O id da medalha ou do prêmio de participação: é o que a evidência guarda.
    public required Guid Id { get; init; }

    public required string ItemCodigo { get; init; }

    // Só na medalha (regras-conquista.md 2: matéria e bimestre vêm da medalha).
    public Guid? MateriaId { get; init; }

    public Guid? BimestreId { get; init; }

    public required Guid SemestreLetivoId { get; init; }

    public required Guid AnoLetivoId { get; init; }

    // Só no prêmio de participação: vem do item do catálogo.
    public string? TipoDeEvento { get; init; }

    // Nulo: a medalha está anulada, e deixou de ser contável no instante da anulação.
    public static FatoContavel? DaMedalha(Medalha medalha, Calendario calendario)
    {
        ArgumentNullException.ThrowIfNull(medalha);
        ArgumentNullException.ThrowIfNull(calendario);

        if (!medalha.EstaAtiva)
        {
            return null;
        }

        var ano = calendario.AnoDoSemestre(medalha.SemestreLetivoId)
            ?? throw new DomainException($"A medalha {medalha.Id} aponta para um semestre fora do calendário.");

        return new FatoContavel
        {
            Origem = OrigemDoFato.Medalha,
            Id = medalha.Id,
            ItemCodigo = medalha.ItemCodigo,
            MateriaId = medalha.MateriaId,
            BimestreId = medalha.BimestreId,
            SemestreLetivoId = medalha.SemestreLetivoId,
            AnoLetivoId = ano.Id,
        };
    }

    // Nulo: o prêmio ainda não é definitivo (regras-conquista.md 3), ou o calendário ainda
    // não tem o semestre em que ele conta. O prêmio conta no semestre que contém a data de
    // início do evento; no recesso, no seguinte (produto.md 4.9).
    public static FatoContavel? DoPremio(
        PremioParticipacao premio, Evento evento, ItemPremioParticipacao item, Calendario calendario)
    {
        ArgumentNullException.ThrowIfNull(premio);
        ArgumentNullException.ThrowIfNull(evento);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(calendario);

        if (premio.EventoId != evento.Id)
        {
            throw new ArgumentException("O evento não é o do prêmio.", nameof(evento));
        }

        if (premio.ItemCodigo != item.Codigo)
        {
            throw new ArgumentException("O item não é o do prêmio.", nameof(item));
        }

        if (premio.Estado != EstadoDoPremioParticipacao.Definitivo)
        {
            return null;
        }

        if (calendario.SemestreDoEvento(evento.Inicio) is not { } semestre)
        {
            return null;
        }

        return new FatoContavel
        {
            Origem = OrigemDoFato.PremioParticipacao,
            Id = premio.Id,
            ItemCodigo = premio.ItemCodigo,
            SemestreLetivoId = semestre.Id,
            AnoLetivoId = calendario.AnoDoSemestre(semestre.Id)!.Id,
            TipoDeEvento = item.TipoDeEvento,
        };
    }

    // A instância do período a que este fato pertence, para o aluno dono dele. Nula: o fato
    // não tem esse período (o prêmio de participação não tem bimestre).
    public InstanciaDePeriodo? InstanciaEm(Periodo periodo, Guid alunoId) => periodo switch
    {
        Periodo.Bimestre => BimestreId is { } bimestre ? new InstanciaDePeriodo(periodo, bimestre) : null,
        Periodo.Semestre => new InstanciaDePeriodo(periodo, SemestreLetivoId),
        Periodo.Ano => new InstanciaDePeriodo(periodo, AnoLetivoId),
        Periodo.Curso => new InstanciaDePeriodo(periodo, alunoId),
        _ => null,
    };

    // A contagem nunca atravessa a instância. No período curso, a instância é o aluno: todo
    // fato dele pertence a ela, e quem chama só passa fatos do aluno.
    public bool PertenceA(InstanciaDePeriodo instancia)
    {
        ArgumentNullException.ThrowIfNull(instancia);

        return instancia.Tipo switch
        {
            Periodo.Bimestre => BimestreId == instancia.Referencia,
            Periodo.Semestre => SemestreLetivoId == instancia.Referencia,
            Periodo.Ano => AnoLetivoId == instancia.Referencia,
            Periodo.Curso => true,
            _ => false,
        };
    }
}

public enum OrigemDoFato
{
    Medalha,
    PremioParticipacao,
}
