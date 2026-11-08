using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Maxx.Domain.Fotos;

// ULID (ADR 0002): 48 bits de instante em milissegundos e 80 bits aleatórios de gerador
// criptográfico, em 26 caracteres de base 32 de Crockford. Os 80 bits são o que torna a URL
// da foto não descobrível; o instante só ordena as chaves no bucket.
public static class Ulid
{
    public const int Tamanho = 26;

    private const string _alfabeto = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string Novo(DateTimeOffset agora)
    {
        Span<byte> bytes = stackalloc byte[16];
        var milissegundos = agora.ToUnixTimeMilliseconds();
        if (milissegundos is < 0 or >= 1L << 48)
        {
            throw new DomainException($"Instante fora do alcance de um ULID: {agora:O}.");
        }

        for (var i = 5; i >= 0; i--)
        {
            bytes[i] = (byte)(milissegundos & 0xFF);
            milissegundos >>= 8;
        }

        RandomNumberGenerator.Fill(bytes[6..]);
        return Codificar(bytes);
    }

    public static bool EhValido(string? texto) =>
        texto is { Length: Tamanho } && texto[0] <= '7' && texto.All(caractere => _alfabeto.Contains(caractere, StringComparison.Ordinal));

    // 128 bits em 26 grupos de 5, do mais significativo para o menos; o primeiro grupo tem
    // só 3 bits, por isso o ULID começa por um dígito de 0 a 7.
    private static string Codificar(ReadOnlySpan<byte> bytes) =>
        string.Create(Tamanho, BinaryPrimitives.ReadUInt128BigEndian(bytes), static (destino, valor) =>
        {
            for (var i = Tamanho - 1; i >= 0; i--)
            {
                destino[i] = _alfabeto[(int)(valor & 0x1F)];
                valor >>= 5;
            }
        });
}
