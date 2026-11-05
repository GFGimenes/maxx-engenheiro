using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Maxx.Application.Autenticacao;

namespace Maxx.Infrastructure.Autenticacao;

// Refresh e token de recuperação: 32 bytes aleatórios em base64url para o cliente, e o
// SHA-256 em hexadecimal para o banco. Valor com 256 bits de entropia não precisa de sal
// nem de hash lento: ninguém adivinha por força bruta.
public sealed class TokenGenerator : ITokenGenerator
{
    private const int _tamanho = 32;

    public TokenOpaco Generate()
    {
        var valor = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(_tamanho));
        return new TokenOpaco(valor, Hash(valor));
    }

    public string Hash(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));
    }
}
