using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Maxx.Application.Autenticacao;

namespace Maxx.Infrastructure.Autenticacao;

// Argon2id com os parâmetros do OWASP: 19 MiB, 2 iterações, paralelismo 1
// (decisoes/README.md, Autenticação). O hash sai no formato PHC, com os parâmetros
// dentro: se um dia eles subirem, os hashes antigos continuam verificando.
public sealed class Argon2idSenhaHasher : ISenhaHasher
{
    public const int MemoriaEmKiB = 19 * 1024;

    public const int Iteracoes = 2;

    public const int Paralelismo = 1;

    private const int _tamanhoDoSal = 16;

    private const int _tamanhoDoHash = 32;

    private const string _prefixo = "$argon2id$v=19$";

    // Placeholder para senha vazia, que o Argon2 não aceita: o custo é o mesmo e a
    // verificação dá falso.
    private const string _senhaVazia = "\0";

    private readonly Lazy<string> _hashFicticio;

    public Argon2idSenhaHasher()
    {
        _hashFicticio = new Lazy<string>(() => Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))));
    }

    public string Hash(string senha)
    {
        ArgumentException.ThrowIfNullOrEmpty(senha);

        var sal = RandomNumberGenerator.GetBytes(_tamanhoDoSal);
        var hash = Calcular(senha, sal, MemoriaEmKiB, Iteracoes, Paralelismo);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{_prefixo}m={MemoriaEmKiB},t={Iteracoes},p={Paralelismo}${Base64SemPreenchimento(sal)}${Base64SemPreenchimento(hash)}");
    }

    public bool Verify(string senha, string hash)
    {
        ArgumentNullException.ThrowIfNull(senha);

        if (!TryLer(hash, out var memoria, out var iteracoes, out var paralelismo, out var sal, out var esperado))
        {
            return false;
        }

        var calculado = Calcular(senha.Length == 0 ? _senhaVazia : senha, sal, memoria, iteracoes, paralelismo, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(calculado, esperado) && senha.Length > 0;
    }

    public void VerifyDummy(string senha) => Verify(senha ?? string.Empty, _hashFicticio.Value);

    private static byte[] Calcular(
        string senha, byte[] sal, int memoria, int iteracoes, int paralelismo, int tamanho = _tamanhoDoHash)
    {
        // NFC antes do hash, no cadastro e no login: o "ç" composto de um teclado e o
        // decomposto de outro são a mesma senha.
        var bytes = Encoding.UTF8.GetBytes(senha.Normalize(NormalizationForm.FormC));
        try
        {
            using var argon2 = new Argon2id(bytes)
            {
                Salt = sal,
                MemorySize = memoria,
                Iterations = iteracoes,
                DegreeOfParallelism = paralelismo,
            };
            return argon2.GetBytes(tamanho);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    // $argon2id$v=19$m=19456,t=2,p=1$<sal>$<hash>
    private static bool TryLer(
        string? hash, out int memoria, out int iteracoes, out int paralelismo, out byte[] sal, out byte[] esperado)
    {
        memoria = iteracoes = paralelismo = 0;
        sal = esperado = [];

        if (hash is null || !hash.StartsWith(_prefixo, StringComparison.Ordinal))
        {
            return false;
        }

        var partes = hash[_prefixo.Length..].Split('$');
        if (partes.Length != 3)
        {
            return false;
        }

        var parametros = partes[0].Split(',');
        if (parametros.Length != 3
            || !TryParametro(parametros[0], "m=", out memoria)
            || !TryParametro(parametros[1], "t=", out iteracoes)
            || !TryParametro(parametros[2], "p=", out paralelismo))
        {
            return false;
        }

        return TryBase64(partes[1], out sal) && TryBase64(partes[2], out esperado) && sal.Length > 0 && esperado.Length > 0;
    }

    private static bool TryParametro(string texto, string nome, out int valor)
    {
        valor = 0;
        return texto.StartsWith(nome, StringComparison.Ordinal)
            && int.TryParse(texto.AsSpan(nome.Length), NumberStyles.None, CultureInfo.InvariantCulture, out valor)
            && valor > 0;
    }

    private static string Base64SemPreenchimento(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=');

    private static bool TryBase64(string texto, out byte[] bytes)
    {
        var comPreenchimento = texto.PadRight(texto.Length + ((4 - (texto.Length % 4)) % 4), '=');
        bytes = new byte[comPreenchimento.Length];
        if (Convert.TryFromBase64String(comPreenchimento, bytes, out var escritos))
        {
            bytes = bytes[..escritos];
            return true;
        }

        bytes = [];
        return false;
    }
}
