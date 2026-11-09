using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Maxx.Infrastructure.Seed;

// data/demo/atividade.json (passo 12): quantos alunos têm cada medalha e cada prêmio de
// participação, os avisos e a contestação aberta. Aqui só a forma do arquivo; os itens, os
// eventos, as pessoas e as turmas citadas são conferidos pelo seeder contra o banco, e as
// regras de cada fato são as do domínio.
public static class AtividadeDemoReader
{
    public const string Arquivo = "atividade.json";

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static readonly string[] _escopos = ["faculdade", "curso", "turma"];

    public static AtividadeDemo Ler(string diretorio)
    {
        var caminho = Path.Combine(diretorio, Arquivo);
        if (!File.Exists(caminho))
        {
            throw new SeedException($"Atividade da demonstração não encontrada: {caminho}.");
        }

        AtividadeDemo atividade;
        try
        {
            atividade = JsonSerializer.Deserialize<AtividadeDemo>(File.ReadAllText(caminho), _json)
                ?? throw new SeedException($"{Arquivo}: arquivo vazio.");
        }
        catch (JsonException ex)
        {
            throw new SeedException($"{Arquivo}: {ex.Message}", ex);
        }

        var erros = new List<string>();
        foreach (var (item, alunos) in atividade.Medalhas.Concat(atividade.PremiosParticipacao))
        {
            if (alunos < 1)
            {
                erros.Add($"{item} com {alunos} alunos: o mínimo é 1 (o item que ninguém tem fica fora do arquivo).");
            }
        }

        foreach (var aviso in atividade.Avisos)
        {
            if (!_escopos.Contains(aviso.Escopo, StringComparer.Ordinal))
            {
                erros.Add($"aviso \"{aviso.Titulo}\": escopo {aviso.Escopo} não existe.");
            }

            if (!Instante(aviso.PublicadoEm, out _))
            {
                erros.Add($"aviso \"{aviso.Titulo}\": publicadoEm {aviso.PublicadoEm} fora da forma aaaa-mm-ddThh:mm.");
            }
        }

        if (atividade.Contestacao is { } contestacao
            && (!Instante(contestacao.ConcedidoEm, out var concedido) || !Instante(contestacao.ContestadaEm, out var contestada)
                || contestada <= concedido))
        {
            erros.Add("contestação: concedidoEm e contestadaEm na forma aaaa-mm-ddThh:mm, e a contestação depois da concessão.");
        }

        foreach (var pendente in atividade.ComprovacoesPendentes.Where(pendente => !Instante(pendente.ReivindicadoEm, out _)))
        {
            erros.Add($"comprovação pendente de {pendente.Ra}: reivindicadoEm {pendente.ReivindicadoEm} fora da forma aaaa-mm-ddThh:mm.");
        }

        if (atividade.Social.SeguimentosPorAluno is not [var minimo, var maximo] || minimo < 0 || maximo < minimo
            || atividade.Social.CurtidasPorPost is not [var poucas, var muitas] || poucas < 0 || muitas < poucas)
        {
            erros.Add("social: seguimentosPorAluno e curtidasPorPost são intervalos [mínimo, máximo].");
        }

        if (atividade.Legendas.Reivindicacao.Count == 0 || atividade.Legendas.Medalha.Count == 0 || atividade.Legendas.Livre.Count == 0
            || atividade.Comentarios.Count == 0)
        {
            erros.Add("legendas e comentários não podem vir vazios.");
        }

        if (erros.Count > 0)
        {
            throw new SeedException(
                $"{Arquivo} inválido, nada foi gravado:" + Environment.NewLine +
                string.Join(Environment.NewLine, erros.Select(erro => $"  {erro}")));
        }

        return atividade;
    }

    // Data e hora de São Paulo, sem fuso no texto.
    public static bool Instante(string texto, out DateTime local) =>
        DateTime.TryParseExact(texto, "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out local);
}

public sealed class AtividadeDemo
{
    public string? Observacao { get; init; }

    // Código do item e quantos alunos distintos o têm.
    public required Dictionary<string, int> Medalhas { get; init; }

    public required Dictionary<string, int> PremiosParticipacao { get; init; }

    public ContestacaoDemo? Contestacao { get; init; }

    public required List<AvisoDemo> Avisos { get; init; }

    // Reivindicações com foto ainda sem decisão: a fila de comprovações do mundo.
    public required List<ComprovacaoPendenteDemo> ComprovacoesPendentes { get; init; }

    public required PostsDemo Posts { get; init; }

    public required LegendasDemo Legendas { get; init; }

    public required SocialDemo Social { get; init; }

    public required List<string> Comentarios { get; init; }
}

public sealed class ComprovacaoPendenteDemo
{
    public required string Ra { get; init; }

    public required string Item { get; init; }

    public required string Evento { get; init; }

    public required string ReivindicadoEm { get; init; }

    public required string Legenda { get; init; }
}

// Quantos posts com foto: reivindicações aprovadas, medalhas postadas e posts livres.
public sealed class PostsDemo
{
    public required int ReivindicacoesComFoto { get; init; }

    public required int MedalhasComFoto { get; init; }

    public required int Livres { get; init; }
}

public sealed class LegendasDemo
{
    public required List<string> Reivindicacao { get; init; }

    public required List<string> Medalha { get; init; }

    public required List<string> Livre { get; init; }
}

// Intervalos [mínimo, máximo]: quantos colegas cada aluno ativo segue, quantas curtidas cada
// post com foto ganha; e quantos comentários no mundo todo.
public sealed class SocialDemo
{
    public required List<int> SeguimentosPorAluno { get; init; }

    public required List<int> CurtidasPorPost { get; init; }

    public required int Comentarios { get; init; }
}

// Um prêmio concedido direto pela instituição e contestado por um professor do aluno, ainda
// sem decisão.
public sealed class ContestacaoDemo
{
    public required string Ra { get; init; }

    public required string Item { get; init; }

    public required string Evento { get; init; }

    public required string ConcedidoEm { get; init; }

    public required string ContestadaPor { get; init; }

    public required string ContestadaEm { get; init; }

    public required string Motivo { get; init; }
}

public sealed class AvisoDemo
{
    public required string Autor { get; init; }

    public required string Escopo { get; init; }

    public List<string>? Cursos { get; init; }

    public List<TurmaDoAvisoDemo>? Turmas { get; init; }

    public required string Titulo { get; init; }

    public required string Texto { get; init; }

    public required string PublicadoEm { get; init; }
}

public sealed class TurmaDoAvisoDemo
{
    public required string Curso { get; init; }

    public required string Materia { get; init; }

    public required string Semestre { get; init; }
}
