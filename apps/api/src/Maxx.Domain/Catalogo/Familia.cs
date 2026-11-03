namespace Maxx.Domain.Catalogo;

// A trilha do design: itens em ordem, o próximo em cadeado. Não concede nada, não pontua
// e não entra em contagem de conquista.
public sealed record Familia
{
    public required string Codigo { get; init; }

    public required string Nome { get; init; }

    public required string Descricao { get; init; }

    public required int Passos { get; init; }
}

public sealed record Catalogo(IReadOnlyList<ItemCatalogo> Itens, IReadOnlyList<Familia> Familias);
