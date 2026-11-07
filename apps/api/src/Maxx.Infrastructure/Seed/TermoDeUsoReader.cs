using System.Globalization;
using System.Text.RegularExpressions;

namespace Maxx.Infrastructure.Seed;

// Lê data/termos/termo-de-uso.v<versão>.md, um arquivo por versão.
public static partial class TermoDeUsoReader
{
    public static IReadOnlyDictionary<int, string> Ler(string diretorio)
    {
        if (!Directory.Exists(diretorio))
        {
            throw new SeedException($"Diretório dos termos não encontrado: {diretorio}.");
        }

        var versoes = new SortedDictionary<int, string>();
        foreach (var arquivo in Directory.GetFiles(diretorio))
        {
            var nome = Path.GetFileName(arquivo);
            var encontrado = NomeDoArquivo().Match(nome);
            if (!encontrado.Success)
            {
                throw new SeedException($"Arquivo fora do padrão termo-de-uso.v<versão>.md: {nome}.");
            }

            // Fim de linha do checkout (CRLF no Windows) não é mudança de texto.
            versoes[int.Parse(encontrado.Groups[1].ValueSpan, CultureInfo.InvariantCulture)] =
                File.ReadAllText(arquivo).ReplaceLineEndings("\n").Trim();
        }

        return versoes.Count > 0 ? versoes : throw new SeedException($"Nenhum termo de uso em {diretorio}.");
    }

    [GeneratedRegex(@"^termo-de-uso\.v([1-9][0-9]{0,8})\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex NomeDoArquivo();
}
