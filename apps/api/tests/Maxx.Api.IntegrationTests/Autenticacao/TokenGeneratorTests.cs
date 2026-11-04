using Maxx.Infrastructure.Autenticacao;

namespace Maxx.Api.IntegrationTests.Autenticacao;

public class TokenGeneratorTests
{
    private readonly TokenGenerator _gerador = new();

    [Fact]
    public void Valor_tem_256_bits_em_base64url_e_o_hash_e_sha256_em_hexadecimal()
    {
        var token = _gerador.Generate();

        Assert.Equal(43, token.Valor.Length);
        Assert.All(token.Valor, caractere => Assert.True(char.IsAsciiLetterOrDigit(caractere) || caractere is '-' or '_'));
        Assert.Equal(64, token.Hash.Length);
        Assert.All(token.Hash, caractere => Assert.True(char.IsAsciiHexDigitLower(caractere) || char.IsAsciiDigit(caractere)));
    }

    [Fact]
    public void Hash_do_valor_apresentado_bate_com_o_gerado()
    {
        var token = _gerador.Generate();

        Assert.Equal(token.Hash, _gerador.Hash(token.Valor));
        Assert.NotEqual(token.Hash, _gerador.Hash(token.Valor + "x"));
    }

    [Fact]
    public void Cada_valor_e_novo()
    {
        var valores = Enumerable.Range(0, 1000).Select(_ => _gerador.Generate().Valor).ToHashSet();

        Assert.Equal(1000, valores.Count);
    }
}
