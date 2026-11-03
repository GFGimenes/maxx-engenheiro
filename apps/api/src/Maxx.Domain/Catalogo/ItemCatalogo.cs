namespace Maxx.Domain.Catalogo;

// Um item do catálogo, como está em data/catalogo/. Tier e pontos são fixos: quem
// concede escolhe o item, ninguém digita valor. A raridade não está aqui: é derivada da
// escassez pelo worker.
public abstract record ItemCatalogo
{
    public required string Codigo { get; init; }

    public required string Nome { get; init; }

    public required string Descricao { get; init; }

    public required Tier Tier { get; init; }

    public required int Pontos { get; init; }

    public string? Familia { get; init; }

    public int? OrdemNaFamilia { get; init; }

    public string? RestricaoCurso { get; init; }

    public Vigencia Vigencia { get; init; } = Vigencia.Sempre;

    public abstract TipoDeItem Tipo { get; }
}

public sealed record Vigencia(DateOnly? Inicio, DateOnly? Fim)
{
    public static Vigencia Sempre { get; } = new(null, null);
}

public sealed record ItemMedalha : ItemCatalogo
{
    public override TipoDeItem Tipo => TipoDeItem.Medalha;

    public required BaseDaMedalha Base { get; init; }

    public required Periodo PeriodoDeReferencia { get; init; }

    public required bool Repetivel { get; init; }

    // Quantas vezes por aluno, matéria e período de referência.
    public required int LimitePorPeriodo { get; init; }

    public required IReadOnlyList<CampoDeDeduplicacao> ChaveDeDeduplicacao { get; init; }
}

public sealed record ItemPremioParticipacao : ItemCatalogo
{
    public override TipoDeItem Tipo => TipoDeItem.PremioParticipacao;

    public required string TipoDeEvento { get; init; }

    public required FormaDoEvento FormaDoEvento { get; init; }

    public required bool ExigeFoto { get; init; }

    public Periodo PeriodoDeReferencia { get; init; } = Periodo.Evento;

    public required IReadOnlyList<CampoDeDeduplicacao> ChaveDeDeduplicacao { get; init; }
}

public sealed record ItemConquista : ItemCatalogo
{
    public override TipoDeItem Tipo => TipoDeItem.Conquista;

    public required Periodo Periodo { get; init; }

    public required Gatilho Gatilho { get; init; }

    public required RegraDeConquista Regra { get; init; }
}
