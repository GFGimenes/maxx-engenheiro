using Maxx.Domain.Academico;
using static Maxx.Domain.Tests.Academico.Calendario2026;

namespace Maxx.Domain.Tests.Academico;

public class CalendarioTests
{
    private readonly Calendario _calendario = new([Ano()]);

    private static DateTimeOffset Utc(int mes, int dia, int hora, int minuto = 0) =>
        new(2026, mes, dia, hora, minuto, 0, TimeSpan.Zero);

    [Fact]
    public void Ultimo_minuto_do_bimestre_em_Sao_Paulo_ainda_e_do_bimestre()
    {
        // 03/04 23:59 em São Paulo (UTC-3) é 04/04 02:59 em UTC.
        var bimestre = _calendario.BimestreEm(Utc(4, 4, 2, 59));

        Assert.Equal(1, bimestre?.Numero);
    }

    [Fact]
    public void Meia_noite_do_dia_seguinte_em_Sao_Paulo_ja_e_fora_do_bimestre()
    {
        // 04/04 00:00 em São Paulo é 04/04 03:00 em UTC: sábado entre os bimestres 1 e 2.
        Assert.Null(_calendario.BimestreEm(Utc(4, 4, 3)));
    }

    [Fact]
    public void Primeiro_minuto_do_bimestre_em_Sao_Paulo_ja_e_do_bimestre()
    {
        // 27/07 00:00 em São Paulo é 27/07 03:00 em UTC; 02:59 em UTC ainda é 26/07.
        Assert.Null(_calendario.BimestreEm(Utc(7, 27, 2, 59)));
        Assert.Equal(3, _calendario.BimestreEm(Utc(7, 27, 3))?.Numero);
    }

    [Fact]
    public void Instante_com_outro_deslocamento_e_lido_pelo_relogio_de_Sao_Paulo()
    {
        // 06/12 00:30 em UTC+1 já é dia 6 lá, mas é 05/12 20:30 em São Paulo: último dia do bimestre 4.
        var instante = new DateTimeOffset(2026, 12, 6, 0, 30, 0, TimeSpan.FromHours(1));

        Assert.Equal(4, _calendario.BimestreEm(instante)?.Numero);
    }

    [Theory]
    [InlineData(7, 15)]
    [InlineData(12, 20)]
    [InlineData(1, 10)]
    public void Instante_no_recesso_nao_cai_em_bimestre_nem_semestre(int mes, int dia)
    {
        var instante = Utc(mes, dia, 15);

        Assert.Null(_calendario.BimestreEm(instante));
        Assert.Null(_calendario.SemestreEm(instante));
    }

    [Fact]
    public void Intervalo_entre_bimestres_do_mesmo_semestre_e_do_semestre()
    {
        // Sábado 26/09 fecha o bimestre 3; domingo 27/09 não é bimestre, mas é 2026/2.
        var domingo = Utc(9, 27, 15);

        Assert.Null(_calendario.BimestreEm(domingo));
        Assert.Equal("2026/2", _calendario.SemestreEm(domingo)?.Codigo);
    }

    [Fact]
    public void Ano_repetido_no_calendario_e_recusado()
    {
        Assert.Throws<DomainException>(() => new Calendario([Ano(), Ano()]));
    }
}
