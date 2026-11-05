using System.Globalization;
using System.Text;

namespace Maxx.Domain.Identidade;

// A parte do arroba antes do hífen: a única que o usuário escolhe e a que é única no
// sistema, sem diferenciar maiúsculas (produto.md 2.3.1). Saber se um prefixo está livre
// é pergunta ao banco; aqui só a forma e a ordem das sugestões.
public static class PrefixoDeArroba
{
    public const int TamanhoMinimo = 3;

    public const int TamanhoMaximo = 30;

    // Teto de segurança: nenhum nome real chega perto disso.
    private const int _tentativasNumeradas = 10_000;

    private static readonly HashSet<string> _particulas =
        new(["da", "de", "do", "das", "dos", "e"], StringComparer.OrdinalIgnoreCase);

    public static bool Mesmo(string prefixo, string outro) =>
        string.Equals(prefixo, outro, StringComparison.OrdinalIgnoreCase);

    // Letras ASCII e dígitos, de 3 a 30, começando por letra. Devolve como foi escrito.
    public static string Validar(string? prefixo)
    {
        if (prefixo is null
            || prefixo.Length < TamanhoMinimo
            || prefixo.Length > TamanhoMaximo
            || !char.IsAsciiLetter(prefixo[0])
            || !prefixo.All(char.IsAsciiLetterOrDigit))
        {
            throw new DomainException(
                $"O arroba tem de {TamanhoMinimo} a {TamanhoMaximo} letras sem acento e números, começando por letra.");
        }

        return prefixo;
    }

    // A primeira sugestão livre, na ordem de Sugestoes. estaLivre recebe o prefixo como
    // sugerido e responde sem diferenciar maiúsculas.
    public static string Sugerir(string nomeCompleto, Func<string, bool> estaLivre)
    {
        ArgumentNullException.ThrowIfNull(estaLivre);

        return Sugestoes(nomeCompleto).FirstOrDefault(estaLivre)
            ?? throw new DomainException($"Nenhuma sugestão de arroba livre para {nomeCompleto}.");
    }

    // Primeiro e último nome; depois com o primeiro nome do meio; depois primeiro e último
    // com número a partir de 2. Só candidatos válidos, sem repetição.
    public static IEnumerable<string> Sugestoes(string nomeCompleto)
    {
        var nomes = Nomes(nomeCompleto);
        var primeiroEUltimo = nomes.Count == 1 ? Juntar(nomes) : Juntar([nomes[0], nomes[^1]]);
        var comONomeDoMeio = nomes.Count >= 3 ? Juntar([nomes[0], nomes[1], nomes[^1]]) : null;

        return Candidatos(primeiroEUltimo, comONomeDoMeio)
            .Where(EhValido)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> Candidatos(string primeiroEUltimo, string? comONomeDoMeio)
    {
        yield return Cortar(primeiroEUltimo, TamanhoMaximo);

        if (comONomeDoMeio is not null)
        {
            yield return Cortar(comONomeDoMeio, TamanhoMaximo);
        }

        for (var numero = 2; numero < _tentativasNumeradas; numero++)
        {
            var sufixo = numero.ToString(CultureInfo.InvariantCulture);
            yield return Cortar(primeiroEUltimo, TamanhoMaximo - sufixo.Length) + sufixo;
        }
    }

    // Nome completo em palavras aproveitáveis: sem acento, sem cedilha, só letras e
    // dígitos ASCII, sem partícula.
    private static List<string> Nomes(string nomeCompleto)
    {
        var nomes = Texto.Obrigatorio(nomeCompleto, "O nome completo")
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(SoAscii)
            .Where(nome => nome.Length > 0 && !_particulas.Contains(nome))
            .ToList();

        if (nomes.Count == 0 || !char.IsAsciiLetter(nomes[0][0]))
        {
            throw new DomainException($"Não dá para sugerir arroba a partir de {nomeCompleto}.");
        }

        return nomes;
    }

    private static string SoAscii(string palavra)
    {
        var resultado = new StringBuilder(palavra.Length);
        foreach (var caractere in palavra.Normalize(NormalizationForm.FormD))
        {
            // A decomposição separa a letra do acento e da cedilha; o que sobra fora do
            // ASCII (acento solto, apóstrofo, hífen) é descartado.
            if (char.IsAsciiLetterOrDigit(caractere))
            {
                resultado.Append(caractere);
            }
        }

        return resultado.ToString();
    }

    private static string Juntar(IEnumerable<string> nomes) =>
        string.Concat(nomes.Select((nome, indice) => indice == 0
            ? nome.ToLowerInvariant()
            : char.ToUpperInvariant(nome[0]) + nome[1..].ToLowerInvariant()));

    private static string Cortar(string texto, int tamanho) => texto.Length <= tamanho ? texto : texto[..tamanho];

    private static bool EhValido(string prefixo) =>
        prefixo.Length is >= TamanhoMinimo and <= TamanhoMaximo
        && char.IsAsciiLetter(prefixo[0])
        && prefixo.All(char.IsAsciiLetterOrDigit);
}
