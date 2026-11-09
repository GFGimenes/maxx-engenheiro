using System.Security.Claims;
using Maxx.Api.Medalhas;
using Maxx.Api.Posts;
using Maxx.Application.Notificacoes;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maxx.Api.Notificacoes;

// O sininho do cabeçalho do feed e as bolinhas das abas (produto.md 8). Cada um vê só as
// próprias notificações: o usuário é o do token, nunca um parâmetro.
internal static class NotificacaoEndpoints
{
    public const int NotificacoesPorPagina = 20;

    public static IEndpointRouteBuilder MapNotificacaoEndpoints(this IEndpointRouteBuilder v1)
    {
        var eu = v1.MapGroup("/eu").WithTags("Notificações");

        eu.MapGet("/notificacoes", ListarAsync)
            .WithName("GetNotificacoes")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        eu.MapPost("/notificacoes/leitura", MarcarLidasAsync)
            .WithName("LerNotificacoes")
            .ProducesValidationProblem();

        eu.MapGet("/novidades", NovidadesAsync)
            .WithName("GetNovidades");

        eu.MapPut("/trofeus/visto", TrofeusVistosAsync)
            .WithName("VerTrofeus");

        return v1;
    }

    private static async Task<Ok<PaginaDeNotificacoesResponse>> ListarAsync(
        string? cursor, ClaimsPrincipal principal, NotificacoesHandler handler, CancellationToken cancellationToken)
    {
        var pagina = await handler.ListarAsync(PostEndpoints.UsuarioId(principal), cursor, NotificacoesPorPagina, cancellationToken);
        return TypedResults.Ok(new PaginaDeNotificacoesResponse([.. pagina.Itens.Select(NotificacaoResponse.De)], pagina.Proximo));
    }

    // Sem corpo, ou sem "ate", marca todas as que existem agora.
    private static async Task<NoContent> MarcarLidasAsync(
        LerNotificacoesRequest? pedido, ClaimsPrincipal principal, NotificacoesHandler handler, CancellationToken cancellationToken)
    {
        await handler.MarcarLidasAsync(PostEndpoints.UsuarioId(principal), pedido?.Ate, cancellationToken);
        return TypedResults.NoContent();
    }

    // O app pergunta ao focar e a cada 60 s (D5 do passo 10): três booleanos, barato.
    private static async Task<Ok<NovidadesResponse>> NovidadesAsync(
        ClaimsPrincipal principal, NotificacoesHandler handler, CancellationToken cancellationToken)
    {
        var novidades = await handler.NovidadesAsync(PostEndpoints.UsuarioId(principal), cancellationToken);
        return TypedResults.Ok(new NovidadesResponse(novidades.Sininho, novidades.Mural, novidades.Trofeus));
    }

    private static async Task<NoContent> TrofeusVistosAsync(
        ClaimsPrincipal principal, NotificacoesHandler handler, CancellationToken cancellationToken)
    {
        await handler.MarcarTrofeusVistosAsync(PostEndpoints.UsuarioId(principal), cancellationToken);
        return TypedResults.NoContent();
    }
}

// O instante da notificação mais nova que o sininho mostrou: as que chegaram depois ficam
// acesas.
public sealed record LerNotificacoesRequest(DateTimeOffset? Ate);

// Tipo em camelCase ("medalhaRecebida", "avisoNovo"); o texto é do app. Os campos opcionais
// vêm só nos tipos que os usam: item nas de prêmio, motivo na reprovação e na revogação,
// decisão ("confirmada" ou "revogada") na decisão de contestação, post nas de prêmio de
// participação com post no ar, título no aviso novo.
public sealed record NotificacaoResponse(
    Guid Id,
    string Tipo,
    Guid ReferenciaId,
    DateTimeOffset CriadaEm,
    bool Lida,
    string? Item,
    string? Motivo,
    string? Decisao,
    Guid? PostId,
    string? TituloDoAviso)
{
    public static NotificacaoResponse De(NotificacaoLida notificacao)
    {
        ArgumentNullException.ThrowIfNull(notificacao);
        return new(
            notificacao.Id,
            ProfessorEndpoints.Nome(notificacao.Tipo),
            notificacao.ReferenciaId,
            notificacao.CriadaEm,
            notificacao.Lida,
            notificacao.Item,
            notificacao.Motivo,
            notificacao.Decisao is { } decisao ? ProfessorEndpoints.Nome(decisao) : null,
            notificacao.PostId,
            notificacao.TituloDoAviso);
    }
}

public sealed record PaginaDeNotificacoesResponse(IReadOnlyList<NotificacaoResponse> Itens, string? Proximo);

public sealed record NovidadesResponse(bool Sininho, bool Mural, bool Trofeus);
