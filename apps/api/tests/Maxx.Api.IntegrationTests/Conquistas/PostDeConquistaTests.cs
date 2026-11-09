using System.Net;
using System.Net.Http.Json;
using Maxx.Api.Posts;
using Maxx.Api.Premios;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Social;

namespace Maxx.Api.IntegrationTests.Conquistas;

// O post de conquista (passo 9): um prêmio rende um post (produto.md 6.1), e a conquista é
// um prêmio. Mesmas regras da medalha: só do autor, um post por conquista, nunca junto de
// outro prêmio; e, se o recálculo remove a conquista, o post perde o vínculo como o da
// medalha anulada (produto.md 4.8).
public sealed class PostDeConquistaTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string _ana = "ana.souza@al.unieduk.com.br";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(MundoDeConquistas Mundo, List<Guid> Medalhas, Guid C01)> AnaComC01Async(PostgresFixture postgres)
    {
        var mundo = await MundoDeConquistas.CriarAsync(postgres);
        var medalhas = await mundo.CincoNoBimestreAsync(mundo.Ana);
        await mundo.EsvaziarAsync();
        var c01 = Assert.Single(await mundo.LinhasAsync<Conquista>()).Id;
        return (mundo, medalhas, c01);
    }

    private static Task<HttpResponseMessage> PublicarAsync(HttpClient client, object pedido) =>
        client.PostAsJsonAsync(new Uri("/api/v1/posts", UriKind.Relative), pedido, Ct);

    [Fact]
    public async Task Aluno_posta_a_propria_conquista_e_trofeus_passa_a_citar_o_post()
    {
        var (mundo, _, c01) = await AnaComC01Async(postgres);
        await using var descartar = mundo;
        using var ana = await mundo.ComoAsync(_ana);

        var resposta = await PublicarAsync(ana, new { legenda = "Cinco no bimestre!", conquistaId = c01 });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var post = (await resposta.Content.ReadFromJsonAsync<PostResponse>(Ct))!;
        var premio = Assert.IsType<PremioDoPostResponse>(post.Premio);
        Assert.Equal(("conquista", c01, "C01", "bronze", 25), (premio.Tipo, premio.Id, premio.Item, premio.Tier, premio.Pontos));
        Assert.Null(premio.Materia);
        Assert.Null(premio.Bimestre);

        var lido = (await ana.GetFromJsonAsync<PostResponse>($"/api/v1/posts/{post.Id}", Ct))!;
        Assert.Equal(c01, lido.Premio?.Id);
        var premios = (await ana.GetFromJsonAsync<PremiosDoAlunoResponse>("/api/v1/eu/premios", Ct))!;
        Assert.Equal(post.Id, Assert.Single(premios.Itens.Single(item => item.Codigo == "C01").Obtidos).PostId);
    }

    [Fact]
    public async Task Uma_conquista_rende_um_post()
    {
        var (mundo, _, c01) = await AnaComC01Async(postgres);
        await using var descartar = mundo;
        using var ana = await mundo.ComoAsync(_ana);
        Assert.Equal(HttpStatusCode.Created, (await PublicarAsync(ana, new { conquistaId = c01 })).StatusCode);

        await AssertRecusaAsync(await PublicarAsync(ana, new { conquistaId = c01 }), HttpStatusCode.Conflict, "premio-ja-postado");
    }

    [Fact]
    public async Task Conquista_de_outra_pessoa_nao_existe_para_quem_posta()
    {
        var (mundo, _, c01) = await AnaComC01Async(postgres);
        await using var descartar = mundo;
        using var bruno = await mundo.ComoAsync("bruno.lima@al.unieduk.com.br");

        await AssertRecusaAsync(await PublicarAsync(bruno, new { conquistaId = c01 }), HttpStatusCode.NotFound, "premio-nao-encontrado");
        await AssertRecusaAsync(
            await PublicarAsync(bruno, new { conquistaId = Guid.CreateVersion7() }), HttpStatusCode.NotFound, "premio-nao-encontrado");
    }

    [Fact]
    public async Task Medalha_e_conquista_no_mesmo_post_sao_recusadas()
    {
        var (mundo, medalhas, c01) = await AnaComC01Async(postgres);
        await using var descartar = mundo;
        using var ana = await mundo.ComoAsync(_ana);

        await AssertRecusaAsync(
            await PublicarAsync(ana, new { medalhaId = medalhas[0], conquistaId = c01 }), HttpStatusCode.BadRequest, "mais-de-um-premio");
    }

    [Fact]
    public async Task Conquista_removida_desvincula_o_post_com_legenda_e_nao_pode_mais_ser_postada()
    {
        var (mundo, medalhas, c01) = await AnaComC01Async(postgres);
        await using var descartar = mundo;
        using var ana = await mundo.ComoAsync(_ana);
        var comLegenda = (await (await PublicarAsync(ana, new { legenda = "Cinco!", conquistaId = c01 }))
            .Content.ReadFromJsonAsync<PostResponse>(Ct))!;

        await mundo.AnularAsync(medalhas[0]);
        await mundo.EsvaziarAsync();

        var post = Assert.Single(await mundo.LinhasAsync<Post>(), linha => linha.Id == comLegenda.Id);
        Assert.Null(post.ConquistaId);
        Assert.Equal("Cinco!", post.Legenda);
        await AssertRecusaAsync(await PublicarAsync(ana, new { conquistaId = c01 }), HttpStatusCode.Conflict, "conquista-removida");
    }

    [Fact]
    public async Task Conquista_removida_apaga_o_post_que_era_so_ela()
    {
        var (mundo, medalhas, c01) = await AnaComC01Async(postgres);
        await using var descartar = mundo;
        using var ana = await mundo.ComoAsync(_ana);
        var soAConquista = (await (await PublicarAsync(ana, new { conquistaId = c01 }))
            .Content.ReadFromJsonAsync<PostResponse>(Ct))!;

        await mundo.AnularAsync(medalhas[0]);
        await mundo.EsvaziarAsync();

        Assert.DoesNotContain(await mundo.LinhasAsync<Post>(), linha => linha.Id == soAConquista.Id);
    }

    private static async Task AssertRecusaAsync(HttpResponseMessage resposta, HttpStatusCode status, string codigo)
    {
        Assert.Equal(status, resposta.StatusCode);
        var corpo = await resposta.Content.ReadFromJsonAsync<Dictionary<string, object>>(Ct);
        Assert.Equal(codigo, corpo!["codigo"].ToString());
    }
}
