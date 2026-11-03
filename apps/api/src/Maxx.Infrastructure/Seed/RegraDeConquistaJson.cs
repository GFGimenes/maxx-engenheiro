using System.Text.Json;
using System.Text.Json.Nodes;
using Maxx.Domain.Catalogo;

namespace Maxx.Infrastructure.Seed;

// A regra no mesmo formato de data/catalogo/conquistas.json, para a coluna jsonb ler
// como o arquivo. O avaliador do passo 9 lê daqui.
public static class RegraDeConquistaJson
{
    public static string Escrever(RegraDeConquista regra)
    {
        ArgumentNullException.ThrowIfNull(regra);

        var json = new JsonObject { ["forma"] = NomeJson.De(regra.Forma) };

        if (regra.Forma == FormaDeContagem.Customizada)
        {
            json["implementacao"] = regra.Implementacao;
            return json.ToJsonString();
        }

        json["fonte"] = regra.Fonte is { } fonte ? NomeJson.De(fonte) : null;
        json["filtroItens"] = regra.FiltroItens is { } filtro ? new JsonArray([.. filtro.Select(codigo => JsonValue.Create(codigo))]) : null;
        json["agruparPor"] = regra.AgruparPor is { } agrupamento ? NomeJson.De(agrupamento) : null;
        json["alvo"] = regra.Alvo switch
        {
            { Fixo: { } fixo } => fixo,
            { EhTodasAsMatriculasDoPeriodo: true } => NomeJson.De(nameof(AlvoDeConquista.TodasAsMatriculasDoPeriodo)),
            _ => null,
        };

        return json.ToJsonString();
    }

    // Lê o que Escrever gravou. Formato fora disso é dado corrompido: falha alto.
    public static RegraDeConquista Ler(string texto)
    {
        var json = JsonNode.Parse(texto)?.AsObject() ?? throw new JsonException("Regra vazia.");

        return new()
        {
            Forma = Enum<FormaDeContagem>(json["forma"]) ?? throw new JsonException($"Regra sem forma: {texto}"),
            Fonte = Enum<FonteDeContagem>(json["fonte"]),
            FiltroItens = json["filtroItens"]?.AsArray().Select(codigo => codigo!.GetValue<string>()).ToList(),
            AgruparPor = Enum<Agrupamento>(json["agruparPor"]),
            Alvo = json["alvo"] switch
            {
                null => null,
                JsonValue valor when valor.TryGetValue<int>(out var fixo) => AlvoDeConquista.De(fixo),
                _ => AlvoDeConquista.TodasAsMatriculasDoPeriodo,
            },
            Implementacao = json["implementacao"]?.GetValue<string>(),
        };
    }

    private static T? Enum<T>(JsonNode? no)
        where T : struct, Enum
    {
        if (no is null)
        {
            return null;
        }

        var texto = no.GetValue<string>();
        return NomeJson.TentarLer<T>(texto, out var valor)
            ? valor
            : throw new JsonException($"Valor \"{texto}\" desconhecido para {typeof(T).Name}.");
    }
}
