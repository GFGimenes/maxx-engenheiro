namespace Maxx.Domain.Catalogo;

// O metal do medalhão. Define a faixa de pontos do item; não existe sub-tier.
public enum Tier
{
    Bronze,
    Prata,
    Ouro,
    Diamante,
}

public readonly record struct FaixaDePontos(int Minimo, int Maximo)
{
    public bool Contem(int pontos) => pontos >= Minimo && pontos <= Maximo;

    public override string ToString() => $"{Minimo} a {Maximo}";
}

public static class Tiers
{
    public static FaixaDePontos Faixa(Tier tier) => tier switch
    {
        Tier.Bronze => new(1, 30),
        Tier.Prata => new(31, 70),
        Tier.Ouro => new(71, 100),
        Tier.Diamante => new(101, 150),
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, null),
    };
}
