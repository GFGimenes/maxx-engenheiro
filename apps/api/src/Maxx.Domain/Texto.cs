namespace Maxx.Domain;

internal static class Texto
{
    public static string Obrigatorio(string? valor, string campo)
    {
        var aparado = valor?.Trim();
        return string.IsNullOrEmpty(aparado)
            ? throw new DomainException($"{campo} é obrigatório.")
            : aparado;
    }
}
