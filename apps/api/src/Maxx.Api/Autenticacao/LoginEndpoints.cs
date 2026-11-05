using System.ComponentModel.DataAnnotations;
using Maxx.Api.Erros;
using Maxx.Api.LimiteDeTaxa;
using Maxx.Application.Autenticacao;
using Maxx.Application.Autenticacao.Login;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maxx.Api.Autenticacao;

// Login, renovação e logout (fluxos/01-login.md). Na web o refresh anda só em cookie; no
// nativo, só no corpo (SessaoHttp).
internal static partial class LoginEndpoints
{
    public static IEndpointRouteBuilder MapLoginEndpoints(this IEndpointRouteBuilder auth)
    {
        var grupo = auth.MapGroup("").WithTags("Autenticacao");

        grupo.MapPost("/login", EntrarAsync)
            .WithName("Entrar")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ComLimiteDeTaxa(Limite.LoginPorIp, contexto => LimiteDeTaxaExtensions.Ip(contexto.HttpContext))
            .ComLimiteDeTaxa(
                Limite.LoginPorIdentificador,
                contexto => LimiteDeTaxaExtensions.Do<LoginRequest>(contexto, pedido => pedido.Identificador));

        grupo.MapPost("/refresh", RenovarAsync)
            .WithName("RenovarSessao")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        grupo.MapPost("/logout", SairAsync)
            .WithName("Sair")
            .ProducesValidationProblem();

        return auth;
    }

    private static async Task<Ok<SessaoResponse>> EntrarAsync(
        LoginRequest pedido, LoginHandler handler, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var sessao = await handler.EntrarAsync(new Credenciais(pedido.Identificador, pedido.Senha), cancellationToken);
        return TypedResults.Ok(SessaoHttp.Escrever(httpContext, sessao));
    }

    private static async Task<Results<Ok<SessaoResponse>, ProblemHttpResult>> RenovarAsync(
        RefreshRequest? pedido,
        RefreshHandler handler,
        HttpContext httpContext,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var refresh = SessaoHttp.LerRefresh(httpContext.Request, pedido?.RefreshToken);
        var renovacao = refresh is null
            ? Renovacao.Recusada(RecusaDaRenovacao.Desconhecido)
            : await handler.RenovarAsync(refresh, cancellationToken);

        if (renovacao.Sessao is { } sessao)
        {
            return TypedResults.Ok(SessaoHttp.Escrever(httpContext, sessao));
        }

        var logger = loggers.CreateLogger(typeof(LoginEndpoints));
        if (renovacao.Recusa == RecusaDaRenovacao.ReusoDetectado)
        {
            LogReuso(logger, renovacao.FamiliaId);
        }
        else
        {
            LogRecusada(logger, renovacao.Recusa, renovacao.FamiliaId);
        }

        SessaoHttp.Apagar(httpContext);
        return TypedResults.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            extensions: new Dictionary<string, object?> { [Erro.CampoCodigo] = CodigosDeRecusa.SessaoInvalida });
    }

    // Sempre 204: refresh desconhecido ou família já revogada não muda a resposta.
    private static async Task<NoContent> SairAsync(
        RefreshRequest? pedido, LogoutHandler handler, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (SessaoHttp.LerRefresh(httpContext.Request, pedido?.RefreshToken) is { } refresh)
        {
            await handler.SairAsync(refresh, cancellationToken);
        }

        SessaoHttp.Apagar(httpContext);
        return TypedResults.NoContent();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reuso de refresh: família {FamiliaId} revogada.")]
    private static partial void LogReuso(ILogger logger, Guid? familiaId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Renovação recusada ({Recusa}), família {FamiliaId}.")]
    private static partial void LogRecusada(ILogger logger, RecusaDaRenovacao? recusa, Guid? familiaId);
}

// Identificador é o RA ou o e-mail. A senha só tem teto na borda: o Argon2id não recebe
// texto sem limite, e a política vale no cadastro, não no login.
public sealed record LoginRequest(
    [property: Required, StringLength(254)] string Identificador,
    [property: Required(AllowEmptyStrings = true), StringLength(512)] string Senha)
{
    public override string ToString() => "LoginRequest { }";
}

// No nativo, o refresh vem aqui; na web o corpo pode faltar, porque ele vem do cookie.
public sealed record RefreshRequest([property: StringLength(128)] string? RefreshToken)
{
    public override string ToString() => "RefreshRequest { }";
}
