using Maxx.Domain.Social;

namespace Maxx.Domain.Tests.Social;

public class FeedTests
{
    private static List<string> Seguidos(int quantos) => [.. Enumerable.Range(1, quantos).Select(i => $"s{i}")];

    private static List<string> Recomendados(int quantos) => [.. Enumerable.Range(1, quantos).Select(i => $"r{i}")];

    [Fact]
    public void Tres_seguidos_para_cada_recomendacao_em_paginas_de_12()
    {
        var feed = Feed.Intercalar(Seguidos(20), Recomendados(20));

        Assert.Equal(["s1", "s2", "s3", "r1", "s4", "s5", "s6", "r2", "s7", "s8", "s9", "r3"], feed.Pagina);
        Assert.Equal((9, 3), (feed.UsadosSeguidos, feed.UsadosRecomendados));
    }

    [Fact]
    public void Quem_nao_segue_ninguem_ve_so_recomendacoes()
    {
        var feed = Feed.Intercalar([], Recomendados(20));

        Assert.Equal(Recomendados(12), feed.Pagina);
        Assert.Equal((0, 12), (feed.UsadosSeguidos, feed.UsadosRecomendados));
    }

    [Fact]
    public void Sem_recomendacao_a_pagina_e_toda_de_seguidos()
    {
        var feed = Feed.Intercalar(Seguidos(20), []);

        Assert.Equal(Seguidos(12), feed.Pagina);
    }

    [Fact]
    public void Seguidos_que_acabam_cedem_a_vez_as_recomendacoes()
    {
        var feed = Feed.Intercalar(Seguidos(4), Recomendados(20));

        Assert.Equal(["s1", "s2", "s3", "r1", "s4", "r2", "r3", "r4", "r5", "r6", "r7", "r8"], feed.Pagina);
        Assert.Equal((4, 8), (feed.UsadosSeguidos, feed.UsadosRecomendados));
    }

    [Fact]
    public void Recomendacoes_que_acabam_cedem_a_vez_aos_seguidos()
    {
        var feed = Feed.Intercalar(Seguidos(20), Recomendados(1));

        Assert.Equal(["s1", "s2", "s3", "r1", "s4", "s5", "s6", "s7", "s8", "s9", "s10", "s11"], feed.Pagina);
    }

    [Fact]
    public void Pouco_conteudo_da_pagina_curta_e_nada_da_pagina_vazia()
    {
        Assert.Equal(["s1", "s2", "r1"], Feed.Intercalar(Seguidos(2), Recomendados(1)).Pagina);
        Assert.Empty(Feed.Intercalar(new List<string>(), new List<string>()).Pagina);
    }
}
