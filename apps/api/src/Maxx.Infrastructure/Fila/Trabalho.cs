namespace Maxx.Infrastructure.Fila;

// Uma linha da tabela de trabalhos (ADR 0008). Trabalho concluído sai da tabela; o que
// fica é o pendente e o falho, que esgotou as tentativas e espera alguém olhar.
public sealed class Trabalho
{
    public long Id { get; set; }

    public required string Tipo { get; set; }

    // JSON da carga, em jsonb.
    public required string Carga { get; set; }

    public EstadoDoTrabalho Estado { get; set; } = EstadoDoTrabalho.Pendente;

    public int Tentativas { get; set; }

    public DateTimeOffset CriadoEm { get; set; }

    // O worker só pega o trabalho a partir daqui: é o recuo da retentativa.
    public DateTimeOffset DisponivelEm { get; set; }

    // Tipo e mensagem da última exceção, sem pilha. Nunca a carga.
    public string? UltimoErro { get; set; }
}

public enum EstadoDoTrabalho
{
    Pendente,
    Falho,
}

// Recuo exponencial: 10 s, 20 s, 40 s e 80 s depois de cada falha; a quinta falha deixa o
// trabalho como falho, fora da fila, para não girar para sempre.
public static class PoliticaDeRetentativa
{
    public const int MaximoDeTentativas = 5;

    public static readonly TimeSpan RecuoInicial = TimeSpan.FromSeconds(10);

    // null: esgotou, o trabalho fica falho.
    public static TimeSpan? RecuoDepoisDa(int tentativas)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(tentativas, 1);
        return tentativas >= MaximoDeTentativas ? null : RecuoInicial * Math.Pow(2, tentativas - 1);
    }
}
