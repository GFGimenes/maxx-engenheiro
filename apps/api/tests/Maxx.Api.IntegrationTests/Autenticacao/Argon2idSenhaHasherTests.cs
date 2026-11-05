using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Maxx.Infrastructure.Autenticacao;

namespace Maxx.Api.IntegrationTests.Autenticacao;

public class Argon2idSenhaHasherTests
{
    private readonly Argon2idSenhaHasher _hasher = new();

    [Fact]
    public void Hash_sai_no_formato_phc_com_os_parametros_do_owasp()
    {
        var hash = _hasher.Hash("senha-correta");

        Assert.StartsWith("$argon2id$v=19$m=19456,t=2,p=1$", hash, StringComparison.Ordinal);
        Assert.Equal(6, hash.Split('$').Length);
    }

    [Fact]
    public void Hash_nunca_contem_a_senha()
    {
        Assert.DoesNotContain("senha-correta", _hasher.Hash("senha-correta"), StringComparison.Ordinal);
    }

    [Fact]
    public void Senha_certa_verifica_e_errada_nao()
    {
        var hash = _hasher.Hash("senha-correta");

        Assert.True(_hasher.Verify("senha-correta", hash));
        Assert.False(_hasher.Verify("senha-errada", hash));
        Assert.False(_hasher.Verify("Senha-correta", hash));
    }

    [Fact]
    public void Mesma_senha_gera_hashes_diferentes_pelo_sal()
    {
        Assert.NotEqual(_hasher.Hash("senha-correta"), _hasher.Hash("senha-correta"));
    }

    [Fact]
    public void Senha_com_acento_e_emoji_vai_e_volta()
    {
        var hash = _hasher.Hash("çãõ€ éà 🔒");

        Assert.True(_hasher.Verify("çãõ€ éà 🔒", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("texto")]
    [InlineData("$argon2i$v=19$m=19456,t=2,p=1$c2Fs$aGFzaA")]
    [InlineData("$argon2id$v=19$m=19456,t=2$c2Fs$aGFzaA")]
    [InlineData("$argon2id$v=19$m=abc,t=2,p=1$c2Fs$aGFzaA")]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=1$***$aGFzaA")]
    public void Hash_malformado_nao_verifica_e_nao_lanca(string hash)
    {
        Assert.False(_hasher.Verify("senha-correta", hash));
    }

    [Fact]
    public void Hash_com_parametros_antigos_continua_verificando()
    {
        // Um hash de quando os parâmetros eram menores: os parâmetros moram no próprio hash.
        var sal = RandomNumberGenerator.GetBytes(16);
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes("senha-correta"))
        {
            Salt = sal,
            MemorySize = 8192,
            Iterations = 1,
            DegreeOfParallelism = 1,
        };
        var calculado = argon2.GetBytes(32);
        var hash = $"$argon2id$v=19$m=8192,t=1,p=1${Convert.ToBase64String(sal).TrimEnd('=')}${Convert.ToBase64String(calculado).TrimEnd('=')}";

        Assert.True(_hasher.Verify("senha-correta", hash));
    }

    [Fact]
    public void Ce_cedilha_composto_e_decomposto_sao_a_mesma_senha()
    {
        // U+00E7 de um teclado; c + U+0327 (cedilha combinante) de outro.
        const string composto = "coração-forte";
        const string decomposto = "coração-forte";
        Assert.NotEqual(composto, decomposto);

        Assert.True(_hasher.Verify(decomposto, _hasher.Hash(composto)));
        Assert.True(_hasher.Verify(composto, _hasher.Hash(decomposto)));
    }

    [Fact]
    public void Senha_vazia_nao_verifica_nem_lanca()
    {
        // Campo de senha vazio no login: recusa comum, nunca 500.
        var hash = _hasher.Hash("senha-correta");

        Assert.False(_hasher.Verify(string.Empty, hash));
    }

    [Fact]
    public void Hash_de_senha_vazia_e_erro_de_quem_chama()
    {
        // A política de senha barra antes; chegar aqui vazia é bug.
        Assert.Throws<ArgumentException>(() => _hasher.Hash(string.Empty));
    }

    [Fact]
    public void Verificacao_ficticia_roda_sem_lancar()
    {
        _hasher.VerifyDummy("qualquer-uma");
        _hasher.VerifyDummy(null!);
    }
}
