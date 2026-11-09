namespace Maxx.Domain.Academico;

// Instantes são guardados em UTC; as fronteiras de bimestre, semestre e ano são datas
// interpretadas em America/Sao_Paulo.
public static class FusoHorario
{
    public static TimeZoneInfo SaoPaulo { get; } = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public static DateOnly DataLocal(DateTimeOffset instante) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, SaoPaulo).DateTime);

    // O instante em UTC de uma data e hora de São Paulo (o seed grava fatos com data passada).
    public static DateTimeOffset Instante(DateOnly data, TimeOnly hora = default)
    {
        var local = data.ToDateTime(hora, DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, SaoPaulo), TimeSpan.Zero);
    }
}
