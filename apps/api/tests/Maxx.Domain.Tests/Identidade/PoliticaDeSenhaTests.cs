using Maxx.Domain.Identidade;

namespace Maxx.Domain.Tests.Identidade;

public class PoliticaDeSenhaTests
{
    [Theory]
    [InlineData("12345678")]
    [InlineData("senhaboa")]
    [InlineData("        ")]
    [InlineData("çãõ€ éàü")]
    public void Senha_de_8_caracteres_ou_mais_e_aceita_sem_regra_de_composicao(string senha)
    {
        PoliticaDeSenha.Validar(senha);
    }

    [Fact]
    public void Senha_de_128_caracteres_e_aceita()
    {
        PoliticaDeSenha.Validar(new string('a', 128));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567")]
    public void Senha_com_menos_de_8_e_recusada(string senha)
    {
        Assert.Throws<DomainException>(() => PoliticaDeSenha.Validar(senha));
    }

    [Fact]
    public void Senha_com_mais_de_128_e_recusada()
    {
        Assert.Throws<DomainException>(() => PoliticaDeSenha.Validar(new string('a', 129)));
    }

    [Fact]
    public void Emoji_conta_como_um_caractere()
    {
        // Sete emojis são 14 unidades de UTF-16, mas sete caracteres: curta demais.
        Assert.Throws<DomainException>(() => PoliticaDeSenha.Validar(string.Concat(Enumerable.Repeat("🔒", 7))));
        PoliticaDeSenha.Validar(string.Concat(Enumerable.Repeat("🔒", 8)));
    }

    [Fact]
    public void A_mensagem_de_recusa_nunca_contem_a_senha()
    {
        var excecao = Assert.Throws<DomainException>(() => PoliticaDeSenha.Validar("segredo"));

        Assert.DoesNotContain("segredo", excecao.Message, StringComparison.Ordinal);
    }
}
