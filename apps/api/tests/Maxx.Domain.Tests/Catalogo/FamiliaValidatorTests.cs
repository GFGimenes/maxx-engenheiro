using Maxx.Domain.Catalogo;
using static Maxx.Domain.Tests.Catalogo.CatalogoDeTeste;

namespace Maxx.Domain.Tests.Catalogo;

public class FamiliaValidatorTests
{
    private static void AssertErro(Domain.Catalogo.Catalogo catalogo, string referencia, string trecho)
    {
        var erros = CatalogoValidator.Validar(catalogo);

        Assert.Contains(erros, erro => erro.Referencia == referencia && erro.Mensagem.Contains(trecho, StringComparison.Ordinal));
    }

    [Fact]
    public void Item_em_familia_inexistente_e_recusado()
    {
        AssertErro(Com(M03 with { Familia = "presenca-materia", OrdemNaFamilia = 1 }), "M03", "presenca-materia não existe");
    }

    [Fact]
    public void Familia_sem_ordem_e_recusada()
    {
        AssertErro(Com(M03 with { Familia = "nota-prova" }), "M03", "vêm juntas");
    }

    [Fact]
    public void Ordem_sem_familia_e_recusada()
    {
        AssertErro(Com(M03 with { OrdemNaFamilia = 1 }), "M03", "vêm juntas");
    }

    [Fact]
    public void Ordem_repetida_na_familia_e_recusada()
    {
        AssertErro(Com(C02 with { OrdemNaFamilia = 1 }), "nota-prova", "[1, 1]");
    }

    [Fact]
    public void Buraco_na_ordem_da_familia_e_recusado()
    {
        AssertErro(Com(C02 with { OrdemNaFamilia = 3 }), "nota-prova", "[1, 3]");
    }

    [Fact]
    public void Passos_declarados_diferentes_dos_itens_sao_recusados()
    {
        var catalogo = Valido() with { Familias = [NotaProva with { Passos = 3 }] };

        AssertErro(catalogo, "nota-prova", "declara 3 passos");
    }

    [Fact]
    public void Pontos_que_nao_sobem_na_familia_sao_recusados()
    {
        AssertErro(Com(C02 with { Tier = Tier.Bronze, Pontos = 30 }), "nota-prova", "precisam subir");
    }

    [Fact]
    public void Familia_repetida_e_recusada()
    {
        var catalogo = Valido() with { Familias = [NotaProva, NotaProva] };

        AssertErro(catalogo, "nota-prova", "família repetida");
    }

    [Fact]
    public void Familia_pode_misturar_medalha_e_conquista()
    {
        // nota-prova vai da medalha M01 às conquistas C02, C08, C04 e C14.
        Assert.Empty(CatalogoValidator.Validar(Valido()));
    }
}
