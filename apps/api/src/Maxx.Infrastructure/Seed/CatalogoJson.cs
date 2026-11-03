using System.Text.Json;

namespace Maxx.Infrastructure.Seed;

// A forma dos arquivos de data/catalogo/. Tudo opcional aqui: a falta de um campo vira
// erro com o código do item no CatalogoReader, em vez de uma exceção genérica.
public static partial class CatalogoReader
{
    private sealed class ArquivoJson<T>
    {
        public int? Versao { get; init; }

        public string? Tipo { get; init; }

        public string? Observacao { get; init; }

        public string? RegraDeRepeticao { get; init; }

        public string? RegraDeDeduplicacao { get; init; }

        public List<T>? Itens { get; init; }
    }

    private sealed class ItemJson
    {
        public string? Codigo { get; init; }

        public string? Nome { get; init; }

        public string? Descricao { get; init; }

        public string? Tipo { get; init; }

        public string? Tier { get; init; }

        public int? Pontos { get; init; }

        public string? RestricaoCurso { get; init; }

        public VigenciaJson? Vigencia { get; init; }

        public string? Familia { get; init; }

        public int? OrdemNaFamilia { get; init; }

        // Medalha
        public string? Base { get; init; }

        public string? PeriodoDeReferencia { get; init; }

        public bool? Repetivel { get; init; }

        public int? LimitePorPeriodo { get; init; }

        public List<string>? ChaveDeDeduplicacao { get; init; }

        // Prêmio de participação
        public string? TipoDeEvento { get; init; }

        public string? FormaDoEvento { get; init; }

        public bool? ExigeFoto { get; init; }

        // Conquista
        public string? Periodo { get; init; }

        public string? Gatilho { get; init; }

        public RegraJson? Regra { get; init; }
    }

    private sealed class VigenciaJson
    {
        public DateOnly? Inicio { get; init; }

        public DateOnly? Fim { get; init; }
    }

    private sealed class RegraJson
    {
        public string? Forma { get; init; }

        public string? Fonte { get; init; }

        public List<string>? FiltroItens { get; init; }

        public string? AgruparPor { get; init; }

        // Inteiro ou "todasAsMatriculasDoPeriodo".
        public JsonElement? Alvo { get; init; }

        public string? Implementacao { get; init; }
    }

    private sealed class FamiliaJson
    {
        public string? Codigo { get; init; }

        public string? Nome { get; init; }

        public string? Descricao { get; init; }

        public int? Passos { get; init; }
    }
}
