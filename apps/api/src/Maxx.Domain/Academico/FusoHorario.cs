namespace Maxx.Domain.Academico;

// Instantes são guardados em UTC; as fronteiras de bimestre, semestre e ano são datas
// interpretadas em America/Sao_Paulo.
public static class FusoHorario
{
    public static TimeZoneInfo SaoPaulo { get; } = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static DateOnly DataLocal(DateTimeOffset instante) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, SaoPaulo).DateTime);
}
