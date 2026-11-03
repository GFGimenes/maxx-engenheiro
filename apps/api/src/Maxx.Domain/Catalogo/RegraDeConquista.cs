namespace Maxx.Domain.Catalogo;

// A regra declarativa de docs/regras-conquista.md. É dado, não código: um avaliador só a
// executa (passo 9).
public sealed record RegraDeConquista
{
    public required FormaDeContagem Forma { get; init; }

    // Nulos só na forma customizada, que delega tudo à implementação.
    public FonteDeContagem? Fonte { get; init; }

    public AlvoDeConquista? Alvo { get; init; }

    // Nulo: qualquer item da fonte.
    public IReadOnlyList<string>? FiltroItens { get; init; }

    public Agrupamento? AgruparPor { get; init; }

    public string? Implementacao { get; init; }
}

public enum FormaDeContagem
{
    Contagem,
    ContagemDistinta,
    Customizada,
}

public enum FonteDeContagem
{
    Medalha,
    PremioParticipacao,
    Ambos,
}

// Só campos que o próprio prêmio carrega: matéria e bimestre vêm da medalha; tipo de
// evento, do item do prêmio de participação.
public enum Agrupamento
{
    Materia,
    Bimestre,
    TipoDeEvento,
}

// Um número fixo, ou a quantidade de matrículas do aluno no período.
public sealed record AlvoDeConquista
{
    private AlvoDeConquista(int? fixo) => Fixo = fixo;

    public int? Fixo { get; }

    public bool EhTodasAsMatriculasDoPeriodo => Fixo is null;

    public static AlvoDeConquista TodasAsMatriculasDoPeriodo { get; } = new(fixo: null);

    public static AlvoDeConquista De(int fixo) => new(fixo);
}
