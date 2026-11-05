namespace Maxx.Domain.Identidade;

// De 8 a 128 caracteres, sem regra de composição (NIST SP 800-63B). Conta caractere, não
// unidade de UTF-16: um emoji é um. A mensagem nunca repete a senha.
public static class PoliticaDeSenha
{
    public const int TamanhoMinimo = 8;

    public const int TamanhoMaximo = 128;

    public static void Validar(string? senha)
    {
        var tamanho = senha?.EnumerateRunes().Count() ?? 0;
        if (tamanho is < TamanhoMinimo or > TamanhoMaximo)
        {
            throw new DomainException($"A senha tem de {TamanhoMinimo} a {TamanhoMaximo} caracteres.");
        }
    }
}
