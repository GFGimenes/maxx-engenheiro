using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Maxx.Api.Posts;
using Maxx.Application;
using Maxx.Application.Fotos;
using Maxx.Application.Ranking;
using Maxx.Domain.Ranking;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maxx.Api.Ranking;

// O ranking (produto.md 5): quatro visões, pelo escopo e pelo período, com o topo por cursor
// e a linha do próprio aluno à parte, para o card "Sua posição". A leitura só lê a tabela do
// worker (ADR 0005): a posição pode estar alguns segundos atrás dos pontos.
internal static class RankingEndpoints
{
    public const string Geral = "geral";
    public const string Curso = "curso";
    public const string Semestre = "semestre";
    public const string Historico = "historico";

    public const int LinhasPorPagina = 20;

    public static IEndpointRouteBuilder MapRankingEndpoints(this IEndpointRouteBuilder v1)
    {
        v1.MapGet("/rankings", RankingAsync)
            .WithName("GetRanking")
            .WithTags("Ranking")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return v1;
    }

    // seguidos: "Quem eu sigo", a mesma visão filtrada, com a posição de todos.
    private static async Task<Ok<RankingResponse>> RankingAsync(
        [Required, AllowedValues(Geral, Curso)] string? escopo,
        [Required, AllowedValues(Semestre, Historico)] string? periodo,
        bool? seguidos,
        string? cursor,
        ClaimsPrincipal principal,
        LeituraDoRankingHandler handler,
        IArmazenamentoDeFotos armazenamento,
        CancellationToken cancellationToken)
    {
        // Os valores errados a validação recusa; a ausência, não, porque o parâmetro anulável é
        // o que deixa a falta virar 400 em vez de erro de vínculo.
        if (escopo is null || periodo is null)
        {
            throw new RecusaException(TipoDeRecusa.Invalido, CodigosDoRanking.VisaoIncompleta, "Ranking sem escopo ou período.");
        }

        var ranking = await handler.LerAsync(
            PostEndpoints.UsuarioId(principal),
            escopo == Curso ? EscopoDoRanking.Curso : EscopoDoRanking.Geral,
            periodo == Historico ? PeriodoDoRanking.Historico : PeriodoDoRanking.Semestre,
            seguidos ?? false,
            cursor,
            LinhasPorPagina,
            cancellationToken);
        return TypedResults.Ok(RankingResponse.De(ranking, armazenamento));
    }
}

// Semestre: o código do semestre atual ("2026/2"), só no período semestre; nulo também antes
// do primeiro semestre do calendário. Curso: o do leitor, só no escopo curso. Eu: a linha do
// próprio aluno, nula para quem não é aluno e para o aluno sem pontos na visão ("Sem posição
// ainda").
public sealed record RankingResponse(
    string Escopo,
    string Periodo,
    string? Semestre,
    string? Curso,
    string? NomeDoCurso,
    LinhaDoRankingResponse? Eu,
    IReadOnlyList<LinhaDoRankingResponse> Itens,
    string? Proximo)
{
    public static RankingResponse De(RankingLido ranking, IArmazenamentoDeFotos armazenamento)
    {
        ArgumentNullException.ThrowIfNull(ranking);

        return new(
            ranking.Escopo == EscopoDoRanking.Curso ? RankingEndpoints.Curso : RankingEndpoints.Geral,
            ranking.Periodo == PeriodoDoRanking.Historico ? RankingEndpoints.Historico : RankingEndpoints.Semestre,
            ranking.Semestre,
            ranking.Curso,
            ranking.NomeDoCurso,
            ranking.Eu is { } eu ? LinhaDoRankingResponse.De(eu, armazenamento) : null,
            [.. ranking.Pagina.Itens.Select(linha => LinhaDoRankingResponse.De(linha, armazenamento))],
            ranking.Pagina.Proximo);
    }
}

// Curso pela sigla e semestre do aluno, como na linha do autor no feed; a foto é a derivada da
// grade. O ex-aluno aparece só no histórico, com a marca.
public sealed record LinhaDoRankingResponse(
    Guid AlunoId,
    int Posicao,
    int Pontos,
    string NomeDeExibicao,
    string Arroba,
    string? Curso,
    string? NomeDoCurso,
    int? Semestre,
    bool ExAluno,
    string? Foto)
{
    public static LinhaDoRankingResponse De(LinhaLida linha, IArmazenamentoDeFotos armazenamento)
    {
        ArgumentNullException.ThrowIfNull(linha);

        return new(
            linha.AlunoId,
            linha.Posicao,
            linha.Pontos,
            linha.NomeDeExibicao,
            linha.Arroba,
            linha.Curso,
            linha.NomeDoCurso,
            linha.Semestre,
            linha.ExAluno,
            PostResponse.Leitura(armazenamento, linha.ChaveDaFotoDaGrade));
    }
}
