using Maxx.Domain.Identidade;

namespace Maxx.Application.Autenticacao;

// O que a autenticação precisa e a Application não implementa: hash de senha, valor e
// hash de token opaco (refresh, recuperação), emissão do access token.

public interface ISenhaHasher
{
    // Argon2id, com sal novo a cada chamada. A senha em claro nunca sai daqui.
    string Hash(string senha);

    bool Verify(string senha, string hash);

    // O mesmo custo de um Verify, contra um hash fictício: login de quem não existe não
    // responde mais rápido que senha errada (ADR 0006, enumeração de usuário).
    void VerifyDummy(string senha);
}

public interface ITokenGenerator
{
    // Valor aleatório para o cliente e o hash dele para o banco.
    TokenOpaco Generate();

    // O hash de um valor que o cliente apresentou, para buscar no banco.
    string Hash(string valor);
}

public sealed record TokenOpaco(string Valor, string Hash);

public interface IAccessTokenIssuer
{
    AccessToken Issue(Usuario usuario);
}

public sealed record AccessToken(string Valor, DateTimeOffset ExpiraEm);
