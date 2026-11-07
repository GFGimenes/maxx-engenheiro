using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Maxx.Api.Autenticacao;
using Maxx.Application;
using Maxx.Application.Autorizacao;
using Maxx.Application.Medalhas;
using Maxx.Domain.Premios;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maxx.Api.Medalhas;

// O que o professor faz no passo 6: ver as turmas do semestre vigente, os alunos de cada
// uma e conceder medalha do catálogo. O papel vem do token; o escopo de turma, do banco.
internal static class ProfessorEndpoints
{
    public static IEndpointRouteBuilder MapProfessorEndpoints(this IEndpointRouteBuilder v1)
    {
        var professor = v1.MapGroup("/professor").WithTags("Professor").RequireAuthorization(Politicas.Professor);

        professor.MapGet("/turmas", TurmasAsync)
            .WithName("GetTurmasDoProfessor")
            .ProducesProblem(StatusCodes.Status403Forbidden);

        professor.MapGet("/turmas/{turmaId:guid}/alunos", AlunosAsync)
            .WithName("GetAlunosDaTurma")
            .ProducesProblem(StatusCodes.Status403Forbidden);

        professor.MapGet("/medalhas", MedalhasAsync)
            .WithName("GetMedalhasDoCatalogo")
            .ProducesProblem(StatusCodes.Status403Forbidden);

        professor.MapPost("/turmas/{turmaId:guid}/medalhas", ConcederAsync)
            .WithName("ConcederMedalha")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return v1;
    }

    private static async Task<Ok<IReadOnlyList<TurmaDoProfessorResponse>>> TurmasAsync(
        ClaimsPrincipal principal, IConsultaDoProfessor consulta, CancellationToken cancellationToken) =>
        TypedResults.Ok<IReadOnlyList<TurmaDoProfessorResponse>>([.. (await consulta.TurmasVigentesAsync(UsuarioId(principal), cancellationToken))
            .Select(turma => new TurmaDoProfessorResponse(turma.Id, turma.Materia, turma.Curso, turma.Semestre, turma.Alunos))]);

    private static async Task<Ok<IReadOnlyList<AlunoDaTurmaResponse>>> AlunosAsync(
        Guid turmaId,
        ClaimsPrincipal principal,
        IEscopoDeTurma escopo,
        IConsultaDoProfessor consulta,
        CancellationToken cancellationToken)
    {
        if (!await escopo.ProfessorLecionaAsync(UsuarioId(principal), turmaId, cancellationToken))
        {
            throw new RecusaException(TipoDeRecusa.Proibido, CodigosDaMedalha.ForaDoEscopo, $"Turma {turmaId} fora do escopo.");
        }

        return TypedResults.Ok<IReadOnlyList<AlunoDaTurmaResponse>>([.. (await consulta.AlunosAsync(turmaId, cancellationToken))
            .Select(aluno => new AlunoDaTurmaResponse(
                aluno.Id,
                aluno.NomeDeExibicao,
                aluno.Arroba,
                [.. aluno.Medalhas.Select(medalha => new MedalhaNaTurmaResponse(medalha.Id, medalha.Item, medalha.Bimestre, medalha.ConcedidaEm))]))]);
    }

    private static async Task<Ok<IReadOnlyList<ItemDeMedalhaResponse>>> MedalhasAsync(
        IConsultaDoProfessor consulta, CancellationToken cancellationToken) =>
        TypedResults.Ok<IReadOnlyList<ItemDeMedalhaResponse>>([.. (await consulta.MedalhasDoCatalogoAsync(cancellationToken))
            .Select(item => new ItemDeMedalhaResponse(
                item.Item.Codigo,
                item.Item.Nome,
                item.Item.Descricao,
                Nome(item.Item.Tier),
                item.Item.Pontos,
                Nome(item.Item.Base),
                Nome(item.Item.PeriodoDeReferencia),
                item.Item.LimitePorPeriodo))]);

    private static async Task<Created<MedalhaConcedidaResponse>> ConcederAsync(
        Guid turmaId,
        ConcessaoDeMedalhaRequest pedido,
        ClaimsPrincipal principal,
        ConcederMedalhaHandler handler,
        CancellationToken cancellationToken)
    {
        var medalha = await handler.ConcederAsync(UsuarioId(principal), turmaId, pedido.AlunoId, pedido.Item, cancellationToken);
        return TypedResults.Created((string?)null, MedalhaConcedidaResponse.De(medalha));
    }

    // O sub do token; a policy de professor já exigiu um usuário autenticado.
    private static Guid UsuarioId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(MaxxClaims.Sub), out var id)
            ? id
            : throw new InvalidOperationException("Token autenticado sem sub.");

    internal static string Nome<T>(T valor)
        where T : struct, Enum => JsonNamingPolicy.CamelCase.ConvertName(valor.ToString());
}

public sealed record TurmaDoProfessorResponse(Guid Id, string Materia, string Curso, string Semestre, int Alunos);

public sealed record AlunoDaTurmaResponse(Guid Id, string NomeDeExibicao, string Arroba, IReadOnlyList<MedalhaNaTurmaResponse> Medalhas);

public sealed record MedalhaNaTurmaResponse(Guid Id, string Item, int Bimestre, DateTimeOffset ConcedidaEm);

// Tier, base e período em camelCase, como no catálogo ("bronze", "presenca", "bimestre").
public sealed record ItemDeMedalhaResponse(
    string Codigo, string Nome, string Descricao, string Tier, int Pontos, string Base, string Periodo, int LimitePorPeriodo);

// Quem recebe e o código do item: tier e pontos vêm do catálogo, ninguém digita valor.
public sealed record ConcessaoDeMedalhaRequest(Guid AlunoId, [property: Required, StringLength(16)] string Item);

public sealed record MedalhaConcedidaResponse(Guid Id, string Item, string Tier, int Pontos, int Ocorrencia, DateTimeOffset ConcedidaEm)
{
    public static MedalhaConcedidaResponse De(Medalha medalha) => new(
        medalha.Id, medalha.ItemCodigo, ProfessorEndpoints.Nome(medalha.Tier), medalha.Pontos, medalha.Ocorrencia, medalha.ConcedidaEm);
}
