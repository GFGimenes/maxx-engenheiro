namespace Maxx.Domain.Social;

// O feed (produto.md 6.2): sempre os posts de quem o aluno segue, mais recomendações de
// outros alunos, e nunca vazio enquanto houver post. Três seguidos para cada recomendação,
// em páginas de 12 (decisão do passo 7). Um trecho que acaba cede a vez ao outro: quem
// não segue ninguém vê só recomendações, e quem segue todo mundo, só seguidos.
public static class Feed
{
    public const int TamanhoDaPagina = 12;

    public const int SeguidosPorRecomendacao = 3;

    // A página montada e quantos de cada trecho ela usou, na ordem em que vieram. A página
    // tem múltiplo de quatro posições, então toda página começa no mesmo ponto do ciclo.
    public static IntercalacaoDoFeed<T> Intercalar<T>(IReadOnlyList<T> seguidos, IReadOnlyList<T> recomendados, int tamanho = TamanhoDaPagina)
    {
        ArgumentNullException.ThrowIfNull(seguidos);
        ArgumentNullException.ThrowIfNull(recomendados);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tamanho);

        var pagina = new List<T>(tamanho);
        var (usadosSeguidos, usadosRecomendados) = (0, 0);
        for (var posicao = 0; pagina.Count < tamanho; posicao++)
        {
            var vezDaRecomendacao = posicao % (SeguidosPorRecomendacao + 1) == SeguidosPorRecomendacao;
            var temSeguido = usadosSeguidos < seguidos.Count;
            var temRecomendado = usadosRecomendados < recomendados.Count;
            if (!temSeguido && !temRecomendado)
            {
                break;
            }

            if ((vezDaRecomendacao && temRecomendado) || !temSeguido)
            {
                pagina.Add(recomendados[usadosRecomendados++]);
            }
            else
            {
                pagina.Add(seguidos[usadosSeguidos++]);
            }
        }

        return new IntercalacaoDoFeed<T>(pagina, usadosSeguidos, usadosRecomendados);
    }
}

public sealed record IntercalacaoDoFeed<T>(IReadOnlyList<T> Pagina, int UsadosSeguidos, int UsadosRecomendados);
