using Maxx.Domain.Academico;

namespace Maxx.Domain.Tests.Academico;

public class FusoHorarioTests
{
    [Fact]
    public void Instante_de_uma_data_e_hora_de_Sao_Paulo_em_UTC()
    {
        Assert.Equal(new DateTimeOffset(2026, 1, 26, 3, 0, 0, TimeSpan.Zero), FusoHorario.Instante(new DateOnly(2026, 1, 26)));
        Assert.Equal(
            new DateTimeOffset(2026, 9, 14, 22, 30, 0, TimeSpan.Zero), FusoHorario.Instante(new DateOnly(2026, 9, 14), new TimeOnly(19, 30)));
        Assert.Equal(new DateOnly(2026, 9, 14), FusoHorario.DataLocal(FusoHorario.Instante(new DateOnly(2026, 9, 14), new TimeOnly(23, 59))));
    }
}
