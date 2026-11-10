using Maxx.Domain.Identidade;

namespace Maxx.Domain.Tests.Identidade;

public class DominiosDeEmailAceitosTests
{
    private static readonly DominiosDeEmailAceitos _faculdade =
        new(["al.unieduk.com.br", "prof.unieduk.com.br", "unieduk.com.br"]);

    [Theory]
    [InlineData("ana.souza@al.unieduk.com.br")]
    [InlineData("renata@prof.unieduk.com.br")]
    [InlineData("coordenacao@unieduk.com.br")]
    [InlineData("Ana.Souza@AL.UniEduk.com.BR")]
    public void Aceita_os_dominios_da_lista_sem_diferenciar_maiusculas(string email) =>
        Assert.True(_faculdade.Aceita(email));

    [Theory]
    [InlineData("ana@gmail.com")]
    [InlineData("ana@x.al.unieduk.com.br")]
    [InlineData("ana@unieduk.com.br.evil.com")]
    [InlineData("ana@unieduk.com")]
    [InlineData("sem-arroba")]
    [InlineData("ana@")]
    public void Recusa_o_que_nao_e_exatamente_um_dominio_da_lista(string email) =>
        Assert.False(_faculdade.Aceita(email));

    [Fact]
    public void A_lista_tira_espacos_arroba_inicial_e_maiusculas()
    {
        var dominios = new DominiosDeEmailAceitos([" @Al.UniEduk.com.br ", "al.unieduk.com.br"]);

        Assert.Equal(["al.unieduk.com.br"], dominios.Dominios);
        Assert.True(dominios.Aceita("ana@al.unieduk.com.br"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ana@al.unieduk.com.br")]
    [InlineData("al unieduk.com.br")]
    public void Dominio_mal_formado_na_lista_e_erro(string dominio) =>
        Assert.Throws<DomainException>(() => new DominiosDeEmailAceitos([dominio]));

    [Fact]
    public void Lista_vazia_e_erro() =>
        Assert.Throws<DomainException>(() => new DominiosDeEmailAceitos([]));
}
