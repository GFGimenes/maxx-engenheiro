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
                    obtido.Id,
                    ProfessorEndpoints.Nome(obtido.Tier),
                    obtido.Pontos,
                    obtido.Materia,
                    obtido.Bimestre,
                    obtido.ObtidoEm,
                    obtido.PostId,
                    obtido.Instancia is { } instancia
                        ? new InstanciaResponse(ProfessorEndpoints.Nome(instancia.Periodo), instancia.Ano, instancia.Semestre, instancia.Bimestre)
                        : null))],
                item.ExigeMateria,
                !soObtidos && item.Progresso is { } progresso
                    ? new ProgressoResponse(
                        ProfessorEndpoints.Nome(progresso.Periodo), progresso.Contagem, progresso.Alvo, progresso.EmMaterias ? "materias" : null)
                    : null))]);
}

public sealed record PremiosDoAlunoResponse(int Pontos, IReadOnlyList<FamiliaResponse> Familias, IReadOnlyList<ItemDoAlunoResponse> Itens);

public sealed record FamiliaResponse(string Codigo, string Nome, string Descricao, int Passos);

// Tipo, tier e raridade em camelCase, como no catálogo ("premioParticipacao", "lendario").
// ExigeMateria: o prêmio de participação que deduplica por matéria (apresentação em sala),
// cuja reivindicação pede a matéria (passo 8).
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
    IReadOnlyList<PremioObtidoResponse> Obtidos,
    bool ExigeMateria,
    ProgressoResponse? Progresso = null);

// O progresso ("2 de 3") da conquista na instância vigente do período dela (passo 9), em
// "bimestre", "semestre", "ano" ou "curso". Só na rota do próprio aluno; o perfil público
// nunca o leva (produto.md 9.1).
// Unidade: "materias" quando o alvo é o das matérias do período (regra de todas as matérias);
// nula, quando é um número de prêmios. Acréscimo do passo 9.
public sealed record ProgressoResponse(string Periodo, int Contagem, int Alvo, string? Unidade = null);

// Percentual dos alunos que têm o item, com uma casa: "x% dos alunos".
public sealed record RaridadeResponse(string Nivel, double Percentual);

// PostId entrou no passo 7: o post que o prêmio rendeu, para o app não oferecer de novo.
// Instancia entrou no passo 9: só na conquista, o período em que ela foi obtida.
public sealed record PremioObtidoResponse(
    Guid Id,
    string Tier,
    int Pontos,
    string? Materia,
    int? Bimestre,
    DateTimeOffset ObtidoEm,
    Guid? PostId,
    InstanciaResponse? Instancia = null);

// A instância de uma conquista obtida, em números, para o app escrever: o bimestre (1 a 4)
// com o semestre e o ano; o semestre (1 ou 2) e o ano; o ano; ou só o período, no curso.
public sealed record InstanciaResponse(string Periodo, int? Ano, int? Semestre, int? Bimestre);
