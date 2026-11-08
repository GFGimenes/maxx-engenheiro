using System.Net;
using System.Net.Http.Json;
using Maxx.Api.IntegrationTests.Fotos;
using Maxx.Api.Posts;
using Maxx.Api.Social;
using Maxx.Domain.Social;
using static Maxx.Api.IntegrationTests.Social.MundoSocial;

namespace Maxx.Api.IntegrationTests.Social;

// Curtida, comentário e seguir (produto.md 6.1 e 6.3), contra o Postgres real.
public sealed class InteracoesTests(PostgresFixture postgres, ArmazenamentoFixture armazenamento)
    : IClassFixture<PostgresFixture>, IClassFixture<ArmazenamentoFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Curtir_e_descurtir_sao_idempotentes_e_devolvem_o_estado()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        using var bruno = await mundo.ComoAsync(Bruno);
        var post = await PublicadoAsync(ana);

        Assert.Equal(new CurtidasResponse(1, true), await CurtirAsync(bruno, post));
        Assert.Equal(new CurtidasResponse(1, true), await CurtirAsync(bruno, post));
        Assert.Equal(new CurtidasResponse(2, true), await CurtirAsync(ana, post));

        // No post lido, a contagem é de todos e o "curtiu" é de quem lê.
        var lidoPeloBruno = (await bruno.GetFromJsonAsync<PostResponse>($"/api/v1/posts/{post}", Ct))!;
        Assert.Equal((2, true), (lidoPeloBruno.Curtidas, lidoPeloBruno.Curtiu));
        using var diego = await mundo.ComoAsync(Diego);
        var lidoPeloDiego = (await diego.GetFromJsonAsync<PostResponse>($"/api/v1/posts/{post}", Ct))!;
        Assert.Equal((2, false), (lidoPeloDiego.Curtidas, lidoPeloDiego.Curtiu));

        Assert.Equal(new CurtidasResponse(1, false), await DescurtirAsync(bruno, post));
        Assert.Equal(new CurtidasResponse(1, false), await DescurtirAsync(bruno, post));
    }

    [Fact]
    public async Task Professora_e_ex_aluna_curtem_e_a_instituicao_nao()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        var post = await PublicadoAsync(ana);

        foreach (var quem in new[] { Renata, Carla })
        {
            using var client = await mundo.ComoAsync(quem);
            Assert.True((await CurtirAsync(client, post)).Curtiu);
        }

        using var coordenacao = await mundo.ComoAsync(Coordenacao);
        await AssertRecusaAsync(await PutCurtidaAsync(coordenacao, post), HttpStatusCode.Forbidden, "interacao-nao-permitida");
        await AssertRecusaAsync(
            await coordenacao.DeleteAsync(new Uri($"/api/v1/posts/{post}/curtida", UriKind.Relative), Ct), HttpStatusCode.Forbidden, "interacao-nao-permitida");
    }

    [Fact]
    public async Task Curtidas_ao_mesmo_tempo_da_mesma_pessoa_contam_uma()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        using var bruno = await mundo.ComoAsync(Bruno);
        var post = await PublicadoAsync(ana);

        var respostas = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => PutCurtidaAsync(bruno, post)));

        Assert.All(respostas, resposta => Assert.Equal(HttpStatusCode.OK, resposta.StatusCode));
        Assert.Single(await mundo.LinhasAsync<Curtida>());
    }

    [Fact]
    public async Task Post_inexistente_ou_removido_nao_recebe_curtida_nem_comentario()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        using var bruno = await mundo.ComoAsync(Bruno);
        using var coordenacao = await mundo.ComoAsync(Coordenacao);
        var removido = await PublicadoAsync(ana);
        Assert.Equal(HttpStatusCode.NoContent, (await coordenacao.DeleteAsync(new Uri($"/api/v1/posts/{removido}", UriKind.Relative), Ct)).StatusCode);

        foreach (var post in new[] { removido, Guid.NewGuid() })
        {
            await AssertRecusaAsync(await PutCurtidaAsync(bruno, post), HttpStatusCode.NotFound, "post-nao-encontrado");
            await AssertRecusaAsync(await ComentarAsync(bruno, post, "Oi"), HttpStatusCode.NotFound, "post-nao-encontrado");
            await AssertRecusaAsync(
                await bruno.GetAsync(new Uri($"/api/v1/posts/{post}/comentarios", UriKind.Relative), Ct), HttpStatusCode.NotFound, "post-nao-encontrado");
        }
    }

    [Fact]
    public async Task Comentario_sai_sem_espacos_nas_pontas_com_o_autor_e_conta_no_post()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        using var bruno = await mundo.ComoAsync(Bruno);
        var post = await PublicadoAsync(ana);

        var resposta = await ComentarAsync(bruno, post, "  Parabéns!  ");

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var comentario = (await resposta.Content.ReadFromJsonAsync<ComentarioResponse>(Ct))!;
        Assert.Equal("Parabéns!", comentario.Texto);
        Assert.Equal(new AutorDoComentarioResponse(mundo.Id(Bruno), "Bruno Lima", "@brunoLima-CC"), comentario.Autor);
        Assert.Equal(mundo.Relogio.GetUtcNow(), comentario.CriadoEm);
        Assert.Equal(1, (await ana.GetFromJsonAsync<PostResponse>($"/api/v1/posts/{post}", Ct))!.Comentarios);
        Assert.Equal([comentario], (await ComentariosAsync(ana, post)).Itens);
    }

    [Fact]
    public async Task Comentario_vazio_longo_ou_da_instituicao_e_recusado()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        using var coordenacao = await mundo.ComoAsync(Coordenacao);
        var post = await PublicadoAsync(ana);

        await AssertRecusaAsync(await ComentarAsync(ana, post, "   "), HttpStatusCode.BadRequest, "comentario-vazio");
        await AssertRecusaAsync(await ComentarAsync(ana, post, new string('a', 501)), HttpStatusCode.BadRequest, "comentario-longo");
        await AssertRecusaAsync(await ComentarAsync(ana, post, new string('a', 2001)), HttpStatusCode.BadRequest, "dados-invalidos");
        await AssertRecusaAsync(await ComentarAsync(coordenacao, post, "Oi"), HttpStatusCode.Forbidden, "interacao-nao-permitida");
        Assert.Empty(await mundo.LinhasAsync<Comentario>());
    }

    [Fact]
    public async Task Comentarios_vem_em_paginas_de_20_sem_repetir_nem_pular_mesmo_no_mesmo_instante()
    {
        // O relógio do teste não anda: os 45 comentários têm o mesmo instante, e quem
        // desempata é o id.
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        var post = await PublicadoAsync(ana);
        for (var i = 0; i < 45; i++)
        {
            Assert.Equal(HttpStatusCode.Created, (await ComentarAsync(ana, post, $"Comentário {i}")).StatusCode);
        }

        var lidos = new List<ComentarioResponse>();
        string? cursor = null;
        var tamanhos = new List<int>();
        do
        {
            var pagina = await ComentariosAsync(ana, post, cursor);
            tamanhos.Add(pagina.Itens.Count);
            lidos.AddRange(pagina.Itens);
            cursor = pagina.Proximo;
        }
        while (cursor is not null);

        Assert.Equal([20, 20, 5], tamanhos);
        Assert.Equal(45, lidos.Select(comentario => comentario.Id).Distinct().Count());
        Assert.Equal(
            Enumerable.Range(0, 45).Select(i => $"Comentário {i}").Order(StringComparer.Ordinal),
            lidos.Select(comentario => comentario.Texto).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("nao-e-cursor")]
    [InlineData("MTIzNA")]
    public async Task Cursor_que_a_api_nao_emitiu_e_400(string cursor)
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        var post = await PublicadoAsync(ana);

        await AssertRecusaAsync(
            await ana.GetAsync(new Uri($"/api/v1/posts/{post}/comentarios?cursor={cursor}", UriKind.Relative), Ct),
            HttpStatusCode.BadRequest,
            "cursor-invalido");
    }

    [Fact]
    public async Task Autor_apaga_o_comentario_e_o_dono_do_post_e_a_professora_nao()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        using var bruno = await mundo.ComoAsync(Bruno);
        using var renata = await mundo.ComoAsync(Renata);
        var post = await PublicadoAsync(ana);
        var comentario = await ComentadoAsync(bruno, post, "Oi");

        await AssertRecusaAsync(await RemoverComentarioAsync(ana, post, comentario), HttpStatusCode.Forbidden, "remocao-de-comentario-nao-permitida");
        await AssertRecusaAsync(await RemoverComentarioAsync(renata, post, comentario), HttpStatusCode.Forbidden, "remocao-de-comentario-nao-permitida");

        Assert.Equal(HttpStatusCode.NoContent, (await RemoverComentarioAsync(bruno, post, comentario)).StatusCode);
        Assert.Empty(await mundo.LinhasAsync<Comentario>());
        await AssertRecusaAsync(await RemoverComentarioAsync(bruno, post, comentario), HttpStatusCode.NotFound, "comentario-nao-encontrado");
    }

    [Fact]
    public async Task Instituicao_remove_o_comentario_gravando_quem_e_quando_e_ele_some()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        using var bruno = await mundo.ComoAsync(Bruno);
        using var coordenacao = await mundo.ComoAsync(Coordenacao);
        var post = await PublicadoAsync(ana);
        var comentario = await ComentadoAsync(bruno, post, "Impróprio");
        mundo.Relogio.Avancar(TimeSpan.FromMinutes(3));

        Assert.Equal(HttpStatusCode.NoContent, (await RemoverComentarioAsync(coordenacao, post, comentario)).StatusCode);

        var linha = Assert.Single(await mundo.LinhasAsync<Comentario>());
        Assert.Equal((mundo.Relogio.GetUtcNow(), mundo.Id(Coordenacao)), (linha.RemovidoEm, linha.RemovidoPorId));
        Assert.Empty((await ComentariosAsync(ana, post)).Itens);
        Assert.Equal(0, (await ana.GetFromJsonAsync<PostResponse>($"/api/v1/posts/{post}", Ct))!.Comentarios);
        await AssertRecusaAsync(await RemoverComentarioAsync(bruno, post, comentario), HttpStatusCode.NotFound, "comentario-nao-encontrado");
    }

    [Fact]
    public async Task Comentario_pelo_caminho_de_outro_post_e_404()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        var umPost = await PublicadoAsync(ana);
        var outroPost = await PublicadoAsync(ana);
        var comentario = await ComentadoAsync(ana, umPost, "Oi");

        await AssertRecusaAsync(await RemoverComentarioAsync(ana, outroPost, comentario), HttpStatusCode.NotFound, "comentario-nao-encontrado");
    }

    [Fact]
    public async Task Post_apagado_pelo_autor_leva_curtidas_e_comentarios()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        using var bruno = await mundo.ComoAsync(Bruno);
        var post = await PublicadoAsync(ana);
        await CurtirAsync(bruno, post);
        await ComentadoAsync(bruno, post, "Oi");

        Assert.Equal(HttpStatusCode.NoContent, (await ana.DeleteAsync(new Uri($"/api/v1/posts/{post}", UriKind.Relative), Ct)).StatusCode);

        Assert.Empty(await mundo.LinhasAsync<Curtida>());
        Assert.Empty(await mundo.LinhasAsync<Comentario>());
    }

    [Fact]
    public async Task Aluno_segue_e_deixa_de_seguir_aluno_ativo_ou_ex_aluno_sem_repetir()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        using var carla = await mundo.ComoAsync(Carla);

        Assert.Equal(HttpStatusCode.NoContent, (await SeguirAsync(ana, mundo.Id(Bruno))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SeguirAsync(ana, mundo.Id(Bruno))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SeguirAsync(ana, mundo.Id(Carla))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SeguirAsync(carla, mundo.Id(Ana))).StatusCode);

        var seguimentos = await mundo.LinhasAsync<Seguimento>();
        Assert.Equal(3, seguimentos.Count);
        Assert.Contains(seguimentos, seguimento => (seguimento.SeguidorId, seguimento.SeguidoId) == (mundo.Id(Ana), mundo.Id(Bruno)));

        Assert.Equal(HttpStatusCode.NoContent, (await DeixarDeSeguirAsync(ana, mundo.Id(Bruno))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await DeixarDeSeguirAsync(ana, mundo.Id(Bruno))).StatusCode);
        Assert.Equal(2, (await mundo.LinhasAsync<Seguimento>()).Count);
    }

    [Fact]
    public async Task Ninguem_segue_professor_instituicao_ou_a_si_mesmo_e_a_instituicao_nao_segue()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        using var coordenacao = await mundo.ComoAsync(Coordenacao);

        foreach (var alvo in new[] { Renata, Coordenacao, Ana })
        {
            await AssertRecusaAsync(await SeguirAsync(ana, mundo.Id(alvo)), HttpStatusCode.Forbidden, "seguir-nao-permitido");
        }

        await AssertRecusaAsync(await SeguirAsync(coordenacao, mundo.Id(Ana)), HttpStatusCode.Forbidden, "seguir-nao-permitido");
        await AssertRecusaAsync(await SeguirAsync(ana, Guid.NewGuid()), HttpStatusCode.NotFound, "usuario-nao-encontrado");
        Assert.Empty(await mundo.LinhasAsync<Seguimento>());
    }

    [Fact]
    public async Task Professora_segue_so_os_alunos_das_turmas_dela_e_o_seguimento_fica_depois_do_semestre()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var renata = await mundo.ComoAsync(Renata);

        Assert.Equal(HttpStatusCode.NoContent, (await SeguirAsync(renata, mundo.Id(Ana))).StatusCode);

        // A Carla não está matriculada na turma da Renata.
        await AssertRecusaAsync(await SeguirAsync(renata, mundo.Id(Carla)), HttpStatusCode.Forbidden, "seguir-nao-permitido");

        // Passado o semestre, o escopo acaba: seguir de novo, não; o que já seguia, fica, e
        // deixar de seguir vale.
        mundo.Relogio.Avancar(TimeSpan.FromDays(365));
        await AssertRecusaAsync(await SeguirAsync(renata, mundo.Id(Bruno)), HttpStatusCode.Forbidden, "seguir-nao-permitido");
        Assert.Single(await mundo.LinhasAsync<Seguimento>());
        Assert.Equal(HttpStatusCode.NoContent, (await DeixarDeSeguirAsync(renata, mundo.Id(Ana))).StatusCode);
        Assert.Empty(await mundo.LinhasAsync<Seguimento>());
    }

    private static async Task<Guid> PublicadoAsync(HttpClient client)
    {
        var resposta = await client.PostAsJsonAsync(new Uri("/api/v1/posts", UriKind.Relative), new { legenda = "Oi, turma." }, Ct);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<PostResponse>(Ct))!.Id;
    }

    private static Task<HttpResponseMessage> PutCurtidaAsync(HttpClient client, Guid post) =>
        client.PutAsync(new Uri($"/api/v1/posts/{post}/curtida", UriKind.Relative), null, Ct);

    private static async Task<CurtidasResponse> CurtirAsync(HttpClient client, Guid post)
    {
        var resposta = await PutCurtidaAsync(client, post);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<CurtidasResponse>(Ct))!;
    }

    private static async Task<CurtidasResponse> DescurtirAsync(HttpClient client, Guid post)
    {
        var resposta = await client.DeleteAsync(new Uri($"/api/v1/posts/{post}/curtida", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<CurtidasResponse>(Ct))!;
    }

    private static Task<HttpResponseMessage> ComentarAsync(HttpClient client, Guid post, string texto) =>
        client.PostAsJsonAsync(new Uri($"/api/v1/posts/{post}/comentarios", UriKind.Relative), new { texto }, Ct);

    private static async Task<Guid> ComentadoAsync(HttpClient client, Guid post, string texto)
    {
        var resposta = await ComentarAsync(client, post, texto);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<ComentarioResponse>(Ct))!.Id;
    }

    private static async Task<PaginaDeComentariosResponse> ComentariosAsync(HttpClient client, Guid post, string? cursor = null) =>
        (await client.GetFromJsonAsync<PaginaDeComentariosResponse>(
            $"/api/v1/posts/{post}/comentarios{(cursor is null ? string.Empty : $"?cursor={cursor}")}", Ct))!;

    private static Task<HttpResponseMessage> RemoverComentarioAsync(HttpClient client, Guid post, Guid comentario) =>
        client.DeleteAsync(new Uri($"/api/v1/posts/{post}/comentarios/{comentario}", UriKind.Relative), Ct);

    private static Task<HttpResponseMessage> SeguirAsync(HttpClient client, Guid usuario) =>
        client.PutAsync(new Uri($"/api/v1/seguindo/{usuario}", UriKind.Relative), null, Ct);

    private static Task<HttpResponseMessage> DeixarDeSeguirAsync(HttpClient client, Guid usuario) =>
        client.DeleteAsync(new Uri($"/api/v1/seguindo/{usuario}", UriKind.Relative), Ct);
}
