namespace Maxx.Domain.Premios;

// O motivo de uma reprovação, contestação ou revogação (produto.md 4.7): obrigatório, de 1 a
// 500 caracteres depois de aparado, contando um emoji como um (decisão do passo 8, D10).
public static class Motivo
{
    public const int TamanhoMaximo = 500;

    // O motivo com que a instituição resolve o prêmio quando remove o post da comprovação.
    public const string RemovidoPelaInstituicao = "Removido pela instituição.";

    // O motivo aparado, ou nulo se vazio ou longo demais.
    public static string? Normalizar(string? motivo)
    {
        var aparado = motivo?.Trim();
        return string.IsNullOrEmpty(aparado) || aparado.EnumerateRunes().Count() > TamanhoMaximo ? null : aparado;
    }
}
