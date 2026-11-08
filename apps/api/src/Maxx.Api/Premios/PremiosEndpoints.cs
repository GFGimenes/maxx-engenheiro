using System.Security.Claims;
using Maxx.Api.Autenticacao;
using Maxx.Api.Medalhas;
using Maxx.Application.Premios;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maxx.Api.Premios;

// A tela de Troféus do próprio aluno: o catálogo com o critério e a raridade, o que falta
// e o que ele já tem. Só o dono vê os faltantes (produto.md 9.1), por isso a rota é de /eu.
internal static class PremiosEndpoints
{
    public static IEndpointRouteBuilder MapPremiosEndpoints(this IEndpointRouteBuilder v1)
    {
        v1.MapGet("/eu/premios", PremiosAsync)
            .WithName("GetMeusPremios")
            .WithTags("Premios")
            .RequireAuthorization(Politicas.Aluno)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return v1;
    }

    private static async Task<Results<Ok<PremiosDoAlunoResponse>, ForbidHttpResult>> PremiosAsync(
        ClaimsPrincipal principal, IConsultaDePremios consulta, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(MaxxClaims.Sub), out var id)
            || await consulta.DoAlunoAsync(id, cancellationToken) is not { } premios)
        {
            return TypedResults.Forbid();
        }

        return TypedResults.Ok(Mapear(premios));
    }

    // Também o perfil público usa esta forma, com só os itens obtidos: faltantes e
    // progresso são só do próprio aluno (produto.md 9.1).
    internal static PremiosDoAlunoResponse Mapear(PremiosDoAluno premios, bool soObtidos = false) =>
        new(
            premios.Pontos,
            [.. premios.Familias.Select(familia => new FamiliaResponse(familia.Codigo, familia.Nome, familia.Descricao, familia.Passos))],
            [.. premios.Itens.Where(item => !soObtidos || item.Obtidos.Count > 0).Select(item => new ItemDoAlunoResponse(
                item.Codigo,
                ProfessorEndpoints.Nome(item.Tipo),
                item.Nome,
                item.Criterio,
                ProfessorEndpoints.Nome(item.Tier),
                item.Pontos,
                item.Familia,
                item.OrdemNaFamilia,
                item.Raridade is { } raridade
                    ? new RaridadeResponse(ProfessorEndpoints.Nome(raridade.Nivel), raridade.Percentual)
                    : null,
                [.. item.Obtidos.Select(obtido => new PremioObtidoResponse(
                    obtido.Id, ProfessorEndpoints.Nome(obtido.Tier), obtido.Pontos, obtido.Materia, obtido.Bimestre, obtido.ObtidoEm))]))]);
}

public sealed record PremiosDoAlunoResponse(int Pontos, IReadOnlyList<FamiliaResponse> Familias, IReadOnlyList<ItemDoAlunoResponse> Itens);

public sealed record FamiliaResponse(string Codigo, string Nome, string Descricao, int Passos);

// Tipo, tier e raridade em camelCase, como no catálogo ("premioParticipacao", "lendario").
// Critério é a descrição do item: não há prêmio secreto (produto.md 4.5).
public sealed record ItemDoAlunoResponse(
    string Codigo,
    string Tipo,
    string Nome,
    string Criterio,
    string Tier,
    int Pontos,
    string? Familia,
    int? OrdemNaFamilia,
    RaridadeResponse? Raridade,
    IReadOnlyList<PremioObtidoResponse> Obtidos);

// Percentual dos alunos que têm o item, com uma casa: "x% dos alunos".
public sealed record RaridadeResponse(string Nivel, double Percentual);

public sealed record PremioObtidoResponse(Guid Id, string Tier, int Pontos, string? Materia, int? Bimestre, DateTimeOffset ObtidoEm);
