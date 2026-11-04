using Maxx.Application.Autenticacao;

namespace Maxx.Api.Autenticacao;

// Como a sessão chega ao cliente (decisoes/README.md, Autenticação): na web o refresh vai
// em cookie HttpOnly e nunca no corpo; no nativo vai no corpo, para o secure store. O
// access token vai sempre no corpo, para ficar só em memória.
internal static class SessaoHttp
{
    public const string CabecalhoDoCliente = "X-Maxx-Cliente";

    public const string ClienteWeb = "web";

    public const string CookieDoRefresh = "maxx_refresh";

    // Só as rotas de autenticação recebem o cookie: nenhuma outra rota o vê.
    public const string CaminhoDoCookie = "/api/v1/auth";

    public static bool EhWeb(HttpRequest request) =>
        string.Equals(request.Headers[CabecalhoDoCliente], ClienteWeb, StringComparison.OrdinalIgnoreCase);

    public static SessaoResponse Escrever(HttpContext httpContext, Sessao sessao)
    {
        if (!EhWeb(httpContext.Request))
        {
            return SessaoResponse.De(sessao, refreshNoCorpo: true);
        }

        httpContext.Response.Cookies.Append(CookieDoRefresh, sessao.Refresh, OpcoesDoCookie(sessao.RefreshExpiraEm));

        return SessaoResponse.De(sessao, refreshNoCorpo: false);
    }

    // Na web o refresh vem só do cookie, e o cookie só é lido com o cabeçalho do cliente:
    // um formulário de outro site não consegue mandar cabeçalho próprio, e o SameSite
    // Strict já não manda o cookie. No nativo vem só do corpo.
    public static string? LerRefresh(HttpRequest request, string? doCorpo)
    {
        var refresh = EhWeb(request) ? request.Cookies[CookieDoRefresh] : doCorpo;
        return string.IsNullOrWhiteSpace(refresh) ? null : refresh;
    }

    // Sessão recusada ou encerrada: na web o navegador esquece o cookie.
    public static void Apagar(HttpContext httpContext)
    {
        if (EhWeb(httpContext.Request))
        {
            httpContext.Response.Cookies.Delete(CookieDoRefresh, OpcoesDoCookie(expira: null));
        }
    }

    private static CookieOptions OpcoesDoCookie(DateTimeOffset? expira) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = CaminhoDoCookie,
        Expires = expira,
        IsEssential = true,
    };
}

public sealed record SessaoResponse(
    string AccessToken, DateTimeOffset AccessTokenExpiraEm, string? RefreshToken, DateTimeOffset RefreshTokenExpiraEm)
{
    internal static SessaoResponse De(Sessao sessao, bool refreshNoCorpo) =>
        new(sessao.AccessToken.Valor, sessao.AccessToken.ExpiraEm, refreshNoCorpo ? sessao.Refresh : null, sessao.RefreshExpiraEm);
}
