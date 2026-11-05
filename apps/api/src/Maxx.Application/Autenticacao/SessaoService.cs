using Maxx.Domain.Identidade;

namespace Maxx.Application.Autenticacao;

// Abre uma sessão: família nova com o primeiro refresh, mais o access token. Usado no
// cadastro, no login e na entrada da demonstração. Quem chama grava.
public sealed class SessaoService(
    ITokenGenerator tokens, IAccessTokenIssuer accessTokens, IFamiliaDeSessaoRepository familias, TimeProvider relogio)
{
    public Sessao Abrir(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var refresh = tokens.Generate();
        var familia = FamiliaDeSessao.Abrir(usuario.Id, refresh.Hash, relogio.GetUtcNow());
        familias.Adicionar(familia);

        return new Sessao(accessTokens.Issue(usuario), refresh.Valor, familia.Tokens.Single().ExpiraEm);
    }
}

// O que o cliente recebe ao entrar. O valor do refresh existe só aqui, em memória, no
// caminho até a resposta: o banco guarda o hash.
public sealed record Sessao(AccessToken AccessToken, string Refresh, DateTimeOffset RefreshExpiraEm);
