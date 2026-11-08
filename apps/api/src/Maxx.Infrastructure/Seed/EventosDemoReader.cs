using System.Text.Json;
using System.Text.Json.Serialization;

namespace Maxx.Infrastructure.Seed;

// data/demo/eventos.json: os eventos semeados, a que o prêmio de participação se amarra
// (produto.md 4.6). Aqui só a coerência interna do arquivo (código único). Itens e curso
// são conferidos contra o banco pelo seeder, e as regras de cada evento são as do domínio.
public static class EventosDemoReader
{
    public const string Arquivo = "eventos.json";

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static EventosDemo Ler(string diretorio)
    {
        var caminho = Path.Combine(diretorio, Arquivo);
        if (!File.Exists(caminho))
        {
            throw new SeedException($"Eventos de demonstração não encontrados: {caminho}.");
        }

        EventosDemo eventos;
        try
        {
            eventos = JsonSerializer.Deserialize<EventosDemo>(File.ReadAllText(caminho), _json)
                ?? throw new SeedException($"{Arquivo}: arquivo vazio.");
        }
        catch (JsonException ex)
        {
            throw new SeedException($"{Arquivo}: {ex.Message}", ex);
        }

        var repetidos = eventos.Eventos.GroupBy(evento => evento.Codigo, StringComparer.Ordinal)
            .Where(grupo => grupo.Count() > 1)
            .Select(grupo => grupo.Key)
            .ToList();
        if (repetidos.Count > 0)
        {
            throw new SeedException($"{Arquivo} inválido, nada foi gravado: código de evento repetido: {string.Join(", ", repetidos)}.");
        }

        return eventos;
    }
}

public sealed class EventosDemo
{
    public string? Observacao { get; init; }

    public required List<EventoDemo> Eventos { get; init; }
}

public sealed class EventoDemo
{
    public required string Codigo { get; init; }

    public required string Nome { get; init; }

    // Datas inclusivas, interpretadas em America/Sao_Paulo, como no calendário.
    public required DateOnly Inicio { get; init; }

    public required DateOnly Fim { get; init; }

    // Códigos dos prêmios de participação que o evento aceita.
    public required List<string> Itens { get; init; }

    // Sigla do curso a que o evento se restringe; nulo, aberto a todos.
    public string? Curso { get; init; }
}
