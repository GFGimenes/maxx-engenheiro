using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Maxx.Api.IntegrationTests.Fotos;
using Maxx.Api.Mural;
using Maxx.Domain.Mural;
using Maxx.Domain.Notificacoes;
using static Maxx.Api.IntegrationTests.Mural.MundoDoMural;

namespace Maxx.Api.IntegrationTests.Mural;

// Ler o mural, abrir um aviso, registrar a leitura e o "Ler todos" (produto.md 7), contra o
// Postgres e o bucket reais. Cada um vê o mural dos vínculos dele (passo 10, pergunta 3).
public sealed class LeituraDoMuralTests(PostgresFixture postgres, ArmazenamentoFixture armazenamento)
    : IClassFixture<PostgresFixture>, IClassFixture<ArmazenamentoFixture>
{
    private static readonly Uri _mural = new("/api/v1/mural", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Os cinco avisos do mundo, um segundo entre cada um, do mais antigo ao mais novo.
    private sealed record Avisos(Guid Faculdade, Guid CursoCC, Guid CursoADS, Guid TurmaBD, Guid TurmaWeb);

    private static async Task<Avisos> PublicarOsCincoAsync(MundoDoMural mundo)
    {
        using var coordenacao = await mundo.ComoAsync(Coordenacao);
        using var renata = await mundo.ComoAsync(Renata);
        using var paulo = await mundo.ComoAsync(Paulo);

        async Task<Guid> Publicar(HttpClient client, object pedido)
        {
            mundo.Relogio.Avancar(TimeSpan.FromSeconds(1));
            var resposta = await client.PostAsJsonAsync(new Uri("/api/v1/avisos", UriKind.Relative), pedido, Ct);
            Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
            return (await resposta.Content.ReadFromJsonAsync<AvisoPublicadoResponse>(Ct))!.Id;
        }

        return new Avisos(
            await Publicar(coordenacao, new { escopo = "faculdade", titulo = "Matrícula até 30/07", texto = "Renove pelo Portal do Aluno." }),
            await Publicar(coordenacao, new { escopo = "curso", cursoIds = new[] { mundo.CC }, titulo = "Semana de CC", texto = "Inscrições abertas." }),
            await Publicar(coordenacao, new { escopo = "curso", cursoIds = new[] { mundo.ADS }, titulo = "Semana de ADS", texto = "Inscrições abertas." }),
            await Publicar(renata, new { escopo = "turma", turmaIds = new[] { mundo.BancoDeDados }, titulo = "Prova remarcada", texto = "Passou para 14/08." }),
            await Publicar(paulo, new { escopo = "turma", turmaIds = new[] { mundo.Web }, titulo = "Entrega do projeto", texto = "Até sexta." }));
    }

    [Fact]
    public async Task Cada_um_ve_o_mural_dos_vinculos_dele_do_mais_novo_ao_mais_antigo()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        var avisos = await PublicarOsCincoAsync(mundo);

        // Ana: faculdade, o curso dela e a turma dela. Diego, o mesmo do lado de ADS.
        Assert.Equal([avisos.TurmaBD, avisos.CursoCC, avisos.Faculdade], await IdsAsync(mundo, Ana));
        Assert.Equal([avisos.TurmaWeb, avisos.CursoADS, avisos.Faculdade], await IdsAsync(mundo, Diego));

        // A ex-aluna lê pelos mesmos vínculos: o curso em que estava e a turma em que esteve.
        Assert.Equal([avisos.TurmaBD, avisos.CursoCC, avisos.Faculdade], await IdsAsync(mundo, Carla));

        // O professor vê a faculdade e os que publicou; a instituição, tudo, inclusive na conta
        // que acumula professora e instituição.
        Assert.Equal([avisos.TurmaBD, avisos.Faculdade], await IdsAsync(mundo, Renata));
        Guid[] todos = [avisos.TurmaWeb, avisos.TurmaBD, avisos.CursoADS, avisos.CursoCC, avisos.Faculdade];
        Assert.Equal(todos, await IdsAsync(mundo, Coordenacao));
        Assert.Equal(todos, await IdsAsync(mundo, Marta));
    }

    [Fact]
    public async Task O_filtro_por_escopo_mostra_so_aquele_escopo_do_mural()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        var avisos = await PublicarOsCincoAsync(mundo);

        Assert.Equal([avisos.Faculdade], await IdsAsync(mundo, Ana, "faculdade"));
        Assert.Equal([avisos.CursoCC], await IdsAsync(mundo, Ana, "curso"));
        Assert.Equal([avisos.TurmaBD], await IdsAsync(mundo, Ana, "turma"));

        using var ana = await mundo.ComoAsync(Ana);
        await AssertRecusaAsync(await ana.GetAsync(new Uri("/api/v1/mural?escopo=mural", UriKind.Relative), Ct), HttpStatusCode.BadRequest, "dados-invalidos");
    }

    [Fact]
    public async Task O_aviso_traz_autor_alvos_e_se_foi_lido()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var renata = await mundo.ComoAsync(Renata);
        using var coordenacao = await mundo.ComoAsync(Coordenacao);
        using var ana = await mundo.ComoAsync(Ana);
        var upload = await FotoDeAvisoAsync(renata);
        await renata.PostAsJsonAsync(
            new Uri("/api/v1/avisos", UriKind.Relative),
            new { escopo = "turma", turmaIds = new[] { mundo.BancoDeDados, mundo.Algoritmos }, titulo = "Laboratório", texto = "Sala nova.", uploadId = upload.Id },
            Ct);
        mundo.Relogio.Avancar(TimeSpan.FromSeconds(1));
        await coordenacao.PostAsJsonAsync(
            new Uri("/api/v1/avisos", UriKind.Relative),
            new { escopo = "curso", cursoIds = new[] { mundo.CC, mundo.ADS }, titulo = "Techday", texto = "Inscrições abertas." },
            Ct);

        var mural = await MuralAsync(ana);

        var techday = mural.Itens[0];
        Assert.Equal(("curso", "Techday", "Inscrições abertas."), (techday.Escopo, techday.Titulo, techday.Texto));
        Assert.Equal(new AutorDoAvisoResponse(mundo.Id(Coordenacao), "Coordenação UniMax", Instituicao: true), techday.Autor);
        Assert.Equal(["ADS", "CC"], techday.Cursos);
        Assert.Empty(techday.Materias);
        Assert.Null(techday.Foto);
        Assert.False(techday.Lido);
        Assert.Equal(mundo.Relogio.GetUtcNow(), techday.PublicadoEm);

        var laboratorio = mural.Itens[1];
        Assert.Equal(new AutorDoAvisoResponse(mundo.Id(Renata), "Renata Albuquerque", Instituicao: false), laboratorio.Autor);
        Assert.Equal(["Algoritmos", "Banco de Dados"], laboratorio.Materias);
        Assert.Empty(laboratorio.Cursos);
        using var http = new HttpClient();
        Assert.Equal(TamanhoDoFeed, (await http.GetByteArrayAsync(new Uri(laboratorio.Foto!.Feed), Ct)).Length);

        // Aberto pelo id, o mesmo aviso.
        var aberto = (await ana.GetFromJsonAsync<AvisoResponse>(new Uri($"/api/v1/avisos/{laboratorio.Id}", UriKind.Relative), Ct))!;
        Assert.Equal((laboratorio.Id, laboratorio.Titulo, laboratorio.Autor, laboratorio.Foto), (aberto.Id, aberto.Titulo, aberto.Autor, aberto.Foto));
        Assert.Equal(laboratorio.Materias, aberto.Materias);
    }

    [Fact]
    public async Task Aviso_fora_do_mural_e_404_como_o_que_nao_existe_e_nao_e_lido()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        var avisos = await PublicarOsCincoAsync(mundo);
        using var diego = await mundo.ComoAsync(Diego);

        foreach (var id in new[] { avisos.TurmaBD, avisos.CursoCC, Guid.CreateVersion7() })
        {
            await AssertRecusaAsync(
                await diego.GetAsync(new Uri($"/api/v1/avisos/{id}", UriKind.Relative), Ct), HttpStatusCode.NotFound, "aviso-nao-encontrado");
            await AssertRecusaAsync(
                await diego.PutAsync(new Uri($"/api/v1/avisos/{id}/leitura", UriKind.Relative), null, Ct),
                HttpStatusCode.NotFound,
                "aviso-nao-encontrado");
        }

        Assert.Empty(await mundo.LinhasAsync<LeituraDeAviso>());
    }

    [Fact]
    public async Task Abrir_registra_a_leitura_uma_vez_e_apaga_a_notificacao_do_aviso()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        var avisos = await PublicarOsCincoAsync(mundo);
        await mundo.EsvaziarFilaAsync();
        using var ana = await mundo.ComoAsync(Ana);
        var lerProva = new Uri($"/api/v1/avisos/{avisos.TurmaBD}/leitura", UriKind.Relative);

        mundo.Relogio.Avancar(TimeSpan.FromMinutes(1));
        var lidaEm = mundo.Relogio.GetUtcNow();
        Assert.Equal(HttpStatusCode.NoContent, (await ana.PutAsync(lerProva, null, Ct)).StatusCode);

        // Ler de novo não muda o instante da primeira leitura.
        mundo.Relogio.Avancar(TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.NoContent, (await ana.PutAsync(lerProva, null, Ct)).StatusCode);

        var leitura = Assert.Single(await mundo.LinhasAsync<LeituraDeAviso>());
        Assert.Equal((mundo.Id(Ana), avisos.TurmaBD, lidaEm), (leitura.UsuarioId, leitura.AvisoId, leitura.LidaEm));
        Assert.Equal(
            [avisos.TurmaBD],
            (await MuralAsync(ana)).Itens.Where(aviso => aviso.Lido).Select(aviso => aviso.Id));

        // A notificação do aviso lido apaga; as outras dela e a do Bruno, não.
        var notificacoes = await mundo.LinhasAsync<Notificacao>();
        var daProva = notificacoes.Single(notificacao => notificacao.UsuarioId == mundo.Id(Ana) && notificacao.ReferenciaId == avisos.TurmaBD);
        Assert.Equal(lidaEm, daProva.LidaEm);
        Assert.All(notificacoes.Where(notificacao => notificacao != daProva), notificacao => Assert.Null(notificacao.LidaEm));
    }

    [Fact]
    public async Task Ler_todos_marca_so_o_mural_de_quem_pede()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        var avisos = await PublicarOsCincoAsync(mundo);
        await mundo.EsvaziarFilaAsync();
        using var ana = await mundo.ComoAsync(Ana);

        Assert.Equal(HttpStatusCode.NoContent, (await ana.PostAsync(new Uri("/api/v1/mural/leitura", UriKind.Relative), null, Ct)).StatusCode);

        Assert.Equal(
            new[] { avisos.Faculdade, avisos.CursoCC, avisos.TurmaBD }.Order(),
            (await mundo.LinhasAsync<LeituraDeAviso>()).Select(leitura => leitura.AvisoId).Order());
        Assert.All(await mundo.LinhasAsync<LeituraDeAviso>(), leitura => Assert.Equal(mundo.Id(Ana), leitura.UsuarioId));
        Assert.All((await MuralAsync(ana)).Itens, aviso => Assert.True(aviso.Lido));

        var notificacoes = await mundo.LinhasAsync<Notificacao>();
        Assert.All(notificacoes.Where(notificacao => notificacao.UsuarioId == mundo.Id(Ana)), notificacao => Assert.NotNull(notificacao.LidaEm));
        Assert.All(notificacoes.Where(notificacao => notificacao.UsuarioId != mundo.Id(Ana)), notificacao => Assert.Null(notificacao.LidaEm));

        // De novo, sem nada a ler, não muda nada.
        Assert.Equal(HttpStatusCode.NoContent, (await ana.PostAsync(new Uri("/api/v1/mural/leitura", UriKind.Relative), null, Ct)).StatusCode);
        Assert.Equal(3, (await mundo.LinhasAsync<LeituraDeAviso>()).Count);
    }

    [Fact]
    public async Task O_mural_responde_304_ate_mudar_e_a_leitura_muda_a_etag()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        var avisos = await PublicarOsCincoAsync(mundo);
        using var ana = await mundo.ComoAsync(Ana);

        var primeira = await ana.GetAsync(_mural, Ct);
        Assert.Equal(HttpStatusCode.OK, primeira.StatusCode);
        var etag = primeira.Headers.ETag!;
        Assert.True(etag.IsWeak);
        Assert.True(primeira.Headers.CacheControl is { Private: true, NoCache: true });

        Assert.Equal(HttpStatusCode.NotModified, (await ComEtagAsync(ana, _mural, etag)).StatusCode);

        await ana.PutAsync(new Uri($"/api/v1/avisos/{avisos.Faculdade}/leitura", UriKind.Relative), null, Ct);
        var depois = await ComEtagAsync(ana, _mural, etag);
        Assert.Equal(HttpStatusCode.OK, depois.StatusCode);
        Assert.NotEqual(etag, depois.Headers.ETag);

        // O aviso aberto pelo id também tem ETag.
        var aviso = new Uri($"/api/v1/avisos/{avisos.Faculdade}", UriKind.Relative);
        var doAviso = (await ana.GetAsync(aviso, Ct)).Headers.ETag!;
        Assert.Equal(HttpStatusCode.NotModified, (await ComEtagAsync(ana, aviso, doAviso)).StatusCode);
    }

    [Fact]
    public async Task O_mural_pagina_por_cursor_de_20_em_20_sem_repetir()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var coordenacao = await mundo.ComoAsync(Coordenacao);
        using var ana = await mundo.ComoAsync(Ana);
        var publicados = new List<Guid>();
        for (var i = 1; i <= 25; i++)
        {
            // Os 25 no mesmo instante: o id desempata.
            var resposta = await coordenacao.PostAsJsonAsync(
                new Uri("/api/v1/avisos", UriKind.Relative), new { escopo = "faculdade", titulo = $"Aviso {i}", texto = "Texto." }, Ct);
            publicados.Add((await resposta.Content.ReadFromJsonAsync<AvisoPublicadoResponse>(Ct))!.Id);
        }

        var primeira = await MuralAsync(ana);
        var segunda = await MuralAsync(ana, cursor: primeira.Proximo);

        Assert.Equal(20, primeira.Itens.Count);
        Assert.NotNull(primeira.Proximo);
        Assert.Equal(5, segunda.Itens.Count);
        Assert.Null(segunda.Proximo);
        Assert.Equal(
            publicados.OrderDescending(),
            primeira.Itens.Concat(segunda.Itens).Select(aviso => aviso.Id));

        await AssertRecusaAsync(
            await ana.GetAsync(new Uri("/api/v1/mural?cursor=xyz", UriKind.Relative), Ct), HttpStatusCode.BadRequest, "cursor-invalido");
    }

    [Fact]
    public async Task O_perfil_do_professor_mostra_os_avisos_dele_que_estao_no_mural_de_quem_ve()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var renata = await mundo.ComoAsync(Renata);
        using var ana = await mundo.ComoAsync(Ana);
        using var bruno = await mundo.ComoAsync(Bruno);
        using var diego = await mundo.ComoAsync(Diego);

        async Task<Guid> Publicar(Guid turma, string titulo)
        {
            mundo.Relogio.Avancar(TimeSpan.FromSeconds(1));
            var resposta = await renata.PostAsJsonAsync(
                new Uri("/api/v1/avisos", UriKind.Relative), new { escopo = "turma", turmaIds = new[] { turma }, titulo, texto = "Texto." }, Ct);
            return (await resposta.Content.ReadFromJsonAsync<AvisoPublicadoResponse>(Ct))!.Id;
        }

        var deBd = await Publicar(mundo.BancoDeDados, "Prova de BD");
        var deAlg = await Publicar(mundo.Algoritmos, "Lista de Algoritmos");
        var doPerfil = new Uri($"/api/v1/perfis/{mundo.Id(Renata)}/avisos", UriKind.Relative);

        Assert.Equal([deAlg, deBd], (await ana.GetFromJsonAsync<PaginaDoMuralResponse>(doPerfil, Ct))!.Itens.Select(aviso => aviso.Id));
        Assert.Equal([deAlg], (await bruno.GetFromJsonAsync<PaginaDoMuralResponse>(doPerfil, Ct))!.Itens.Select(aviso => aviso.Id));
        Assert.Empty((await diego.GetFromJsonAsync<PaginaDoMuralResponse>(doPerfil, Ct))!.Itens);
        Assert.Equal([deAlg, deBd], (await renata.GetFromJsonAsync<PaginaDoMuralResponse>(doPerfil, Ct))!.Itens.Select(aviso => aviso.Id));

        // Perfil inexistente é 404, como na grade de posts; a instituição não tem perfil social.
        await AssertRecusaAsync(
            await ana.GetAsync(new Uri($"/api/v1/perfis/{Guid.CreateVersion7()}/avisos", UriKind.Relative), Ct),
            HttpStatusCode.NotFound,
            "usuario-nao-encontrado");
        await AssertRecusaAsync(
            await ana.GetAsync(new Uri($"/api/v1/perfis/{mundo.Id(Coordenacao)}/avisos", UriKind.Relative), Ct),
            HttpStatusCode.NotFound,
            "usuario-nao-encontrado");
    }

    private static async Task<PaginaDoMuralResponse> MuralAsync(HttpClient client, string? escopo = null, string? cursor = null)
    {
        var consulta = string.Join("&", new[] { escopo is null ? null : $"escopo={escopo}", cursor is null ? null : $"cursor={cursor}" }.OfType<string>());
        return (await client.GetFromJsonAsync<PaginaDoMuralResponse>(
            new Uri(consulta.Length == 0 ? "/api/v1/mural" : $"/api/v1/mural?{consulta}", UriKind.Relative), Ct))!;
    }

    private static async Task<List<Guid>> IdsAsync(MundoDoMural mundo, string email, string? escopo = null)
    {
        using var client = await mundo.ComoAsync(email);
        return [.. (await MuralAsync(client, escopo)).Itens.Select(aviso => aviso.Id)];
    }

    private static async Task<HttpResponseMessage> ComEtagAsync(HttpClient client, Uri uri, EntityTagHeaderValue etag)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Get, uri);
        pedido.Headers.IfNoneMatch.Add(etag);
        return await client.SendAsync(pedido, Ct);
    }
}
