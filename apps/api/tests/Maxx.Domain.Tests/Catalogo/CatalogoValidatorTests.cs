using Maxx.Domain.Catalogo;
using static Maxx.Domain.Tests.Catalogo.CatalogoDeTeste;

namespace Maxx.Domain.Tests.Catalogo;

public class CatalogoValidatorTests
{
    private static void AssertErro(Domain.Catalogo.Catalogo catalogo, string referencia, string trecho)
    {
        var erros = CatalogoValidator.Validar(catalogo);

        Assert.Contains(erros, erro => erro.Referencia == referencia && erro.Mensagem.Contains(trecho, StringComparison.Ordinal));
    }

    [Fact]
    public void Catalogo_valido_nao_tem_erro()
    {
        Assert.Empty(CatalogoValidator.Validar(Valido()));
    }

    [Fact]
    public void Devolve_todos_os_erros_de_uma_vez()
    {
        var catalogo = Valido() with
        {
            Itens = [M01 with { Pontos = 31 }, M03 with { Codigo = "X03" }, M10, P01, C02, C03, C06],
        };

        Assert.True(CatalogoValidator.Validar(catalogo).Count >= 2);
    }

    // Códigos

    [Fact]
    public void Codigo_repetido_e_recusado()
    {
        var catalogo = Valido() with { Itens = [.. Valido().Itens, M01] };

        AssertErro(catalogo, "M01", "código repetido");
    }

    [Theory]
    [InlineData("P05")]
    [InlineData("M")]
    [InlineData("M1A")]
    public void Codigo_de_medalha_e_M_seguido_de_digitos(string codigo)
    {
        AssertErro(Com(M03 with { Codigo = codigo }), codigo, "M seguido de dígitos");
    }

    [Fact]
    public void Codigo_de_conquista_com_prefixo_de_premio_e_recusado()
    {
        AssertErro(Com(C06 with { Codigo = "P06" }), "P06", "C seguido de dígitos");
    }

    [Fact]
    public void Nome_vazio_e_recusado()
    {
        AssertErro(Com(P01 with { Nome = " " }), "P01", "nome vazio");
    }

    // Tier e pontos

    [Theory]
    [InlineData(Tier.Bronze, 0)]
    [InlineData(Tier.Bronze, 31)]
    [InlineData(Tier.Prata, 30)]
    [InlineData(Tier.Prata, 71)]
    [InlineData(Tier.Ouro, 70)]
    [InlineData(Tier.Ouro, 101)]
    [InlineData(Tier.Diamante, 100)]
    [InlineData(Tier.Diamante, 151)]
    public void Pontos_fora_da_faixa_do_tier_sao_recusados(Tier tier, int pontos)
    {
        AssertErro(Com(C06 with { Tier = tier, Pontos = pontos }), "C06", "fora da faixa");
    }

    [Theory]
    [InlineData(Tier.Bronze, 1)]
    [InlineData(Tier.Bronze, 30)]
    [InlineData(Tier.Prata, 31)]
    [InlineData(Tier.Prata, 70)]
    [InlineData(Tier.Ouro, 71)]
    [InlineData(Tier.Ouro, 100)]
    [InlineData(Tier.Diamante, 101)]
    [InlineData(Tier.Diamante, 150)]
    public void Pontos_nas_pontas_da_faixa_sao_aceitos(Tier tier, int pontos)
    {
        Assert.Empty(CatalogoValidator.Validar(Com(C06 with { Tier = tier, Pontos = pontos })));
    }

    // Medalha

    [Fact]
    public void Medalha_de_bimestre_que_nao_e_bronze_e_recusada()
    {
        AssertErro(Com(M03 with { Tier = Tier.Prata, Pontos = 35 }), "M03", "fechamento de matéria");
    }

    [Fact]
    public void Medalha_prata_de_semestre_e_aceita()
    {
        Assert.Empty(CatalogoValidator.Validar(Com(M10)));
    }

    [Fact]
    public void Medalha_de_ano_e_recusada()
    {
        AssertErro(Com(M10 with { PeriodoDeReferencia = Periodo.Ano }), "M10", "bimestre ou de semestre");
    }

    [Fact]
    public void Medalha_repetivel_sem_ocorrencia_na_chave_e_recusada()
    {
        var semOcorrencia = M01.ChaveDeDeduplicacao.Where(campo => campo != CampoDeDeduplicacao.Ocorrencia).ToList();

        AssertErro(Com(M01 with { ChaveDeDeduplicacao = semOcorrencia }), "M01", "se, e só se");
    }

    [Fact]
    public void Medalha_nao_repetivel_com_ocorrencia_na_chave_e_recusada()
    {
        AssertErro(
            Com(M03 with { ChaveDeDeduplicacao = [.. M03.ChaveDeDeduplicacao, CampoDeDeduplicacao.Ocorrencia] }),
            "M03",
            "se, e só se");
    }

    [Fact]
    public void Medalha_repetivel_com_limite_1_e_recusada()
    {
        AssertErro(Com(M01 with { LimitePorPeriodo = 1 }), "M01", "ao menos 2");
    }

    [Fact]
    public void Medalha_nao_repetivel_com_limite_2_e_recusada()
    {
        AssertErro(Com(M03 with { LimitePorPeriodo = 2 }), "M03", "limite por período 1");
    }

    [Fact]
    public void Chave_de_medalha_sem_o_campo_do_periodo_e_recusada()
    {
        var comBimestre = M10.ChaveDeDeduplicacao
            .Select(campo => campo == CampoDeDeduplicacao.Semestre ? CampoDeDeduplicacao.Bimestre : campo)
            .ToList();

        AssertErro(Com(M10 with { ChaveDeDeduplicacao = comBimestre }), "M10", "período de referência");
    }

    [Fact]
    public void Chave_sem_aluno_e_recusada()
    {
        var semAluno = M03.ChaveDeDeduplicacao.Where(campo => campo != CampoDeDeduplicacao.Aluno).ToList();

        AssertErro(Com(M03 with { ChaveDeDeduplicacao = semAluno }), "M03", "precisa do aluno");
    }

    [Fact]
    public void Chave_de_medalha_com_evento_e_recusada()
    {
        AssertErro(
            Com(M03 with { ChaveDeDeduplicacao = [.. M03.ChaveDeDeduplicacao, CampoDeDeduplicacao.Evento] }),
            "M03",
            "Evento não pertence");
    }

    [Fact]
    public void Campo_repetido_na_chave_e_recusado()
    {
        AssertErro(
            Com(M03 with { ChaveDeDeduplicacao = [.. M03.ChaveDeDeduplicacao, CampoDeDeduplicacao.Materia] }),
            "M03",
            "campo repetido");
    }

    // Prêmio de participação

    [Fact]
    public void Chave_de_premio_sem_evento_nem_data_e_recusada()
    {
        AssertErro(Com(P01 with { ChaveDeDeduplicacao = [CampoDeDeduplicacao.Aluno] }), "P01", "evento ou da data");
    }

    [Fact]
    public void Chave_de_premio_com_data_do_evento_e_materia_e_aceita()
    {
        var apresentacao = P01 with
        {
            ChaveDeDeduplicacao =
            [
                CampoDeDeduplicacao.Aluno, CampoDeDeduplicacao.Item, CampoDeDeduplicacao.DataDoEvento, CampoDeDeduplicacao.Materia,
            ],
        };

        Assert.Empty(CatalogoValidator.Validar(Com(apresentacao)));
    }

    [Fact]
    public void Chave_de_premio_com_ocorrencia_e_recusada()
    {
        AssertErro(
            Com(P01 with { ChaveDeDeduplicacao = [.. P01.ChaveDeDeduplicacao, CampoDeDeduplicacao.Ocorrencia] }),
            "P01",
            "Ocorrencia não pertence");
    }

    [Fact]
    public void Premio_com_periodo_que_nao_e_evento_e_recusado()
    {
        AssertErro(Com(P01 with { PeriodoDeReferencia = Periodo.Semestre }), "P01", "semestre do evento");
    }

    [Fact]
    public void Premio_sem_tipo_de_evento_e_recusado()
    {
        AssertErro(Com(P01 with { TipoDeEvento = "" }), "P01", "tipo de evento vazio");
    }

    // Vigência

    [Fact]
    public void Vigencia_que_termina_antes_de_comecar_e_recusada()
    {
        var vigencia = new Vigencia(new DateOnly(2026, 12, 1), new DateOnly(2026, 1, 1));

        AssertErro(Com(P01 with { Vigencia = vigencia }), "P01", "vigência");
    }

    [Fact]
    public void Vigencia_so_com_fim_e_aceita()
    {
        Assert.Empty(CatalogoValidator.Validar(Com(P01 with { Vigencia = new Vigencia(null, new DateOnly(2026, 12, 5)) })));
    }
}
