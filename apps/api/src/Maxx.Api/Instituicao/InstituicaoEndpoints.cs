using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Maxx.Api.Autenticacao;
using Maxx.Api.Medalhas;
using Maxx.Api.Posts;
using Maxx.Application.Fotos;
using Maxx.Application.Instituicao;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maxx.Api.Instituicao;

// A área da instituição (produto.md 10.2, passo 12): busca e ficha do aluno, e os atos
// sobre a linha dele. Só a instituição; o papel é conferido de novo no banco.
internal static class InstituicaoEndpoints
{
    public static IEndpointRouteBuilder MapInstituicaoEndpoints(this IEndpointRouteBuilder v1)
    {
        var alunos = v1.MapGroup("/instituicao/alunos").WithTags("Instituicao").RequireAuthorization(Politicas.Instituicao);

        alunos.MapGet("/", BuscarAsync)
            .WithName("BuscarAlunosDaBase")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        alunos.MapGet("/{cadastroId:guid}", FichaAsync)
            .WithName("GetFichaDoAluno")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        alunos.MapPut("/{cadastroId:guid}/email", CorrigirEmailAsync)
            .WithName("CorrigirEmailDoAluno")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        alunos.MapPost("/{cadastroId:guid}/ex-aluno", MarcarExAlunoAsync)
            .WithName("MarcarExAluno")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        alunos.MapPut("/{cadastroId:guid}/curso", TrocarDeCursoAsync)
            .WithName("TrocarCursoDoAluno")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return v1;
    }

    private static async Task<Ok<IReadOnlyList<AlunoDaBaseResponse>>> BuscarAsync(
        ClaimsPrincipal principal,
        AtosSobreOAlunoHandler handler,
        IArmazenamentoDeFotos armazenamento,
        CancellationToken cancellationToken,
        [StringLength(200)] string? busca = null) =>
        TypedResults.Ok<IReadOnlyList<AlunoDaBaseResponse>>([.. (await handler.BuscarAsync(PostEndpoints.UsuarioId(principal), busca, cancellationToken))
            .Select(aluno => AlunoDaBaseResponse.De(aluno, armazenamento))]);

    private static async Task<Ok<FichaDoAlunoResponse>> FichaAsync(
        Guid cadastroId,
        ClaimsPrincipal principal,
        AtosSobreOAlunoHandler handler,
        IArmazenamentoDeFotos armazenamento,
        CancellationToken cancellationToken)
    {
        var ficha = await handler.FichaAsync(PostEndpoints.UsuarioId(principal), cadastroId, cancellationToken);
        return TypedResults.Ok(new FichaDoAlunoResponse(
            AlunoDaBaseResponse.De(ficha.Aluno, armazenamento),
            [.. ficha.Medalhas.Select(medalha => new MedalhaDaFichaResponse(
                medalha.Id,
                medalha.Item,
                medalha.NomeDoItem,
                ProfessorEndpoints.Nome(medalha.Tier),
                medalha.Pontos,
                medalha.Materia,
                medalha.Ano,
                medalha.Bimestre,
                medalha.ConcedidaPor,
                medalha.ConcedidaEm))]));
    }

    private static async Task<NoContent> CorrigirEmailAsync(
        Guid cadastroId, EmailRequest pedido, ClaimsPrincipal principal, AtosSobreOAlunoHandler handler, CancellationToken cancellationToken)
    {
        await handler.CorrigirEmailAsync(PostEndpoints.UsuarioId(principal), cadastroId, pedido.Email, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> MarcarExAlunoAsync(
        Guid cadastroId, ClaimsPrincipal principal, AtosSobreOAlunoHandler handler, CancellationToken cancellationToken)
    {
        await handler.MarcarExAlunoAsync(PostEndpoints.UsuarioId(principal), cadastroId, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> TrocarDeCursoAsync(
        Guid cadastroId, TrocaDeCursoRequest pedido, ClaimsPrincipal principal, AtosSobreOAlunoHandler handler, CancellationToken cancellationToken)
    {
        await handler.TrocarDeCursoAsync(PostEndpoints.UsuarioId(principal), cadastroId, pedido.CursoId, pedido.Semestre, cancellationToken);
        return TypedResults.NoContent();
    }
}

// A forma do e-mail é do domínio; aqui, só um teto bruto para o corpo.
public sealed record EmailRequest([property: StringLength(320)] string? Email);

public sealed record TrocaDeCursoRequest(Guid CursoId, int Semestre);

// Conta nula: está na base e ainda não se cadastrou. Demonstracao: linha criada pela
// demonstração, não pela faculdade.
public sealed record AlunoDaBaseResponse(
    Guid Id,
    string NomeCompleto,
    string Ra,
    string Email,
    Guid CursoId,
    string Curso,
    int Semestre,
    bool Demonstracao,
    ContaDoAlunoResponse? Conta)
{
    public static AlunoDaBaseResponse De(AlunoDaBase aluno, IArmazenamentoDeFotos armazenamento)
    {
        ArgumentNullException.ThrowIfNull(aluno);
        return new(
            aluno.Id,
            aluno.NomeCompleto,
            aluno.Ra,
            aluno.Email,
            aluno.CursoId,
            aluno.Curso,
            aluno.Semestre,
            aluno.Demonstracao,
            aluno.Conta is { } conta
                ? new ContaDoAlunoResponse(
                    conta.Id, conta.NomeDeExibicao, conta.Arroba, conta.ExAlunoDesde, PostResponse.Leitura(armazenamento, conta.ChaveDaFoto))
                : null);
    }
}

// ExAlunoDesde nulo: aluno ativo. Foto: a derivada da grade, para o avatar.
public sealed record ContaDoAlunoResponse(Guid Id, string NomeDeExibicao, string Arroba, DateTimeOffset? ExAlunoDesde, string? Foto);

public sealed record FichaDoAlunoResponse(AlunoDaBaseResponse Aluno, IReadOnlyList<MedalhaDaFichaResponse> Medalhas);

// Tier em camelCase, como no catálogo. Bimestre de 1 a 4 no ano.
public sealed record MedalhaDaFichaResponse(
    Guid Id,
    string Item,
    string NomeDoItem,
    string Tier,
    int Pontos,
    string Materia,
    int Ano,
    int Bimestre,
    string ConcedidaPor,
    DateTimeOffset ConcedidaEm);
