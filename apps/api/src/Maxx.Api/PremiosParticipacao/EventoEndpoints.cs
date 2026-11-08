using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Maxx.Api.Autenticacao;
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

        return v1;
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

// Datas em America/Sao_Paulo, inclusivas. Itens: os códigos dos prêmios que o evento aceita.
public sealed record EventoResponse(Guid Id, string Nome, DateOnly Inicio, DateOnly Fim, IReadOnlyList<string> Itens);
