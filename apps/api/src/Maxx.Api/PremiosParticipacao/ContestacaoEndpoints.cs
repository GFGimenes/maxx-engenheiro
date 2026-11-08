using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Maxx.Api.Autenticacao;
using Maxx.Api.Medalhas;
using Maxx.Api.Posts;
using Maxx.Application.Fotos;
using Maxx.Application.Paginacao;
using Maxx.Application.PremiosParticipacao;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maxx.Api.PremiosParticipacao;

// Contestação, fila de contestações e decisão da instituição (fluxos/04-contestacao.md), e o
// histórico de reivindicações do próprio aluno (D9). Contestam professor e instituição; decide
// e lê a fila só a instituição.
internal static class ContestacaoEndpoints
{
    public const int PorPagina = 20;

    public static IEndpointRouteBuilder MapContestacaoEndpoints(this IEndpointRouteBuilder v1)
    {
        v1.MapPost("/premios-participacao/{premioId:guid}/contestacao", ContestarAsync)
            .WithName("ContestarPremioParticipacao")
            .WithTags("Contestacoes")
            .RequireAuthorization(Politicas.Aprovador)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var contestacoes = v1.MapGroup("/contestacoes").WithTags("Contestacoes").RequireAuthorization(Politicas.Instituicao);

        contestacoes.MapGet("/", AbertasAsync)
            .WithName("GetFilaDeContestacoes")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        contestacoes.MapPost("/{contestacaoId:guid}/confirmacao", ConfirmarAsync)
            .WithName("ConfirmarPremioContestado")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        contestacoes.MapPost("/{contestacaoId:guid}/revogacao", RevogarAsync)
            .WithName("RevogarPremioContestado")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        v1.MapGet("/eu/reivindicacoes", ReivindicacoesAsync)
            .WithName("GetMinhasReivindicacoes")
            .WithTags("Premios")
            .RequireAuthorization(Politicas.Aluno)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return v1;
    }

    private static async Task<Created<ContestacaoCriadaResponse>> ContestarAsync(
        Guid premioId,
        MotivoRequest pedido,
        ClaimsPrincipal principal,
        ContestacaoHandler handler,
        CancellationToken cancellationToken)
    {
        var contestacao = await handler.ContestarAsync(PostEndpoints.UsuarioId(principal), premioId, pedido.Motivo, cancellationToken);
        return TypedResults.Created((string?)null, new ContestacaoCriadaResponse(contestacao.Id, contestacao.PremioParticipacaoId, contestacao.ContestadaEm));
    }

    private static async Task<Ok<PaginaDeContestacoesResponse>> AbertasAsync(
        IConsultaDeContestacoes consulta,
        IArmazenamentoDeFotos armazenamento,
        TimeProvider relogio,
        CancellationToken cancellationToken,
        string? cursor = null)
    {
        var pagina = await consulta.AbertasAsync(CursorDeTempo.Ler(cursor), PorPagina, relogio.GetUtcNow(), cancellationToken);
        return TypedResults.Ok(new PaginaDeContestacoesResponse(
            [.. pagina.Itens.Select(contestacao => ContestacaoNaFilaResponse.De(contestacao, armazenamento))], pagina.Proximo));
    }

    private static async Task<NoContent> ConfirmarAsync(
        Guid contestacaoId, ClaimsPrincipal principal, ContestacaoHandler handler, CancellationToken cancellationToken)
    {
        await handler.ConfirmarAsync(PostEndpoints.UsuarioId(principal), contestacaoId, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> RevogarAsync(
        Guid contestacaoId, MotivoRequest pedido, ClaimsPrincipal principal, ContestacaoHandler handler, CancellationToken cancellationToken)
    {
        await handler.RevogarAsync(PostEndpoints.UsuarioId(principal), contestacaoId, pedido.Motivo, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<ReivindicacaoResponse>>> ReivindicacoesAsync(
        ClaimsPrincipal principal, IConsultaDeContestacoes consulta, CancellationToken cancellationToken) =>
        TypedResults.Ok<IReadOnlyList<ReivindicacaoResponse>>([.. (await consulta.ReivindicacoesAsync(PostEndpoints.UsuarioId(principal), cancellationToken))
            .Select(reivindicacao => new ReivindicacaoResponse(
                reivindicacao.PremioId,
                reivindicacao.PostId,
                reivindicacao.Item,
                reivindicacao.NomeDoItem,
                ProfessorEndpoints.Nome(reivindicacao.Tier),
                reivindicacao.Pontos,
                reivindicacao.Evento,
                reivindicacao.Materia,
                ProfessorEndpoints.Nome(reivindicacao.Estado),
                reivindicacao.Motivo,
                reivindicacao.Desde))]);
}

// O limite do motivo (500 caracteres, contando emoji como um) é do domínio; aqui, só um teto
// bruto para o corpo.
public sealed record MotivoRequest([property: StringLength(4000)] string? Motivo);

public sealed record ContestacaoCriadaResponse(Guid Id, Guid PremioId, DateTimeOffset ContestadaEm);

public sealed record PaginaDeContestacoesResponse(IReadOnlyList<ContestacaoNaFilaResponse> Itens, string? Proximo);

// ContestadaPor: o arroba de quem contestou. PassouDoPrazo: a marca de 7 dias desde a
// contestação.
public sealed record ContestacaoNaFilaResponse(
    Guid Id,
    Guid PremioId,
    Guid? PostId,
    AutorDoPostResponse Aluno,
    string Item,
    string NomeDoItem,
    string Tier,
    int Pontos,
    EventoDaComprovacaoResponse Evento,
    string? Materia,
    FotoDoPostResponse? Foto,
    string? Legenda,
    string Motivo,
    string ContestadaPor,
    DateTimeOffset ContestadaEm,
    DateTimeOffset AprovadoEm,
    bool PassouDoPrazo)
{
    public static ContestacaoNaFilaResponse De(ContestacaoNaFila contestacao, IArmazenamentoDeFotos armazenamento) => new(
        contestacao.Id,
        contestacao.PremioId,
        contestacao.PostId,
        new AutorDoPostResponse(
            contestacao.Aluno.Id,
            contestacao.Aluno.NomeDeExibicao,
            contestacao.Aluno.Arroba,
            contestacao.Aluno.Curso,
            contestacao.Aluno.Semestre,
            PostResponse.Leitura(armazenamento, contestacao.Aluno.ChaveDaFoto)),
        contestacao.Item,
        contestacao.NomeDoItem,
        ProfessorEndpoints.Nome(contestacao.Tier),
        contestacao.Pontos,
        new EventoDaComprovacaoResponse(
            contestacao.Evento.Id, contestacao.Evento.Nome, contestacao.Evento.Inicio, contestacao.Evento.Fim, contestacao.Evento.Digitado),
        contestacao.Materia,
        contestacao is { ChaveDaFotoDoFeed: { } feed, ChaveDaFotoDaGrade: { } grade }
            ? new FotoDoPostResponse(armazenamento.UrlDeLeitura(feed).AbsoluteUri, armazenamento.UrlDeLeitura(grade).AbsoluteUri)
            : null,
        contestacao.Legenda,
        contestacao.Motivo,
        contestacao.ContestadaPor,
        contestacao.ContestadaEm,
        contestacao.AprovadoEm,
        contestacao.PassouDoPrazo);
}

// Estado em camelCase: "reivindicado", "emAveriguacao", "reprovado", "revogado". Motivo: o
// da reprovação ou o da revogação. Desde: quando entrou no estado.
public sealed record ReivindicacaoResponse(
    Guid PremioId,
    Guid? PostId,
    string Item,
    string NomeDoItem,
    string Tier,
    int Pontos,
    string Evento,
    string? Materia,
    string Estado,
    string? Motivo,
    DateTimeOffset Desde);
