using Maxx.Domain.Catalogo;

namespace Maxx.Domain.Tests.Catalogo;

// As faixas de produto.md 4.3, com as fronteiras exatas: "abaixo de" é estrito.
public class RaridadeTests
{
    [Theory]
    [InlineData(0, 3200, Raridade.Lendario)]
    [InlineData(0, 0, Raridade.Lendario)]
    [InlineData(31, 3200, Raridade.Lendario)] // 0,97%
    [InlineData(32, 3200, Raridade.Epico)] // 1% exato
    [InlineData(159, 3200, Raridade.Epico)] // 4,97%
    [InlineData(160, 3200, Raridade.Raro)] // 5% exato
    [InlineData(639, 3200, Raridade.Raro)] // 19,97%
    [InlineData(640, 3200, Raridade.Comum)] // 20% exato
    [InlineData(3200, 3200, Raridade.Comum)]
    [InlineData(1, 3, Raridade.Comum)]
    [InlineData(1, 100, Raridade.Epico)]
    [InlineData(1, 101, Raridade.Lendario)]
    public void Raridade_pela_escassez(int alunosComItem, int alunosElegiveis, Raridade esperada) =>
        Assert.Equal(esperada, Raridades.De(alunosComItem, alunosElegiveis));

    [Fact]
    public void Mais_alunos_com_o_item_que_elegiveis_e_erro_de_quem_chama()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Raridades.De(2, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Raridades.De(-1, 1));
    }
}
