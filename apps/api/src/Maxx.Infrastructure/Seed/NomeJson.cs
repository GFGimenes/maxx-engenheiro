using System.Text.Json;

namespace Maxx.Infrastructure.Seed;

// O catálogo escreve enumerações em camelCase ("premioParticipacao", "porFato"). O banco
// guarda o mesmo texto, para uma linha de item_catalogo ler como o JSON de origem.
internal static class NomeJson
{
    public static string De<T>(T valor)
        where T : struct, Enum => De(valor.ToString());

    public static string De(string nome) => JsonNamingPolicy.CamelCase.ConvertName(nome);

    public static bool TentarLer<T>(string? texto, out T valor)
        where T : struct, Enum
    {
        foreach (var candidato in Enum.GetValues<T>())
        {
            if (De(candidato) == texto)
            {
                valor = candidato;
                return true;
            }
        }

        valor = default;
        return false;
    }
}
