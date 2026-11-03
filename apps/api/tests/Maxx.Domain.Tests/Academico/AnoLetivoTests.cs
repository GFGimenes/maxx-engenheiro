using Maxx.Domain.Academico;
using static Maxx.Domain.Tests.Academico.Calendario2026;

namespace Maxx.Domain.Tests.Academico;

public class AnoLetivoTests
{
    [Fact]
    public void Ano_tem_dois_semestres_e_cada_semestre_dois_bimestres()
    {
        var ano = Ano();

        Assert.Equal([1, 2], ano.Semestres.Select(semestre => semestre.Numero));
        Assert.Equal([1, 2], ano.Semestres[0].Bimestres.Select(bimestre => bimestre.Numero));
        Assert.Equal([3, 4], ano.Semestres[1].Bimestres.Select(bimestre => bimestre.Numero));
    }

    [Fact]
    public void Semestre_vai_do_inicio_do_primeiro_bimestre_ao_fim_do_segundo()
    {
        var semestre = Segundo();

        Assert.Equal("2026/2", semestre.Codigo);
        Assert.Equal(Data(7, 27), semestre.Inicio);
        Assert.Equal(Data(12, 5), semestre.Fim);
    }

    [Fact]
    public void Bimestre_que_termina_antes_de_comecar_e_recusado()
    {
        Assert.Throws<DomainException>(() => new Bimestre(1, Data(4, 3), Data(1, 26)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Bimestre_fora_de_1_a_4_e_recusado(int numero)
    {
        Assert.Throws<DomainException>(() => new Bimestre(numero, Data(1, 26), Data(4, 3)));
    }

    [Fact]
    public void Bimestres_que_se_sobrepoem_sao_recusados()
    {
        Assert.Throws<DomainException>(() => new SemestreLetivo(
            2026, 1, new Bimestre(1, Data(1, 26), Data(4, 10)), new Bimestre(2, Data(4, 6), Data(6, 27))));
    }

    [Fact]
    public void Bimestres_fora_de_ordem_sao_recusados()
    {
        Assert.Throws<DomainException>(() => new SemestreLetivo(
            2026, 1, new Bimestre(2, Data(4, 6), Data(6, 27)), new Bimestre(1, Data(1, 26), Data(4, 3))));
    }

    [Fact]
    public void Semestre_1_com_bimestres_do_semestre_2_e_recusado()
    {
        Assert.Throws<DomainException>(() => new SemestreLetivo(
            2026, 1, new Bimestre(3, Data(7, 27), Data(9, 26)), new Bimestre(4, Data(9, 28), Data(12, 5))));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Semestre_fora_de_1_e_2_e_recusado(int numero)
    {
        Assert.Throws<DomainException>(() => new SemestreLetivo(
            2026, numero, new Bimestre(1, Data(1, 26), Data(4, 3)), new Bimestre(2, Data(4, 6), Data(6, 27))));
    }

    [Fact]
    public void Semestres_fora_de_ordem_sao_recusados()
    {
        Assert.Throws<DomainException>(() => new AnoLetivo(2026, Segundo(), Primeiro()));
    }

    [Fact]
    public void Semestre_de_outro_ano_e_recusado()
    {
        var de2027 = new SemestreLetivo(
            2027, 2, new Bimestre(3, Data(7, 27), Data(9, 26)), new Bimestre(4, Data(9, 28), Data(12, 5)));

        Assert.Throws<DomainException>(() => new AnoLetivo(2026, Primeiro(), de2027));
    }

    [Fact]
    public void Semestres_que_se_sobrepoem_sao_recusados()
    {
        var segundoCedo = new SemestreLetivo(
            2026, 2, new Bimestre(3, Data(6, 1), Data(9, 26)), new Bimestre(4, Data(9, 28), Data(12, 5)));

        Assert.Throws<DomainException>(() => new AnoLetivo(2026, Primeiro(), segundoCedo));
    }
}
