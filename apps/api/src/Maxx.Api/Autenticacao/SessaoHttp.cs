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

        httpContext.Response.Cookies.Append(CookieDoRefresh, sessao.Refresh, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = CaminhoDoCookie,
            Expires = sessao.RefreshExpiraEm,
            IsEssential = true,
        });

        return SessaoResponse.De(sessao, refreshNoCorpo: false);
    }
}

public sealed record SessaoResponse(
    string AccessToken, DateTimeOffset AccessTokenExpiraEm, string? RefreshToken, DateTimeOffset RefreshTokenExpiraEm)
{
    internal static SessaoResponse De(Sessao sessao, bool refreshNoCorpo) =>
        new(sessao.AccessToken.Valor, sessao.AccessToken.ExpiraEm, refreshNoCorpo ? sessao.Refresh : null, sessao.RefreshExpiraEm);
}
