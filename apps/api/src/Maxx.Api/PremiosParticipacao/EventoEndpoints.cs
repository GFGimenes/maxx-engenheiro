using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Maxx.Api.Autenticacao;
using Maxx.Api.Posts;
using Maxx.Application.Fotos;
using Maxx.Application.Posts;
using Maxx.Application.PremiosParticipacao;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maxx.Api.PremiosParticipacao;

// Os eventos semeados que o aluno escolhe ao reivindicar um prêmio de participação
// (produto.md 4.6). Só aluno: é ele quem reivindica.
internal static class EventoEndpoints
{
    public static IEndpointRouteBuilder MapEventoEndpoints(this IEndpointRouteBuilder v1)
    {
        v1.MapGet("/eventos", ReivindicaveisAsync)
            .WithName("GetEventosReivindicaveis")
            .WithTags("Eventos")
            .RequireAuthorization(Politicas.Aluno)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        v1.MapGet("/eu/materias", MateriasAsync)
            .WithName("GetMinhasMaterias")
            .WithTags("Eventos")
            .RequireAuthorization(Politicas.Aluno)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        v1.MapPost("/reivindicacoes", ReivindicarAsync)
            .WithName("ReivindicarPremioParticipacao")
            .WithTags("Eventos")
            .RequireAuthorization(Politicas.Aluno)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return v1;
    }

    // As matérias que o aluno cursa no semestre de hoje, para a apresentação em sala (P17).
    private static async Task<Ok<IReadOnlyList<MateriaResponse>>> MateriasAsync(
        ClaimsPrincipal principal, MateriasDoAlunoHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok<IReadOnlyList<MateriaResponse>>([.. (await handler.CursadasAsync(PostEndpoints.UsuarioId(principal), cancellationToken))
            .Select(materia => new MateriaResponse(materia.Id, materia.Nome))]);

    // O post de reivindicação com o prêmio pendente (fluxos/02-post-com-comprovacao.md). A
    // resposta é o post, como o feed o mostra, com a marca de pendente.
    private static async Task<Created<PostResponse>> ReivindicarAsync(
        ReivindicacaoRequest pedido,
        ClaimsPrincipal principal,
        ReivindicarHandler handler,
        IConsultaDePosts consulta,
        IArmazenamentoDeFotos armazenamento,
        CancellationToken cancellationToken)
    {
        var alunoId = PostEndpoints.UsuarioId(principal);
        var reivindicacao = await handler.ReivindicarAsync(
            alunoId,
            new PedidoDeReivindicacao(
                pedido.Item,
                pedido.EventoId,
                pedido.EventoDigitado is { } digitado ? new EventoDigitado(digitado.Nome, digitado.Inicio, digitado.Fim) : null,
                pedido.MateriaId,
                pedido.UploadId,
                pedido.Legenda),
            cancellationToken);
        var lido = await consulta.PostAsync(reivindicacao.Post.Id, alunoId, cancellationToken)
            ?? throw new InvalidOperationException($"O post {reivindicacao.Post.Id} acabou de ser gravado e não foi lido.");
        return TypedResults.Created($"/api/v1/posts/{lido.Id}", PostResponse.De(lido, armazenamento));
    }

    // item: o código do prêmio de participação; com ele, só os eventos que o aceitam.
    private static async Task<Ok<IReadOnlyList<EventoResponse>>> ReivindicaveisAsync(
        ClaimsPrincipal principal,
        EventosHandler handler,
        CancellationToken cancellationToken,
        [StringLength(16)] string? item = null)
    {
        var alunoId = Guid.TryParse(principal.FindFirstValue(MaxxClaims.Sub), out var id)
            ? id
            : throw new InvalidOperationException("Token autenticado sem sub.");

        return TypedResults.Ok<IReadOnlyList<EventoResponse>>([.. (await handler.ReivindicaveisAsync(alunoId, item, cancellationToken))
            .Select(evento => new EventoResponse(evento.Id, evento.Nome, evento.Inicio, evento.Fim, evento.ItensAceitos))]);
    }
}

// O evento é o id de um semeado (GET /v1/eventos) ou o digitado, nunca os dois; a matéria,
// só na apresentação em sala. A foto é obrigatória: é a comprovação. Os limites de tamanho
// são do domínio; aqui, só tetos brutos para o corpo.
public sealed record ReivindicacaoRequest(
    [property: Required, StringLength(16)] string Item,
    Guid? EventoId,
    EventoDigitadoRequest? EventoDigitado,
    Guid? MateriaId,
    Guid UploadId,
    [property: StringLength(4000)] string? Legenda);

// Datas em America/Sao_Paulo, inclusivas.
public sealed record EventoDigitadoRequest([property: Required, StringLength(1000)] string Nome, DateOnly Inicio, DateOnly Fim);

// Datas em America/Sao_Paulo, inclusivas. Itens: os códigos dos prêmios que o evento aceita.
public sealed record EventoResponse(Guid Id, string Nome, DateOnly Inicio, DateOnly Fim, IReadOnlyList<string> Itens);

public sealed record MateriaResponse(Guid Id, string Nome);
