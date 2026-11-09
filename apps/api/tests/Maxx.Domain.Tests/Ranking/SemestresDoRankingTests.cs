using Maxx.Domain.Academico;
using Maxx.Domain.Ranking;
using Maxx.Domain.Tests.Academico;

namespace Maxx.Domain.Tests.Ranking;

// Que semestre a tela chama de atual e quando o ranking do semestre congela (produto.md 5):
// 30 dias depois do fim, com a mesma leitura da conquista de fechamento (passo 9).
public class SemestresDoRankingTests
{
    private readonly Calendario _calendario;
    private readonly SemestreLetivo _primeiro;
    private readonly SemestreLetivo _segundo;
    private readonly SemestreLetivo _seguinte;

    public SemestresDoRankingTests()
    {
        var ano2026 = Calendario2026.Ano();
        var ano2027 = new AnoLetivo(
            2027,
            new SemestreLetivo(2027, 1, new Bimestre(1, new(2027, 2, 1), new(2027, 4, 3)), new Bimestre(2, new(2027, 4, 5), new(2027, 6, 26))),
            new SemestreLetivo(2027, 2, new Bimestre(3, new(2027, 7, 26), new(2027, 9, 25)), new Bimestre(4, new(2027, 9, 27), new(2027, 12, 4))));
        _calendario = new Calendario([ano2026, ano2027]);
        _primeiro = ano2026.Semestres[0];
        _segundo = ano2026.Semestres[1];
        _seguinte = ano2027.Semestres[0];
    }

    [Theory]
    [InlineData(2026, 11, 10, "2026/2")]
    [InlineData(2026, 12, 5, "2026/2")]
    [InlineData(2026, 12, 21, "2026/2")]
    [InlineData(2027, 1, 31, "2026/2")]
    [InlineData(2027, 2, 1, "2027/1")]
    [InlineData(2026, 7, 1, "2026/1")]
    public void Semestre_atual_e_o_ultimo_que_ja_comecou_e_no_recesso_continua_o_que_terminou(int ano, int mes, int dia, string esperado)
    {
        Assert.Equal(esperado, SemestresDoRanking.Atual(_calendario, new DateOnly(ano, mes, dia))!.Codigo);
    }

    [Fact]
    public void Antes_do_primeiro_semestre_nao_ha_semestre_atual()
    {
        Assert.Null(SemestresDoRanking.Atual(_calendario, new DateOnly(2026, 1, 25)));
    }

    // 2026/2 termina em 05/12/2026: congela em 04/01/2027.
    [Theory]
    [InlineData(2026, 12, 5, false)]
    [InlineData(2027, 1, 3, false)]
    [InlineData(2027, 1, 4, true)]
    [InlineData(2027, 3, 1, true)]
    public void Ranking_do_semestre_congela_no_dia_do_fim_mais_30(int ano, int mes, int dia, bool congelado)
    {
        Assert.Equal(congelado, SemestresDoRanking.Congelado(_segundo, _calendario, new DateOnly(ano, mes, dia)));
    }

    [Fact]
    public void Abertos_sao_os_nao_congelados_inclusive_o_que_ainda_nao_comecou()
    {
        var noRecesso = SemestresDoRanking.Abertos(_calendario, new DateOnly(2026, 12, 21));
        var depoisDoCongelamento = SemestresDoRanking.Abertos(_calendario, new DateOnly(2027, 1, 4));

        Assert.DoesNotContain(_primeiro, noRecesso);
        Assert.Contains(_segundo, noRecesso);
        Assert.Contains(_seguinte, noRecesso);
        Assert.DoesNotContain(_segundo, depoisDoCongelamento);
        Assert.Contains(_seguinte, depoisDoCongelamento);
    }
}
