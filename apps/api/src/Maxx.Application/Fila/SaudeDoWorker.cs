namespace Maxx.Application.Fila;

// A saúde do worker (ADR 0005, passo 13): o worker morto congela o ranking sem erro em tela,
// e por isso a API responde por ele, para um monitor de fora alertar. Três problemas:
// worker parado (sem batimento recente), fila parada (trabalho disponível esperando demais)
// e trabalho que ficou falho na janela.
public interface IConsultaDaSaudeDoWorker
{
    Task<SaudeDoWorker> ConsultarAsync(CancellationToken cancellationToken);
}

public sealed class SaudeDoWorkerOptions
{
    public const string Section = "SaudeDoWorker";

    // A cada quanto o worker grava o batimento. Menor que o máximo, com folga para um
    // trabalho que leve o tempo inteiro (FilaOptions.TempoMaximoPorTrabalho, 60 s).
    public TimeSpan IntervaloDoBatimento { get; set; } = TimeSpan.FromSeconds(15);

    public TimeSpan BatimentoMaximo { get; set; } = TimeSpan.FromMinutes(2);

    public TimeSpan EsperaMaximaNaFila { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan JanelaDeFalhos { get; set; } = TimeSpan.FromHours(24);
}

public static class ProblemasDoWorker
{
    public const string WorkerParado = "worker-parado";
    public const string FilaParada = "fila-parada";
    public const string TrabalhoFalho = "trabalho-falho";
}

public sealed record SaudeDoWorker(
    TimeSpan? DesdeOBatimento,
    TimeSpan? EsperaDoMaisAntigo,
    int Disponiveis,
    int FalhosNaJanela,
    IReadOnlyList<string> Problemas)
{
    public bool Saudavel => Problemas.Count == 0;

    // A espera conta da chegada à fila ou de quando o trabalho ficou disponível, o que vier
    // depois: o seed enfileira trabalhos com datas passadas, que não estão parados.
    public static SaudeDoWorker Avaliar(
        DateTimeOffset agora,
        DateTimeOffset? batidoEm,
        DateTimeOffset? maisAntigoDisponivelDesde,
        int disponiveis,
        int falhosNaJanela,
        SaudeDoWorkerOptions limites)
    {
        ArgumentNullException.ThrowIfNull(limites);
        var desdeOBatimento = agora - batidoEm;
        var espera = agora - maisAntigoDisponivelDesde;

        var problemas = new List<string>();
        if (desdeOBatimento is not { } batimento || batimento > limites.BatimentoMaximo)
        {
            problemas.Add(ProblemasDoWorker.WorkerParado);
        }

        if (espera > limites.EsperaMaximaNaFila)
        {
            problemas.Add(ProblemasDoWorker.FilaParada);
        }

        if (falhosNaJanela > 0)
        {
            problemas.Add(ProblemasDoWorker.TrabalhoFalho);
        }

        return new(desdeOBatimento, espera, disponiveis, falhosNaJanela, problemas);
    }
}
