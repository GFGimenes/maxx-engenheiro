using System.ComponentModel.DataAnnotations;
using Maxx.Api.LimiteDeTaxa;
using Maxx.Application.Autenticacao.Recuperacao;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maxx.Api.Autenticacao;

// Recuperação de senha e primeiro acesso (fluxos/06-recuperacao-de-senha.md).
internal static class RecuperacaoEndpoints
{
    public static IEndpointRouteBuilder MapRecuperacaoEndpoints(this IEndpointRouteBuilder auth)
    {
        var recuperacao = auth.MapGroup("/recuperacao").WithTags("Autenticacao");

        recuperacao.MapPost("", SolicitarAsync)
            .WithName("SolicitarRecuperacaoDeSenha")
            .ProducesValidationProblem()
            .ComLimiteDeTaxa(
                Limite.RecuperacaoPorEmail, contexto => LimiteDeTaxaExtensions.Do<RecuperacaoRequest>(contexto, pedido => pedido.Email));

        recuperacao.MapPost("/confirmar", ConfirmarAsync)
            .WithName("ConfirmarRecuperacaoDeSenha")
            .ProducesValidationProblem();

        return auth;
    }

    // Sempre 202, e sem corpo: exista a conta ou não, o cliente diz "se o e-mail estiver
    // cadastrado, o link chega em instantes".
    private static async Task<Accepted> SolicitarAsync(
        RecuperacaoRequest pedido, RecuperacaoHandler handler, CancellationToken cancellationToken)
    {
        await handler.SolicitarAsync(pedido.Email, cancellationToken);
        return TypedResults.Accepted((string?)null);
    }

    // Sem sessão na resposta: a senha nova derrubou todas, e o cliente leva ao login.
    private static async Task<NoContent> ConfirmarAsync(
        ConfirmacaoRequest pedido, RecuperacaoHandler handler, CancellationToken cancellationToken)
    {
        await handler.ConfirmarAsync(new NovaSenha(pedido.Token, pedido.Senha), cancellationToken);
        return TypedResults.NoContent();
    }
}

public sealed record RecuperacaoRequest([property: Required, StringLength(254)] string Email);

public sealed record ConfirmacaoRequest(
    [property: Required, StringLength(128)] string Token,
    [property: Required(AllowEmptyStrings = true), StringLength(512)] string Senha)
{
    public override string ToString() => "ConfirmacaoRequest { }";
}

// A URL pública do app, de onde o e-mail aponta para a tela de senha nova (passo 5). O
// token vai no fragmento, depois do #: o navegador não o manda a servidor nenhum, e ele
// não aparece em log de acesso nem em Referer.
internal sealed class LinkDoApp(Uri urlPublica) : ILinkDoApp
{
    public const string Section = "App";

    public Uri RedefinicaoDeSenha(string token) =>
        new(urlPublica, $"redefinir-senha#token={Uri.EscapeDataString(token)}");
}
