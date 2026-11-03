namespace Maxx.Infrastructure.Persistence;

// Uma linha de item_catalogo: os três tipos numa tabela só, com coluna de tipo. Campos
// comuns sempre preenchidos; os específicos de cada tipo, nulos nos outros (uma check
// constraint cobra isso). Enumerações em texto camelCase, como no JSON de origem.
//
// Não é o modelo do domínio: os itens do domínio são imutáveis, e a versão é assunto do
// seeder (sobe quando o conteúdo muda; ADR 0007).
public sealed class ItemCatalogoRow
{
    public required string Codigo { get; set; }

    public required string Tipo { get; set; }

    public int Versao { get; set; } = 1;

    public required string Nome { get; set; }

    public required string Descricao { get; set; }

    public required string Tier { get; set; }

    public int Pontos { get; set; }

    // periodoDeReferencia na medalha e no prêmio de participação; periodo na conquista.
    public required string Periodo { get; set; }

    public string? FamiliaCodigo { get; set; }

    public int? OrdemNaFamilia { get; set; }

    public string? RestricaoCurso { get; set; }

    public DateOnly? VigenciaInicio { get; set; }

    public DateOnly? VigenciaFim { get; set; }

    // Medalha e prêmio de participação.
    public string[]? ChaveDeDeduplicacao { get; set; }

    // Medalha.
    public string? Base { get; set; }

    public bool? Repetivel { get; set; }

    public int? LimitePorPeriodo { get; set; }

    // Prêmio de participação.
    public string? TipoDeEvento { get; set; }

    public string? FormaDoEvento { get; set; }

    public bool? ExigeFoto { get; set; }

    // Conquista. A regra é dado que o avaliador interpreta: jsonb, no formato do catálogo.
    public string? Gatilho { get; set; }

    public string? Regra { get; set; }
}

public sealed class FamiliaRow
{
    public required string Codigo { get; set; }

    public required string Nome { get; set; }

    public required string Descricao { get; set; }

    public int Passos { get; set; }
}
