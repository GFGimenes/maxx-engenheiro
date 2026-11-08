using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Maxx.Application.Paginacao;
using Maxx.Application.Posts;

namespace Maxx.Application.Feed;

// A posição na ordem aleatória das recomendações: a chave é o md5 da semente com o id do
// post, em hexadecimal, e o id desempata.
public sealed record PosicaoAleatoria(string Chave, Guid Id);

// Os dois trechos do feed, só ids e chaves de ordem; o post inteiro vem depois, em lote.
public interface IConsultaDoFeed
{
    // Posts de quem o leitor segue e os dele, do mais novo ao mais antigo, pelo índice
    // (autor_id, criado_em desc, id desc).
    Task<IReadOnlyList<PosicaoNoTempo>> SeguidosAsync(
        Guid leitorId, PosicaoNoTempo? depoisDe, int limite, CancellationToken cancellationToken);

    // Posts dos outros alunos, em ordem aleatória fixa pela semente (decisão D8, opção A).
    // A ordenação não usa índice: varre os candidatos, e o p95 se mede no passo 13.
    Task<IReadOnlyList<PosicaoAleatoria>> RecomendadosAsync(
        Guid leitorId, string semente, PosicaoAleatoria? depoisDe, int limite, CancellationToken cancellationToken);
}

// O cursor do feed carrega a semente da sessão e onde cada trecho parou. A primeira página
// (sem cursor) sorteia a semente: puxar para atualizar é sessão nova, com outra ordem de
// recomendações; rolar mantém a mesma.
public sealed partial record CursorDoFeed(
    string Semente, PosicaoNoTempo? Seguidos, bool SeguidosAcabaram, PosicaoAleatoria? Recomendados, bool RecomendadosAcabaram)
{
    public static CursorDoFeed Novo() =>
        new(Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8)), null, false, null, false);

    public string Codificar()
    {
        var seguidos = SeguidosAcabaram ? "x" : Seguidos is { } tempo
            ? string.Create(CultureInfo.InvariantCulture, $"{tempo.Instante.UtcTicks}.{tempo.Id:N}")
            : "-";
        var recomendados = RecomendadosAcabaram ? "x" : Recomendados is { } aleatoria ? $"{aleatoria.Chave}.{aleatoria.Id:N}" : "-";
        return Base64Url.EncodeToString(Encoding.ASCII.GetBytes($"f1|{Semente}|{seguidos}|{recomendados}"));
    }

    public static CursorDoFeed Ler(string cursor)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        try
        {
            if (Encoding.ASCII.GetString(Base64Url.DecodeFromChars(cursor)).Split('|') is ["f1", var semente, var seguidos, var recomendados]
                && Semente16().IsMatch(semente)
                && LerSeguidos(seguidos) is var (posicaoNoTempo, seguidosAcabaram, seguidosValido) && seguidosValido
                && LerRecomendados(recomendados) is var (posicaoAleatoria, recomendadosAcabaram, recomendadosValido) && recomendadosValido)
            {
                return new CursorDoFeed(semente, posicaoNoTempo, seguidosAcabaram, posicaoAleatoria, recomendadosAcabaram);
            }
        }
        catch (FormatException)
        {
        }

        throw new RecusaException(TipoDeRecusa.Invalido, CodigosDaPaginacao.CursorInvalido, "Cursor do feed malformado.");
    }

    private static (PosicaoNoTempo?, bool, bool) LerSeguidos(string texto) => texto switch
    {
        "-" => (null, false, true),
        "x" => (null, true, true),
        _ when texto.Split('.') is [var ticks, var id]
            && long.TryParse(ticks, NumberStyles.None, CultureInfo.InvariantCulture, out var valor)
            && valor <= DateTimeOffset.MaxValue.UtcTicks
            && Guid.TryParseExact(id, "N", out var guid) => (new PosicaoNoTempo(new DateTimeOffset(valor, TimeSpan.Zero), guid), false, true),
        _ => (null, false, false),
    };

    private static (PosicaoAleatoria?, bool, bool) LerRecomendados(string texto) => texto switch
    {
        "-" => (null, false, true),
        "x" => (null, true, true),
        _ when texto.Split('.') is [var chave, var id] && Md5().IsMatch(chave) && Guid.TryParseExact(id, "N", out var guid) =>
            (new PosicaoAleatoria(chave, guid), false, true),
        _ => (null, false, false),
    };

    [GeneratedRegex("^[0-9a-f]{16}$")]
    private static partial Regex Semente16();

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex Md5();
}

// Uma página do feed (produto.md 6.2): busca um a mais de cada trecho para saber se ele
// continua, intercala pela regra do domínio e lê os posts escolhidos em lote, na ordem.
public sealed class FeedHandler(IConsultaDoFeed consulta, IConsultaDePosts posts)
{
    public async Task<Pagina<PostLido>> PaginaAsync(Guid leitorId, string? cursor, CancellationToken cancellationToken)
    {
        var atual = string.IsNullOrEmpty(cursor) ? CursorDoFeed.Novo() : CursorDoFeed.Ler(cursor);
        const int limite = Domain.Social.Feed.TamanhoDaPagina + 1;

        var seguidos = atual.SeguidosAcabaram
            ? []
            : await consulta.SeguidosAsync(leitorId, atual.Seguidos, limite, cancellationToken);
        var recomendados = atual.RecomendadosAcabaram
            ? []
            : await consulta.RecomendadosAsync(leitorId, atual.Semente, atual.Recomendados, limite, cancellationToken);

        var feed = Domain.Social.Feed.Intercalar<Guid>(
            [.. seguidos.Take(Domain.Social.Feed.TamanhoDaPagina).Select(posicao => posicao.Id)],
            [.. recomendados.Take(Domain.Social.Feed.TamanhoDaPagina).Select(posicao => posicao.Id)]);

        // Um trecho acabou se a página usou tudo o que veio e não veio o "um a mais".
        var proximo = new CursorDoFeed(
            atual.Semente,
            feed.UsadosSeguidos > 0 ? seguidos[feed.UsadosSeguidos - 1] : atual.Seguidos,
            atual.SeguidosAcabaram || feed.UsadosSeguidos >= seguidos.Count,
            feed.UsadosRecomendados > 0 ? recomendados[feed.UsadosRecomendados - 1] : atual.Recomendados,
            atual.RecomendadosAcabaram || feed.UsadosRecomendados >= recomendados.Count);

        var lidos = await posts.PostsAsync(feed.Pagina, leitorId, cancellationToken);
        return new Pagina<PostLido>(
            [.. feed.Pagina.Where(lidos.ContainsKey).Select(id => lidos[id])],
            proximo.SeguidosAcabaram && proximo.RecomendadosAcabaram ? null : proximo.Codificar());
    }
}
