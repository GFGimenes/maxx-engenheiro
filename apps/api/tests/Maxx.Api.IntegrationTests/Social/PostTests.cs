using System.Net;
using System.Net.Http.Json;
using Maxx.Api.IntegrationTests.Fotos;
using Maxx.Api.Posts;
using Maxx.Application.Fotos;
using Maxx.Domain.Fotos;
using Maxx.Domain.Social;
using Maxx.Infrastructure.Fila;
using static Maxx.Api.IntegrationTests.Social.MundoSocial;

namespace Maxx.Api.IntegrationTests.Social;

// Publicar, ler e remover post (produto.md 6.1), contra o Postgres e o bucket reais.
public sealed class PostTests(PostgresFixture postgres, ArmazenamentoFixture armazenamento)
    : IClassFixture<PostgresFixture>, IClassFixture<ArmazenamentoFixture>
{
    private static readonly Uri _posts = new("/api/v1/posts", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_com_premio_foto_e_legenda_volta_pronto_para_o_feed()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        var medalha = await mundo.MedalhaAsync(Ana);
        var upload = await FotoEnviadaAsync(ana);

        var resposta = await PublicarAsync(ana, "Nota 10 em Banco de Dados!", upload.Id, medalha);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var post = (await resposta.Content.ReadFromJsonAsync<PostResponse>(Ct))!;
        Assert.Equal($"/api/v1/posts/{post.Id}", resposta.Headers.Location?.OriginalString);
        Assert.Equal("Nota 10 em Banco de Dados!", post.Legenda);
        Assert.Equal(mundo.Relogio.GetUtcNow(), post.CriadoEm);

        // A linha do autor, como no feed: nome, arroba, curso e semestre. Nada de RA.
        Assert.Equal(new AutorDoPostResponse(mundo.Id(Ana), "Ana Souza", "@anaSouza-CC", "CC", 3, null), post.Autor);

        // O prêmio com o tier e os pontos do momento, a matéria e o bimestre.
        var premio = post.Premio!;
        Assert.Equal(("medalha", medalha, "M01", "Nota 10 em prova"), (premio.Tipo, premio.Id, premio.Item, premio.Nome));
        Assert.Equal(("bronze", 30, "Banco de Dados", 2), (premio.Tier, premio.Pontos, premio.Materia, premio.Bimestre));

        // A foto, lida direto do bucket, sem login.
        using var http = new HttpClient();
        Assert.StartsWith(armazenamento.UrlDeLeitura.ToString(), post.Foto!.Feed, StringComparison.Ordinal);
        Assert.Equal(TamanhoDoFeed, (await http.GetByteArrayAsync(new Uri(post.Foto.Feed), Ct)).Length);
        Assert.Equal(TamanhoDaGrade, (await http.GetByteArrayAsync(new Uri(post.Foto.Grade), Ct)).Length);

        // O upload foi confirmado: a limpeza não o apaga mais.
        Assert.NotNull(Assert.Single(await mundo.LinhasAsync<UploadPendente>()).ConfirmadoEm);
        mundo.Relogio.Avancar(TimeSpan.FromHours(24));
        await mundo.EsvaziarFilaAsync();
        Assert.True(await mundo.Bucket.ExisteAsync(Assert.Single(await mundo.LinhasAsync<Post>()).ChaveDaFotoDoFeed!, Ct));
    }

    [Fact]
    public async Task Qualquer_usuario_logado_le_o_post_e_o_que_nao_existe_e_404()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        var post = await PublicadoAsync(ana, "Primeiro dia no campus.");

        foreach (var quem in new[] { Bruno, Carla, Renata, Coordenacao })
        {
            using var leitor = await mundo.ComoAsync(quem);
            var lido = await leitor.GetFromJsonAsync<PostResponse>($"/api/v1/posts/{post.Id}", Ct);
            Assert.Equal(post, lido);
        }

        await AssertRecusaAsync(
            await ana.GetAsync(new Uri($"/api/v1/posts/{Guid.NewGuid()}", UriKind.Relative), Ct), HttpStatusCode.NotFound, "post-nao-encontrado");
        using var anonimo = mundo.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync(new Uri($"/api/v1/posts/{post.Id}", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task So_o_premio_ou_so_a_legenda_basta()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);

        var soPremio = await PublicadoAsync(ana, medalha: await mundo.MedalhaAsync(Ana));
        var soLegenda = await PublicadoAsync(ana, "Oi, turma.");

        Assert.Null(soPremio.Foto);
        Assert.Null(soPremio.Legenda);
        Assert.NotNull(soPremio.Premio);
        Assert.Null(soLegenda.Premio);
        Assert.Null(soLegenda.Foto);
    }

    [Fact]
    public async Task Post_vazio_e_legenda_longa_sao_400()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);

        await AssertRecusaAsync(await PublicarAsync(ana, "   "), HttpStatusCode.BadRequest, "post-vazio");
        await AssertRecusaAsync(await PublicarAsync(ana, new string('a', 1001)), HttpStatusCode.BadRequest, "legenda-longa");
        await AssertRecusaAsync(await PublicarAsync(ana, new string('a', 4001)), HttpStatusCode.BadRequest, "dados-invalidos");
        Assert.Empty(await mundo.LinhasAsync<Post>());
    }

    [Theory]
    [InlineData(Carla)]
    [InlineData(Renata)]
    [InlineData(Coordenacao)]
    public async Task So_aluno_ativo_publica(string quem)
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var client = await mundo.ComoAsync(quem);

        await AssertRecusaAsync(await PublicarAsync(client, "Oi"), HttpStatusCode.Forbidden, "post-nao-permitido");
    }

    [Fact]
    public async Task Foto_de_outra_pessoa_desconhecida_ou_ja_usada_nao_serve()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        using var bruno = await mundo.ComoAsync(Bruno);
        var doBruno = await FotoEnviadaAsync(bruno);
        var daAna = await FotoEnviadaAsync(ana);

        await AssertRecusaAsync(await PublicarAsync(ana, upload: doBruno.Id), HttpStatusCode.NotFound, "upload-nao-encontrado");
        await AssertRecusaAsync(await PublicarAsync(ana, upload: Guid.NewGuid()), HttpStatusCode.NotFound, "upload-nao-encontrado");

        Assert.Equal(HttpStatusCode.Created, (await PublicarAsync(ana, upload: daAna.Id)).StatusCode);
        await AssertRecusaAsync(await PublicarAsync(ana, "De novo", upload: daAna.Id), HttpStatusCode.Conflict, "upload-ja-usado");
    }

    [Fact]
    public async Task Foto_que_nao_chegou_ao_bucket_e_409_e_o_upload_continua_pendente()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        var upload = await FotoEnviadaAsync(ana, enviar: false);

        await AssertRecusaAsync(await PublicarAsync(ana, "Olha", upload: upload.Id), HttpStatusCode.Conflict, "foto-nao-enviada");

        Assert.Null(Assert.Single(await mundo.LinhasAsync<UploadPendente>()).ConfirmadoEm);
        Assert.Empty(await mundo.LinhasAsync<Post>());
    }

    [Fact]
    public async Task Premio_de_outra_pessoa_anulado_ou_ja_postado_nao_serve()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        var doBruno = await mundo.MedalhaAsync(Bruno);
        var anulada = await mundo.MedalhaAsync(Ana, "M03");
        await mundo.AnularAsync(anulada);
        var postada = await mundo.MedalhaAsync(Ana);
        await PublicadoAsync(ana, medalha: postada);

        await AssertRecusaAsync(await PublicarAsync(ana, medalha: doBruno), HttpStatusCode.NotFound, "premio-nao-encontrado");
        await AssertRecusaAsync(await PublicarAsync(ana, medalha: Guid.NewGuid()), HttpStatusCode.NotFound, "premio-nao-encontrado");
        await AssertRecusaAsync(await PublicarAsync(ana, medalha: anulada), HttpStatusCode.Conflict, "premio-anulado");
        await AssertRecusaAsync(await PublicarAsync(ana, "De novo", medalha: postada), HttpStatusCode.Conflict, "premio-ja-postado");
    }

    [Fact]
    public async Task Dois_posts_ao_mesmo_tempo_com_o_mesmo_premio_rendem_um_so()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        var medalha = await mundo.MedalhaAsync(Ana);

        var respostas = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => PublicarAsync(ana, $"Tentativa {i}", medalha: medalha)));

        Assert.Single(respostas, resposta => resposta.StatusCode == HttpStatusCode.Created);
        foreach (var recusada in respostas.Where(resposta => resposta.StatusCode != HttpStatusCode.Created))
        {
            await AssertRecusaAsync(recusada, HttpStatusCode.Conflict, "premio-ja-postado");
        }

        Assert.Single(await mundo.LinhasAsync<Post>());
    }

    [Fact]
    public async Task Dois_posts_ao_mesmo_tempo_com_a_mesma_foto_rendem_um_so()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        var upload = await FotoEnviadaAsync(ana);

        var respostas = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => PublicarAsync(ana, $"Tentativa {i}", upload: upload.Id)));

        Assert.Single(respostas, resposta => resposta.StatusCode == HttpStatusCode.Created);
        foreach (var recusada in respostas.Where(resposta => resposta.StatusCode != HttpStatusCode.Created))
        {
            await AssertRecusaAsync(recusada, HttpStatusCode.Conflict, "upload-ja-usado");
        }

        Assert.Single(await mundo.LinhasAsync<Post>());
    }

    [Fact]
    public async Task Autor_apaga_o_post_a_foto_sai_do_bucket_e_o_premio_pode_render_outro_post()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        var medalha = await mundo.MedalhaAsync(Ana);
        var post = await PublicadoAsync(ana, "Nota 10!", (await FotoEnviadaAsync(ana)).Id, medalha);
        var chave = Assert.Single(await mundo.LinhasAsync<Post>()).ChaveDaFotoDoFeed!;

        Assert.Equal(HttpStatusCode.NoContent, (await RemoverAsync(ana, post.Id)).StatusCode);

        Assert.Empty(await mundo.LinhasAsync<Post>());
        await AssertRecusaAsync(await ana.GetAsync(new Uri($"/api/v1/posts/{post.Id}", UriKind.Relative), Ct), HttpStatusCode.NotFound, "post-nao-encontrado");
        Assert.Contains(await mundo.LinhasAsync<Trabalho>(), trabalho => trabalho.Tipo == RemoverFotos.Tipo);
        await mundo.EsvaziarFilaAsync();
        Assert.False(await mundo.Bucket.ExisteAsync(chave, Ct));

        Assert.Equal(HttpStatusCode.Created, (await PublicarAsync(ana, medalha: medalha)).StatusCode);
    }

    [Fact]
    public async Task Instituicao_remove_o_post_gravando_quem_e_quando_e_ele_some_para_todos()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        using var coordenacao = await mundo.ComoAsync(Coordenacao);
        var medalha = await mundo.MedalhaAsync(Ana);
        var post = await PublicadoAsync(ana, "Impróprio", (await FotoEnviadaAsync(ana)).Id, medalha);
        mundo.Relogio.Avancar(TimeSpan.FromMinutes(5));

        Assert.Equal(HttpStatusCode.NoContent, (await RemoverAsync(coordenacao, post.Id)).StatusCode);

        var linha = Assert.Single(await mundo.LinhasAsync<Post>());
        Assert.Equal((mundo.Relogio.GetUtcNow(), mundo.Id(Coordenacao)), (linha.RemovidoEm, linha.RemovidoPorId));
        Assert.Equal("Impróprio", linha.Legenda);
        await AssertRecusaAsync(await ana.GetAsync(new Uri($"/api/v1/posts/{post.Id}", UriKind.Relative), Ct), HttpStatusCode.NotFound, "post-nao-encontrado");
        await AssertRecusaAsync(await RemoverAsync(ana, post.Id), HttpStatusCode.NotFound, "post-nao-encontrado");

        await mundo.EsvaziarFilaAsync();
        Assert.False(await mundo.Bucket.ExisteAsync(linha.ChaveDaFotoDoFeed!, Ct));

        // O prêmio continua do aluno, e pode render um post novo.
        Assert.Equal(HttpStatusCode.Created, (await PublicarAsync(ana, medalha: medalha)).StatusCode);
    }

    [Theory]
    [InlineData(Bruno)]
    [InlineData(Renata)]
    public async Task Quem_nao_e_autor_nem_instituicao_nao_remove(string quem)
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        using var outro = await mundo.ComoAsync(quem);
        var post = await PublicadoAsync(ana, "Oi");

        await AssertRecusaAsync(await RemoverAsync(outro, post.Id), HttpStatusCode.Forbidden, "remocao-nao-permitida");
        Assert.Null(Assert.Single(await mundo.LinhasAsync<Post>()).RemovidoEm);
    }

    [Fact]
    public async Task Medalha_anulada_desvincula_o_post_com_legenda_e_apaga_o_que_era_so_a_medalha()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        var primeira = await mundo.MedalhaAsync(Ana);
        var segunda = await mundo.MedalhaAsync(Ana, "M03");
        var comLegenda = await PublicadoAsync(ana, "Nota 10!", medalha: primeira);
        var soMedalha = await PublicadoAsync(ana, medalha: segunda);

        await mundo.AnularAsync(primeira);
        await mundo.AnularAsync(segunda);

        var lido = await ana.GetFromJsonAsync<PostResponse>($"/api/v1/posts/{comLegenda.Id}", Ct);
        Assert.Null(lido!.Premio);
        Assert.Equal("Nota 10!", lido.Legenda);
        await AssertRecusaAsync(await ana.GetAsync(new Uri($"/api/v1/posts/{soMedalha.Id}", UriKind.Relative), Ct), HttpStatusCode.NotFound, "post-nao-encontrado");
        Assert.Equal(comLegenda.Id, Assert.Single(await mundo.LinhasAsync<Post>()).Id);
    }

    private static Task<HttpResponseMessage> PublicarAsync(HttpClient client, string? legenda = null, Guid? upload = null, Guid? medalha = null) =>
        client.PostAsJsonAsync(_posts, new { legenda, uploadId = upload, medalhaId = medalha }, Ct);

    private static async Task<PostResponse> PublicadoAsync(HttpClient client, string? legenda = null, Guid? upload = null, Guid? medalha = null)
    {
        var resposta = await PublicarAsync(client, legenda, upload, medalha);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<PostResponse>(Ct))!;
    }

    private static Task<HttpResponseMessage> RemoverAsync(HttpClient client, Guid post) =>
        client.DeleteAsync(new Uri($"/api/v1/posts/{post}", UriKind.Relative), Ct);
}
