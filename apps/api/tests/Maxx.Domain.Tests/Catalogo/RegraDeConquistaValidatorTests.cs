using Maxx.Domain.Catalogo;
using static Maxx.Domain.Tests.Catalogo.CatalogoDeTeste;

namespace Maxx.Domain.Tests.Catalogo;

public class RegraDeConquistaValidatorTests
{
    private static void AssertErro(ItemConquista conquista, string trecho)
    {
        var erros = CatalogoValidator.Validar(Com(conquista));

        Assert.Contains(
            erros,
            erro => erro.Referencia == conquista.Codigo && erro.Mensagem.Contains(trecho, StringComparison.Ordinal));
    }

    private static void AssertSemErro(ItemConquista conquista) =>
        Assert.Empty(CatalogoValidator.Validar(Com(conquista)));

    // filtroItens

    [Fact]
    public void Filtro_com_codigo_inexistente_e_recusado()
    {
        AssertErro(C02 with { Regra = C02.Regra with { FiltroItens = ["M99"] } }, "M99, que não existe");
    }

    [Fact]
    public void Filtro_com_item_de_outra_fonte_e_recusado()
    {
        AssertErro(C02 with { Regra = C02.Regra with { FiltroItens = ["P01"] } }, "não é da fonte Medalha");
    }

    [Fact]
    public void Filtro_com_conquista_e_recusado()
    {
        AssertErro(C02 with { Regra = C02.Regra with { FiltroItens = ["C03"] } }, "nunca conta outra conquista");
    }

    [Fact]
    public void Filtro_vazio_e_recusado()
    {
        AssertErro(C02 with { Regra = C02.Regra with { FiltroItens = [] } }, "filtroItens vazio");
    }

    [Fact]
    public void Fonte_ambos_aceita_medalha_e_premio_no_filtro()
    {
        AssertSemErro(C02 with { Regra = C02.Regra with { Fonte = FonteDeContagem.Ambos, FiltroItens = ["M01", "P01"] } });
    }

    [Fact]
    public void Fonte_ambos_ainda_recusa_conquista_no_filtro()
    {
        AssertErro(
            C02 with { Regra = C02.Regra with { Fonte = FonteDeContagem.Ambos, FiltroItens = ["M01", "C03"] } },
            "nunca conta outra conquista");
    }

    // agruparPor e forma

    [Fact]
    public void Contagem_simples_com_agrupamento_e_recusada()
    {
        AssertErro(C02 with { Regra = C02.Regra with { AgruparPor = Agrupamento.Materia } }, "agruparPor precisa ser nulo");
    }

    [Fact]
    public void Contagem_distinta_sem_agrupamento_e_recusada()
    {
        AssertErro(C06 with { Regra = C06.Regra with { AgruparPor = null } }, "precisa de agruparPor");
    }

    [Theory]
    [InlineData(Agrupamento.Materia)]
    [InlineData(Agrupamento.Bimestre)]
    public void Agrupar_premio_por_campo_de_medalha_e_recusado(Agrupamento agrupamento)
    {
        AssertErro(C06 with { Regra = C06.Regra with { AgruparPor = agrupamento } }, "só existe na fonte Medalha");
    }

    [Fact]
    public void Agrupar_medalha_por_tipo_de_evento_e_recusado()
    {
        AssertErro(
            C03 with { Regra = C03.Regra with { AgruparPor = Agrupamento.TipoDeEvento, Alvo = AlvoDeConquista.De(3) } },
            "só existe na fonte PremioParticipacao");
    }

    [Fact]
    public void Agrupar_medalha_por_bimestre_com_alvo_fixo_e_aceito()
    {
        // A forma da C14: quatro bimestres distintos com nota 10 no ano.
        AssertSemErro(C03 with
        {
            Periodo = Periodo.Ano,
            Tier = Tier.Diamante,
            Pontos = 110,
            Regra = C03.Regra with { FiltroItens = ["M01"], AgruparPor = Agrupamento.Bimestre, Alvo = AlvoDeConquista.De(4) },
        });
    }

    // alvo

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Alvo_fixo_menor_que_1_e_recusado(int alvo)
    {
        AssertErro(C02 with { Regra = C02.Regra with { Alvo = AlvoDeConquista.De(alvo) } }, "ao menos 1");
    }

    [Fact]
    public void Regra_sem_alvo_e_recusada()
    {
        AssertErro(C02 with { Regra = C02.Regra with { Alvo = null } }, "sem alvo");
    }

    [Fact]
    public void Regra_sem_fonte_e_recusada()
    {
        AssertErro(C02 with { Regra = C02.Regra with { Fonte = null } }, "sem fonte");
    }

    [Fact]
    public void Alvo_dinamico_agrupando_por_bimestre_e_recusado()
    {
        AssertErro(
            C03 with { Periodo = Periodo.Ano, Regra = C03.Regra with { AgruparPor = Agrupamento.Bimestre } },
            "agrupando por matéria");
    }

    [Fact]
    public void Alvo_dinamico_com_gatilho_por_fato_e_recusado()
    {
        AssertErro(C03 with { Gatilho = Gatilho.PorFato }, "exige gatilho fechamentoDePeriodo");
    }

    // período e gatilho

    [Fact]
    public void Conquista_de_evento_e_recusada()
    {
        AssertErro(C02 with { Periodo = Periodo.Evento }, "não de evento");
    }

    [Fact]
    public void Conquista_de_curso_com_fechamento_e_recusada()
    {
        AssertErro(C02 with { Periodo = Periodo.Curso, Gatilho = Gatilho.FechamentoDePeriodo }, "não tem fechamento");
    }

    // forma customizada

    [Fact]
    public void Regra_customizada_com_implementacao_e_aceita()
    {
        AssertSemErro(C06 with { Regra = new() { Forma = FormaDeContagem.Customizada, Implementacao = "Formatura" } });
    }

    [Fact]
    public void Regra_customizada_sem_implementacao_e_recusada()
    {
        AssertErro(C06 with { Regra = new() { Forma = FormaDeContagem.Customizada } }, "nome da implementação");
    }

    [Fact]
    public void Implementacao_fora_da_regra_customizada_e_recusada()
    {
        AssertErro(C02 with { Regra = C02.Regra with { Implementacao = "Formatura" } }, "só existe na regra customizada");
    }
}
