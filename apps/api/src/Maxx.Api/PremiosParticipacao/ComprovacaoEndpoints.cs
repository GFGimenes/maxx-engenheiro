using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Maxx.Api.Autenticacao;
using Maxx.Api.Medalhas;
using Maxx.Api.Posts;
using Maxx.Application.Fotos;
using Maxx.Application.PremiosParticipacao;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maxx.Api.PremiosParticipacao;

// A fila de comprovações e a decisão sobre cada uma (fluxos/02-post-com-comprovacao.md):
// professor e instituição. O papel do token só abre a porta; o escopo de turma do professor
// é conferido no banco a cada leitura e a cada decisão.
internal static class ComprovacaoEndpoints
{
    public static IEndpointRouteBuilder MapComprovacaoEndpoints(this IEndpointRouteBuilder v1)
    {
        var comprovacoes = v1.MapGroup("/comprovacoes").WithTags("Comprovacoes").RequireAuthorization(Politicas.Aprovador);

        comprovacoes.MapGet("/", PendentesAsync)
            .WithName("GetFilaDeComprovacoes")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        comprovacoes.MapGet("/eventos", EventosAsync)
            .WithName("GetEventosDaFilaDeComprovacoes")
            .ProducesProblem(StatusCodes.Status403Forbidden);

        comprovacoes.MapPost("/{comprovacaoId:guid}/aprovacao", AprovarAsync)
            .WithName("AprovarComprovacao")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        comprovacoes.MapPost("/{comprovacaoId:guid}/reprovacao", ReprovarAsync)
            .WithName("ReprovarComprovacao")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return v1;
    }

    // As pendentes, das mais antigas para as mais novas, 20 por página; com o evento, só as dele.
    private static async Task<Ok<PaginaDeComprovacoesResponse>> PendentesAsync(
        ClaimsPrincipal principal,
        FilaDeComprovacoesHandler handler,
        IArmazenamentoDeFotos armazenamento,
        CancellationToken cancellationToken,
        Guid? eventoId = null,
        string? cursor = null)
    {
        var pagina = await handler.PendentesAsync(PostEndpoints.UsuarioId(principal), eventoId, cursor, cancellationToken);
        return TypedResults.Ok(new PaginaDeComprovacoesResponse(
            [.. pagina.Itens.Select(comprovacao => ComprovacaoNaFilaResponse.De(comprovacao, armazenamento))], pagina.Proximo));
    }

    private static async Task<Ok<IReadOnlyList<EventoNaFilaResponse>>> EventosAsync(
        ClaimsPrincipal principal, FilaDeComprovacoesHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok<IReadOnlyList<EventoNaFilaResponse>>([.. (await handler.EventosAsync(PostEndpoints.UsuarioId(principal), cancellationToken))
            .Select(evento => new EventoNaFilaResponse(evento.Id, evento.Nome, evento.Inicio, evento.Pendentes))]);

    private static async Task<NoContent> AprovarAsync(
        Guid comprovacaoId, ClaimsPrincipal principal, DecidirComprovacaoHandler handler, CancellationToken cancellationToken)
    {
        await handler.AprovarAsync(PostEndpoints.UsuarioId(principal), comprovacaoId, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> ReprovarAsync(
        Guid comprovacaoId,
        ReprovacaoRequest pedido,
        ClaimsPrincipal principal,
        DecidirComprovacaoHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.ReprovarAsync(PostEndpoints.UsuarioId(principal), comprovacaoId, pedido.Motivo, cancellationToken);
        return TypedResults.NoContent();
    }
}

// O limite do motivo (500 caracteres, contando emoji como um) é do domínio; aqui, só um teto
// bruto para o corpo.
public sealed record ReprovacaoRequest([property: StringLength(4000)] string? Motivo);

public sealed record PaginaDeComprovacoesResponse(IReadOnlyList<ComprovacaoNaFilaResponse> Itens, string? Proximo);

// Uma comprovação pendente: o aluno, o item com tier e pontos do momento, o evento, a foto e a
// legenda do post. PassouDoPrazo: a marca de 7 dias. Materias: as do aluno no semestre
// vigente; para o professor, só as que ele também leciona.
public sealed record ComprovacaoNaFilaResponse(
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
    DateTimeOffset ReivindicadaEm,
    bool PassouDoPrazo,
    IReadOnlyList<string> Materias)
{
    public static ComprovacaoNaFilaResponse De(ComprovacaoNaFila comprovacao, IArmazenamentoDeFotos armazenamento) => new(
        comprovacao.Id,
        comprovacao.PremioId,
        comprovacao.PostId,
        new AutorDoPostResponse(
            comprovacao.Aluno.Id,
            comprovacao.Aluno.NomeDeExibicao,
            comprovacao.Aluno.Arroba,
            comprovacao.Aluno.Curso,
            comprovacao.Aluno.Semestre,
            PostResponse.Leitura(armazenamento, comprovacao.Aluno.ChaveDaFoto)),
        comprovacao.Item,
        comprovacao.NomeDoItem,
        ProfessorEndpoints.Nome(comprovacao.Tier),
        comprovacao.Pontos,
        new EventoDaComprovacaoResponse(
            comprovacao.Evento.Id, comprovacao.Evento.Nome, comprovacao.Evento.Inicio, comprovacao.Evento.Fim, comprovacao.Evento.Digitado),
        comprovacao.Materia,
        comprovacao is { ChaveDaFotoDoFeed: { } feed, ChaveDaFotoDaGrade: { } grade }
            ? new FotoDoPostResponse(armazenamento.UrlDeLeitura(feed).AbsoluteUri, armazenamento.UrlDeLeitura(grade).AbsoluteUri)
            : null,
        comprovacao.Legenda,
        comprovacao.ReivindicadaEm,
        comprovacao.PassouDoPrazo,
        comprovacao.Materias);
}

// Digitado: o evento é texto livre do aluno, não do seed.
public sealed record EventoDaComprovacaoResponse(Guid Id, string Nome, DateOnly Inicio, DateOnly Fim, bool Digitado);

public sealed record EventoNaFilaResponse(Guid Id, string Nome, DateOnly Inicio, int Pendentes);
