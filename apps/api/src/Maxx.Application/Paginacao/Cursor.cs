using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Maxx.Application.Paginacao;

// Paginação por cursor, sem offset (decisoes/README.md, Feed): a página seguinte começa
// depois do último item lido, pelo índice, e o que entrou no meio não desloca nada.
public sealed record Pagina<T>(IReadOnlyList<T> Itens, string? Proximo);

// A posição numa lista ordenada por instante e id. O id desempata itens do mesmo instante.
public sealed record PosicaoNoTempo(DateTimeOffset Instante, Guid Id);

// O cursor é opaco para o app: base64url de "<ticks UTC>.<id>". Não é segredo nem
// assinatura; só não é para o app montar.
public static class CursorDeTempo
{
    public static string Codificar(PosicaoNoTempo posicao)
    {
        ArgumentNullException.ThrowIfNull(posicao);
        var texto = string.Create(CultureInfo.InvariantCulture, $"{posicao.Instante.UtcTicks}.{posicao.Id:N}");
        return Base64Url.EncodeToString(Encoding.ASCII.GetBytes(texto));
    }

    // Nulo para a primeira página.
    public static PosicaoNoTempo? Ler(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor))
        {
            return null;
        }

        try
        {
            var partes = Encoding.ASCII.GetString(Base64Url.DecodeFromChars(cursor)).Split('.');
            if (partes is [var ticks, var id]
                && long.TryParse(ticks, NumberStyles.None, CultureInfo.InvariantCulture, out var valor)
                && valor is >= 0 and <= 3_155_378_975_999_999_999
                && Guid.TryParseExact(id, "N", out var guid))
            {
                return new PosicaoNoTempo(new DateTimeOffset(valor, TimeSpan.Zero), guid);
            }
        }
        catch (FormatException)
        {
        }

        throw new RecusaException(TipoDeRecusa.Invalido, CodigosDaPaginacao.CursorInvalido, "Cursor malformado.");
    }
}
