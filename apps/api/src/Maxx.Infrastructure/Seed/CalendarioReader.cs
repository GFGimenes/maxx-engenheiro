using System.Text.Json;
using System.Text.Json.Serialization;
using Maxx.Domain;
using Maxx.Domain.Academico;

namespace Maxx.Infrastructure.Seed;

// Lê data/calendario/*.json, um arquivo por ano letivo. A validação é a do domínio: o
// calendário só existe se os construtores de AnoLetivo e Calendario aceitarem.
public static class CalendarioReader
{
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static Calendario Ler(string diretorio)
    {
        if (!Directory.Exists(diretorio))
        {
            throw new SeedException($"Diretório do calendário não encontrado: {diretorio}.");
        }

        var anos = Directory.GetFiles(diretorio, "*.json")
            .Order(StringComparer.Ordinal)
            .Select(LerAno)
            .ToList();

        if (anos.Count == 0)
        {
            throw new SeedException($"Nenhum ano letivo em {diretorio}.");
        }

        try
        {
            return new Calendario(anos);
        }
        catch (DomainException ex)
        {
            throw new SeedException($"Calendário inválido: {ex.Message}", ex);
        }
    }

    private static AnoLetivo LerAno(string caminho)
    {
        var arquivo = Path.GetFileName(caminho);
        try
        {
            var json = JsonSerializer.Deserialize<AnoJson>(File.ReadAllText(caminho), _json)
                ?? throw new SeedException($"{arquivo}: arquivo vazio.");

            if (json.Semestres is not [var primeiro, var segundo])
            {
                throw new SeedException($"{arquivo}: o ano letivo tem dois semestres.");
            }

            return new AnoLetivo(json.Ano, Semestre(json.Ano, primeiro), Semestre(json.Ano, segundo));
        }
        catch (JsonException ex)
        {
            throw new SeedException($"{arquivo}: {ex.Message}", ex);
        }
        catch (DomainException ex)
        {
            throw new SeedException($"{arquivo}: {ex.Message}", ex);
        }
    }

    private static SemestreLetivo Semestre(int ano, SemestreJson json)
    {
        if (json.Bimestres is not [var primeiro, var segundo])
        {
            throw new DomainException($"O semestre {ano}/{json.Numero} tem dois bimestres.");
        }

        return new SemestreLetivo(
            ano,
            json.Numero,
            new Bimestre(primeiro.Numero, primeiro.Inicio, primeiro.Fim),
            new Bimestre(segundo.Numero, segundo.Inicio, segundo.Fim));
    }

    private sealed class AnoJson
    {
        public required int Ano { get; init; }

        public string? Observacao { get; init; }

        public required List<SemestreJson> Semestres { get; init; }
    }

    private sealed class SemestreJson
    {
        public required int Numero { get; init; }

        public required List<BimestreJson> Bimestres { get; init; }
    }

    private sealed class BimestreJson
    {
        public required int Numero { get; init; }

        public required DateOnly Inicio { get; init; }

        public required DateOnly Fim { get; init; }
    }
}
