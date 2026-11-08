using System.Net;
using System.Net.Http.Json;
using Maxx.Api.Feed;
using Maxx.Api.IntegrationTests.Fotos;
using Maxx.Api.Posts;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.Social.MundoSocial;

namespace Maxx.Api.IntegrationTests.Social;

// O feed (produto.md 6.2; decisão D8, opção A), contra o Postgres real: seguidos e
// recomendações intercalados, cursor sem offset e semente de sessão.
public sealed class FeedTests(PostgresFixture postgres, ArmazenamentoFixture armazenamento)
    : IClassFixture<PostgresFixture>, IClassFixture<ArmazenamentoFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sem_post_nenhum_o_feed_vem_vazio_e_sem_proxima_pagina()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);

        var pagina = await PaginaAsync(ana);

        Assert.Empty(pagina.Itens);
        Assert.Null(pagina.Proximo);
    }

    [Fact]
    public async Task Quem_nao_segue_ninguem_ve_os_proprios_posts_e_recomendacoes_dos_outros()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        var deOutros = await PostarAsync(mundo, Bruno, 2);
        deOutros.AddRange(await PostarAsync(mundo, Diego, 2));
        var daAna = await PostarAsync(mundo, Ana, 1);

        var pagina = await PaginaAsync(ana);

        Assert.Equal([.. deOutros, .. daAna], pagina.Itens.Select(post => post.Id).Order());
        Assert.Null(pagina.Proximo);
    }

    [Fact]
    public async Task Paginas_intercalam_tres_seguidos_e_uma_recomendacao_sem_repetir_nem_pular()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        await SeguirAsync(ana, mundo.Id(Bruno));
        var seguidos = await PostarAsync(mundo, Bruno, 15);
        seguidos.AddRange(await PostarAsync(mundo, Ana, 5));
        var recomendados = await PostarAsync(mundo, Diego, 15);

        var paginas = await TodasAsync(ana);

        Assert.Equal([12, 12, 11], paginas.Select(pagina => pagina.Count));
        var todos = paginas.SelectMany(pagina => pagina).ToList();
        Assert.Equal(35, todos.Distinct().Count());

        // Na primeira página, as posições 4, 8 e 12 são recomendações; o resto, de quem a Ana
        // segue e dela, do mais novo ao mais antigo.
        var primeira = paginas[0];
        Assert.All([3, 7, 11], posicao => Assert.Contains(primeira[posicao], recomendados));
        var trechoDeSeguidos = primeira.Where((_, posicao) => posicao % 4 != 3).ToList();
        Assert.All(trechoDeSeguidos, id => Assert.Contains(id, seguidos));
        Assert.Equal(Enumerable.Reverse(seguidos).Take(9), trechoDeSeguidos);

        // Os seguidos acabam na terceira página, e as recomendações que sobraram fecham o feed.
        Assert.Equal(recomendados.Order(), todos.Where(recomendados.Contains).Order());
    }

    [Fact]
    public async Task A_mesma_sessao_mantem_a_ordem_e_outra_sessao_embaralha_de_novo()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        await PostarAsync(mundo, Bruno, 15);
        await PostarAsync(mundo, Diego, 15);

        var primeira = await PaginaAsync(ana);
        var segunda = await PaginaAsync(ana, primeira.Proximo);
        var deNovo = await PaginaAsync(ana, primeira.Proximo);
        Assert.Equal(Ids(segunda), Ids(deNovo));

        // Sessões novas (sem cursor) sorteiam outra semente: entre cinco, a ordem dos 30 posts
        // não se repete em todas.
        var ordens = new List<string>();
        for (var sessao = 0; sessao < 5; sessao++)
        {
            ordens.Add(string.Join(',', (await TodasAsync(ana)).SelectMany(pagina => pagina)));
        }

        Assert.True(ordens.Distinct(StringComparer.Ordinal).Count() > 1);
    }

    [Fact]
    public async Task Post_novo_no_meio_da_sessao_nao_desloca_as_paginas_seguintes()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        await SeguirAsync(ana, mundo.Id(Bruno));
        var antigos = await PostarAsync(mundo, Bruno, 20);

        var primeira = await PaginaAsync(ana);
        var novo = await PostarAsync(mundo, Bruno, 1);
        var resto = await TodasAsync(ana, primeira.Proximo);

        var vistos = Ids(primeira).Concat(resto.SelectMany(pagina => pagina)).ToList();
        Assert.Equal(antigos.Order(), vistos.Order());
        Assert.DoesNotContain(novo[0], vistos);

        // Puxar para atualizar começa uma sessão nova, e o post novo vem primeiro.
        Assert.Equal(novo[0], (await PaginaAsync(ana)).Itens[0].Id);
    }

    [Fact]
    public async Task Post_removido_pela_instituicao_some_do_feed()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        using var coordenacao = await mundo.ComoAsync(Coordenacao);
        var posts = await PostarAsync(mundo, Bruno, 2);
        Assert.Equal(HttpStatusCode.NoContent, (await coordenacao.DeleteAsync(new Uri($"/api/v1/posts/{posts[0]}", UriKind.Relative), Ct)).StatusCode);

        Assert.Equal([posts[1]], Ids(await PaginaAsync(ana)));
    }

    [Fact]
    public async Task Professora_ve_os_alunos_que_segue_e_a_instituicao_ve_so_recomendacoes()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var renata = await mundo.ComoAsync(Renata);
        using var coordenacao = await mundo.ComoAsync(Coordenacao);
        await SeguirAsync(renata, mundo.Id(Ana));
        var daAna = await PostarAsync(mundo, Ana, 6);
        var doBruno = await PostarAsync(mundo, Bruno, 6);

        var daProfessora = Ids(await PaginaAsync(renata));
        Assert.Equal(Enumerable.Reverse(daAna).Take(3), daProfessora.Take(3));
        Assert.Contains(daProfessora[3], doBruno);

        Assert.Equal([.. daAna, .. doBruno], Ids(await PaginaAsync(coordenacao)).Order());
    }

    [Fact]
    public async Task Item_do_feed_e_o_post_completo_com_o_curtiu_de_quem_le()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        using var bruno = await mundo.ComoAsync(Bruno);
        var medalha = await mundo.MedalhaAsync(Bruno);
        var post = await bruno.PostAsJsonAsync(new Uri("/api/v1/posts", UriKind.Relative), new { legenda = "Nota 10!", medalhaId = medalha }, Ct);
        var publicado = (await post.Content.ReadFromJsonAsync<PostResponse>(Ct))!;
        Assert.Equal(HttpStatusCode.OK, (await ana.PutAsync(new Uri($"/api/v1/posts/{publicado.Id}/curtida", UriKind.Relative), null, Ct)).StatusCode);

        var item = Assert.Single((await PaginaAsync(ana)).Itens);

        Assert.Equal(publicado with { Curtidas = 1, Curtiu = true }, item);
        Assert.False(Assert.Single((await PaginaAsync(bruno)).Itens).Curtiu);
    }

    [Theory]
    [InlineData("nao-e-cursor")]
    [InlineData("ZjF8fC18LQ")]
    public async Task Cursor_que_a_api_nao_emitiu_e_400(string cursor)
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);

        await AssertRecusaAsync(
            await ana.GetAsync(new Uri($"/api/v1/feed?cursor={cursor}", UriKind.Relative), Ct), HttpStatusCode.BadRequest, "cursor-invalido");
    }

    [Fact]
    public async Task O_trecho_de_seguidos_anda_pelo_indice_do_autor()
    {
        // Com poucos posts o planejador prefere varrer a tabela; desligada a varredura, o
        // plano mostra que a consulta cabe no índice (autor_id, criado_em desc, id desc).
        await using var mundo = await CriarAsync(postgres, armazenamento);
        await using var scope = mundo.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        await using var transacao = await db.Database.BeginTransactionAsync(Ct);
        await db.Database.ExecuteSqlRawAsync("SET LOCAL enable_seqscan = off", Ct);

        var plano = await db.Database.SqlQueryRaw<string>(
                """
                EXPLAIN SELECT p.criado_em, p.id
                FROM post p
                WHERE p.removido_em IS NULL
                  AND p.autor_id IN (SELECT '00000000-0000-0000-0000-000000000001'::uuid UNION ALL
                                     SELECT s.seguido_id FROM seguimento s WHERE s.seguidor_id = '00000000-0000-0000-0000-000000000001'::uuid)
                  AND (p.criado_em, p.id) < (now(), '00000000-0000-0000-0000-000000000001'::uuid)
                ORDER BY p.criado_em DESC, p.id DESC
                LIMIT 13
                """)
            .ToListAsync(Ct);

        Assert.Contains(plano, linha => linha.Contains("ix_post_autor_id_criado_em_id", StringComparison.Ordinal));
    }

    // Um post de legenda por vez, com o relógio andando 100 ms: a ordem por tempo é a da
    // criação. Pouco, de propósito: o token emitido pelo relógio do teste não pode passar da
    // tolerância de 30 s do relógio de verdade.
    private static async Task<List<Guid>> PostarAsync(MundoSocial mundo, string autor, int quantos)
    {
        using var client = await mundo.ComoAsync(autor);
        var ids = new List<Guid>();
        for (var i = 0; i < quantos; i++)
        {
            mundo.Relogio.Avancar(TimeSpan.FromMilliseconds(100));
            var resposta = await client.PostAsJsonAsync(new Uri("/api/v1/posts", UriKind.Relative), new { legenda = $"{autor} {i}" }, Ct);
            Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
            ids.Add((await resposta.Content.ReadFromJsonAsync<PostResponse>(Ct))!.Id);
        }

        return ids;
    }

    private static async Task SeguirAsync(HttpClient client, Guid usuario) =>
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync(new Uri($"/api/v1/seguindo/{usuario}", UriKind.Relative), null, Ct)).StatusCode);

    private static async Task<PaginaDoFeedResponse> PaginaAsync(HttpClient client, string? cursor = null) =>
        (await client.GetFromJsonAsync<PaginaDoFeedResponse>($"/api/v1/feed{(cursor is null ? string.Empty : $"?cursor={cursor}")}", Ct))!;

    private static async Task<List<List<Guid>>> TodasAsync(HttpClient client, string? cursor = null)
    {
        var paginas = new List<List<Guid>>();
        do
        {
            var pagina = await PaginaAsync(client, cursor);
            paginas.Add(Ids(pagina));
            cursor = pagina.Proximo;
        }
        while (cursor is not null);

        return paginas;
    }

    private static List<Guid> Ids(PaginaDoFeedResponse pagina) => [.. pagina.Itens.Select(post => post.Id)];
}
