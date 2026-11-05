using Maxx.Application.Autenticacao;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Maxx.Api.Autenticacao;

// Access token curto com sub e papéis, e nada mais: a lista de turmas do professor nunca
// entra no token (ADR 0006).
internal sealed class JwtAccessTokenIssuer(ChaveDeAssinatura chave, TimeProvider relogio) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var agora = relogio.GetUtcNow();
        var expiraEm = agora + JwtOptions.Validade;

        var claims = new Dictionary<string, object>
        {
            [MaxxClaims.Sub] = usuario.Id.ToString(),
            [MaxxClaims.Papel] = NomesDosPapeis(usuario.Papeis),
        };

        var valor = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = JwtOptions.Emissor,
            Audience = JwtOptions.Audiencia,
            Claims = claims,
            IssuedAt = agora.UtcDateTime,
            NotBefore = agora.UtcDateTime,
            Expires = expiraEm.UtcDateTime,
            SigningCredentials = new SigningCredentials(chave.Chave, SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(valor, expiraEm);
    }

    // Sempre lista, mesmo com um papel só: o cliente lê um formato só.
    internal static string[] NomesDosPapeis(Papeis papeis) =>
    [
        .. new[] { (Papeis.Aluno, "aluno"), (Papeis.Professor, "professor"), (Papeis.Instituicao, "instituicao") }
            .Where(papel => papeis.HasFlag(papel.Item1))
            .Select(papel => papel.Item2),
    ];
}

internal sealed record ChaveDeAssinatura(SymmetricSecurityKey Chave);
