using System.Text.Json;
using System.Text.Json.Serialization;
using Maxx.Domain.Fotos;

namespace Maxx.Infrastructure.Seed;

// data/demo/fotos/ (passo 12): as fotos dos posts do mundo de demonstração, já nas duas
// derivadas do app (ADR 0003), e fotos.json com os cursos em que cada uma cabe e a
// procedência, que CREDITOS.md repete para quem lê. Fotos de banco livre, sem rosto
// identificável (P7). Aqui também o limite de tamanho das derivadas, o mesmo do upload.
public static class FotosDemoReader
{
    public const string Diretorio = "fotos";
    public const string Arquivo = "fotos.json";
    public const string Todos = "todos";

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static List<FotoDemo> Ler(string diretorioDemo)
    {
        var diretorio = Path.Combine(diretorioDemo, Diretorio);
        var caminho = Path.Combine(diretorio, Arquivo);
        if (!File.Exists(caminho))
        {
            throw new SeedException($"Fotos da demonstração não encontradas: {caminho}.");
        }

        ListaDeFotos lista;
        try
        {
            lista = JsonSerializer.Deserialize<ListaDeFotos>(File.ReadAllText(caminho), _json)
                ?? throw new SeedException($"{Arquivo}: arquivo vazio.");
        }
        catch (JsonException ex)
        {
            throw new SeedException($"{Arquivo}: {ex.Message}", ex);
        }

        var fotos = new List<FotoDemo>();
        var erros = new List<string>();
        foreach (var foto in lista.Fotos)
        {
            var feed = Path.Combine(diretorio, $"{foto.Nome}.feed.jpg");
            var grade = Path.Combine(diretorio, $"{foto.Nome}.grade.jpg");
            if (!File.Exists(feed) || !File.Exists(grade))
            {
                erros.Add($"{foto.Nome}: faltam as derivadas {foto.Nome}.feed.jpg e {foto.Nome}.grade.jpg.");
                continue;
            }

            var bytesDoFeed = File.ReadAllBytes(feed);
            var bytesDaGrade = File.ReadAllBytes(grade);
            if (bytesDoFeed.Length > UploadPendente.TamanhoMaximoDoFeed || bytesDaGrade.Length > UploadPendente.TamanhoMaximoDaGrade)
            {
                erros.Add($"{foto.Nome}: derivada acima do limite do upload.");
                continue;
            }

            if (foto.Cursos.Count == 0 || string.IsNullOrWhiteSpace(foto.Autor) || string.IsNullOrWhiteSpace(foto.Licenca)
                || !Uri.TryCreate(foto.Fonte, UriKind.Absolute, out _))
            {
                erros.Add($"{foto.Nome}: cursos, autor, licença e fonte são obrigatórios.");
                continue;
            }

            fotos.Add(foto with { Feed = bytesDoFeed, Grade = bytesDaGrade });
        }

        if (fotos.Select(foto => foto.Nome).Distinct(StringComparer.Ordinal).Count() != fotos.Count)
        {
            erros.Add("nome de foto repetido.");
        }

        if (erros.Count > 0 || fotos.Count == 0)
        {
            throw new SeedException(
                $"{Arquivo} inválido, nada foi gravado:" + Environment.NewLine +
                string.Join(Environment.NewLine, erros.DefaultIfEmpty("nenhuma foto.").Select(erro => $"  {erro}")));
        }

        return fotos;
    }

    private sealed class ListaDeFotos
    {
        public required List<FotoDemo> Fotos { get; init; }
    }
}

// Cursos: as siglas em que a foto cabe, ou "todos".
public sealed record FotoDemo
{
    public required string Nome { get; init; }

    public required List<string> Cursos { get; init; }

    public required string Titulo { get; init; }

    public required string Autor { get; init; }

    public required string Licenca { get; init; }

    public required string Fonte { get; init; }

    [JsonIgnore]
    public byte[] Feed { get; init; } = [];

    [JsonIgnore]
    public byte[] Grade { get; init; } = [];

    public bool CabeEm(string sigla) => Cursos.Contains(FotosDemoReader.Todos, StringComparer.Ordinal) || Cursos.Contains(sigla, StringComparer.Ordinal);
}
