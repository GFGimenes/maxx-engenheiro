using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Maxx.Api.Autenticacao;
using Maxx.Api.Feed;
using Maxx.Api.Fotos;
using Maxx.Api.IntegrationTests.Fotos;
using Maxx.Api.Perfil;
using Maxx.Api.Premios;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.Social.MundoSocial;

namespace Maxx.Api.IntegrationTests.Social;

// Perfil, grade, prêmios, busca e editar perfil (produto.md 6.4, 6.5 e 9.1), contra o
// Postgres e o bucket reais.
public sealed class PerfilTests(PostgresFixture postgres, ArmazenamentoFixture armazenamento)
    : IClassFixture<PostgresFixture>, IClassFixture<ArmazenamentoFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task O_proprio_perfil_do_aluno_tem_ra_pontos_e_contagens()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        using var bruno = await mundo.ComoAsync(Bruno);
        await mundo.MedalhaAsync(Ana);
        await SeguirAsync(bruno, mundo.Id(Ana));
        await SeguirAsync(ana, mundo.Id(Diego));

        var perfil = await MeuPerfilAsync(ana);

        Assert.Equal(
            new PerfilResponse(
                mundo.Id(Ana), "Ana Souza", "@anaSouza-CC", "aluno", "CC", "Ciência da Computação", 3, false, "20260101", 30, 1, 1, false, null, null),
            perfil);
    }

    [Fact]
    public async Task O_perfil_de_outro_nao_mostra_o_ra_e_diz_se_eu_sigo()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var bruno = await mundo.ComoAsync(Bruno);
        await SeguirAsync(bruno, mundo.Id(Ana));

        var ana = await PerfilAsync(bruno, mundo.Id(Ana));
        var carla = await PerfilAsync(bruno, mundo.Id(Carla));

        Assert.Null(ana.Ra);
        Assert.True(ana.EuSigo);
        Assert.Equal(1, ana.Seguidores);
        Assert.True(carla.ExAluno);
        Assert.Equal("@carlaDias-EX", carla.Arroba);
        Assert.False(carla.EuSigo);
    }

    [Fact]
    public async Task O_perfil_do_professor_mostra_as_turmas_e_as_medalhas_concedidas_sem_pontos()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        await mundo.MedalhaAsync(Ana);
        await mundo.MedalhaAsync(Bruno);

        var renata = await PerfilAsync(ana, mundo.Id(Renata));

        Assert.Equal(("professor", "@renataA-PROF", null, null), (renata.Papel, renata.Arroba, renata.Pontos, renata.Ra));
        Assert.Equal(2, renata.Professor!.MedalhasConcedidas);
        Assert.Equal("Banco de Dados", Assert.Single(renata.Professor.Turmas).Materia);
    }

    [Fact]
    public async Task Instituicao_e_quem_nao_existe_nao_tem_perfil()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);

        foreach (var id in new[] { mundo.Id(Coordenacao), Guid.NewGuid() })
        {
            await AssertRecusaAsync(await ana.GetAsync(new Uri($"/api/v1/perfis/{id}", UriKind.Relative), Ct), HttpStatusCode.NotFound, "usuario-nao-encontrado");
        }
    }

    [Fact]
    public async Task Perfil_tem_etag_privado_e_304_enquanto_nada_muda()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var bruno = await mundo.ComoAsync(Bruno);
        var caminho = new Uri($"/api/v1/perfis/{mundo.Id(Ana)}", UriKind.Relative);

        var primeira = await bruno.GetAsync(caminho, Ct);
        var etag = primeira.Headers.ETag!;
        Assert.True(etag.IsWeak);
        Assert.True(primeira.Headers.CacheControl!.Private);

        using var deNovo = new HttpRequestMessage(HttpMethod.Get, caminho);
        deNovo.Headers.IfNoneMatch.Add(etag);
        Assert.Equal(HttpStatusCode.NotModified, (await bruno.SendAsync(deNovo, Ct)).StatusCode);

        // Seguir muda o "eu sigo" e a contagem: outra versão.
        await SeguirAsync(bruno, mundo.Id(Ana));
        using var depois = new HttpRequestMessage(HttpMethod.Get, caminho);
        depois.Headers.IfNoneMatch.Add(etag);
        var mudou = await bruno.SendAsync(depois, Ct);
        Assert.Equal(HttpStatusCode.OK, mudou.StatusCode);
        Assert.NotEqual(etag, mudou.Headers.ETag);
    }

    [Fact]
    public async Task A_grade_traz_so_os_posts_do_perfil_em_paginas_de_18()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        using var bruno = await mundo.ComoAsync(Bruno);
        var daAna = new List<Guid>();
        for (var i = 0; i < 20; i++)
        {
            mundo.Relogio.Avancar(TimeSpan.FromMilliseconds(100));
            daAna.Add(await PublicarAsync(ana, $"Post {i}"));
        }

        await PublicarAsync(bruno, "Do Bruno");

        var primeira = await GradeAsync(bruno, mundo.Id(Ana));
        var segunda = await GradeAsync(bruno, mundo.Id(Ana), primeira.Proximo);

        Assert.Equal(Enumerable.Reverse(daAna).Take(18), primeira.Itens.Select(post => post.Id));
        Assert.Equal(Enumerable.Reverse(daAna).Skip(18), segunda.Itens.Select(post => post.Id));
        Assert.Null(segunda.Proximo);
        Assert.Empty((await GradeAsync(bruno, mundo.Id(Renata))).Itens);
    }

    [Fact]
    public async Task Os_premios_do_perfil_sao_so_os_obtidos_e_o_proprio_aluno_ve_os_faltantes()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        using var bruno = await mundo.ComoAsync(Bruno);
        await mundo.MedalhaAsync(Ana);

        var publicos = (await bruno.GetFromJsonAsync<PremiosDoAlunoResponse>($"/api/v1/perfis/{mundo.Id(Ana)}/premios", Ct))!;
        var meus = (await ana.GetFromJsonAsync<PremiosDoAlunoResponse>("/api/v1/eu/premios", Ct))!;

        Assert.Equal("M01", Assert.Single(publicos.Itens).Codigo);
        Assert.Equal(30, publicos.Pontos);
        Assert.Contains(meus.Itens, item => item.Codigo == "M03" && item.Obtidos.Count == 0);
        await AssertRecusaAsync(
            await ana.GetAsync(new Uri($"/api/v1/perfis/{mundo.Id(Renata)}/premios", UriKind.Relative), Ct), HttpStatusCode.NotFound, "usuario-nao-encontrado");
    }

    [Fact]
    public async Task Editar_nome_e_prefixo_muda_o_arroba_e_o_antigo_fica_reservado_so_para_o_dono()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        using var bruno = await mundo.ComoAsync(Bruno);

        var editado = await EditadoAsync(ana, new { nomeDeExibicao = "  Aninha  ", prefixo = "aninha" });

        Assert.Equal(("Aninha", "@aninha-CC"), (editado.NomeDeExibicao, editado.Arroba));
        Assert.Equal("@aninha-CC", (await ana.GetFromJsonAsync<EuResponse>("/api/v1/eu", Ct))!.Arroba);

        await AssertRecusaAsync(await EditarAsync(bruno, new { prefixo = "ANASOUZA" }), HttpStatusCode.Conflict, "prefixo-indisponivel");
        await AssertRecusaAsync(await EditarAsync(bruno, new { prefixo = "aninha" }), HttpStatusCode.Conflict, "prefixo-indisponivel");
        Assert.Equal("@anaSouza-CC", (await EditadoAsync(ana, new { prefixo = "anaSouza" })).Arroba);
    }

    [Fact]
    public async Task Edicao_recusada_nao_grava_nada()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);

        await AssertRecusaAsync(await EditarAsync(ana, new { nomeDeExibicao = "Aninha", prefixo = "a-b" }), HttpStatusCode.BadRequest, "prefixo-invalido");
        await AssertRecusaAsync(await EditarAsync(ana, new { nomeDeExibicao = "   " }), HttpStatusCode.BadRequest, "nome-de-exibicao-invalido");

        var perfil = await MeuPerfilAsync(ana);
        Assert.Equal(("Ana Souza", "@anaSouza-CC"), (perfil.NomeDeExibicao, perfil.Arroba));
    }

    [Fact]
    public async Task Disponibilidade_do_prefixo_considera_a_reserva_do_proprio_dono()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        await EditadoAsync(ana, new { prefixo = "aninha" });

        Assert.True(await DisponivelAsync(ana, "anaSouza"));
        Assert.True(await DisponivelAsync(ana, "novoPrefixo"));
        Assert.False(await DisponivelAsync(ana, "brunolima"));
        await AssertRecusaAsync(
            await ana.GetAsync(new Uri("/api/v1/eu/perfil/disponibilidade?prefixo=a-b", UriKind.Relative), Ct), HttpStatusCode.BadRequest, "prefixo-invalido");
    }

    [Fact]
    public async Task Foto_de_perfil_aparece_no_perfil_no_eu_e_no_feed_e_a_trocada_sai_do_bucket()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        var primeira = await FotoDePerfilAsync(ana);

        var perfil = await EditadoAsync(ana, new { fotoUploadId = primeira.Id });

        Assert.StartsWith(armazenamento.UrlDeLeitura.ToString(), perfil.Foto!.Grade, StringComparison.Ordinal);
        Assert.Equal(perfil.Foto.Grade, (await ana.GetFromJsonAsync<EuResponse>("/api/v1/eu", Ct))!.Foto);
        await PublicarAsync(ana, "Com foto");
        Assert.Equal(perfil.Foto.Grade, Assert.Single((await ana.GetFromJsonAsync<PaginaDoFeedResponse>("/api/v1/feed", Ct))!.Itens).Autor.Foto);

        var segunda = await FotoDePerfilAsync(ana);
        var trocada = await EditadoAsync(ana, new { fotoUploadId = segunda.Id });
        await mundo.EsvaziarFilaAsync();
        Assert.NotEqual(perfil.Foto.Grade, trocada.Foto!.Grade);
        using var http = new HttpClient();
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(new Uri(perfil.Foto.Grade), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync(new Uri(trocada.Foto.Grade), Ct)).StatusCode);

        var semFoto = await EditadoAsync(ana, new { removerFoto = true });
        await mundo.EsvaziarFilaAsync();
        Assert.Null(semFoto.Foto);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(new Uri(trocada.Foto.Feed), Ct)).StatusCode);
    }

    [Fact]
    public async Task Foto_de_post_que_nao_chegou_ou_trocar_e_remover_juntos_sao_recusados()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        var dePost = await FotoEnviadaAsync(ana);
        var naoEnviada = await FotoDePerfilAsync(ana, enviar: false);
        var boa = await FotoDePerfilAsync(ana);

        await AssertRecusaAsync(await EditarAsync(ana, new { fotoUploadId = dePost.Id }), HttpStatusCode.NotFound, "upload-nao-encontrado");
        await AssertRecusaAsync(await EditarAsync(ana, new { fotoUploadId = naoEnviada.Id }), HttpStatusCode.Conflict, "foto-nao-enviada");
        await AssertRecusaAsync(
            await EditarAsync(ana, new { fotoUploadId = boa.Id, removerFoto = true }), HttpStatusCode.BadRequest, "foto-de-perfil-ambigua");
        Assert.Null((await MeuPerfilAsync(ana)).Foto);
    }

    [Fact]
    public async Task Professora_tambem_tem_foto_de_perfil()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var renata = await mundo.ComoAsync(Renata);

        var perfil = await EditadoAsync(renata, new { fotoUploadId = (await FotoDePerfilAsync(renata)).Id });

        Assert.NotNull(perfil.Foto);
    }

    [Theory]
    [InlineData("joao", "João Brunão")]
    [InlineData("BRUNAO", "João Brunão")]
    [InlineData("@brunoLima-CC", "João Brunão")]
    [InlineData("nolim", "João Brunão")]
    [InlineData("souza", "Ana Souza")]
    public async Task Busca_por_nome_sem_acento_ou_por_arroba(string termo, string esperado)
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        using var bruno = await mundo.ComoAsync(Bruno);
        await EditadoAsync(bruno, new { nomeDeExibicao = "João Brunão" });

        var achados = await BuscarAsync(ana, $"termo={Uri.EscapeDataString(termo)}");

        Assert.Equal(esperado, Assert.Single(achados.Itens).NomeDeExibicao);
    }

    [Fact]
    public async Task Busca_por_filtro_traz_alunos_e_ex_alunos_por_nome_e_nunca_professor_nem_ra()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        await SeguirAsync(ana, mundo.Id(Diego));

        var doCurso = await BuscarAsync(ana, "curso=cc");
        var doQuinto = await BuscarAsync(ana, "semestre=5");

        Assert.Equal(["Ana Souza", "Bruno Lima", "Carla Dias", "Diego Reis"], doCurso.Itens.Select(aluno => aluno.NomeDeExibicao));
        Assert.True(doCurso.Itens.Single(aluno => aluno.NomeDeExibicao == "Carla Dias").ExAluno);
        var diego = Assert.Single(doQuinto.Itens);
        Assert.Equal(("Diego Reis", "CC", 5, true), (diego.NomeDeExibicao, diego.Curso, diego.Semestre, diego.EuSigo));
        Assert.Empty((await BuscarAsync(ana, "termo=20260101")).Itens);
        Assert.Empty((await BuscarAsync(ana, "termo=renata")).Itens);
        Assert.Empty((await BuscarAsync(ana, "termo=a%25")).Itens);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?termo=a")]
    [InlineData("?termo=@")]
    public async Task Busca_sem_criterio_e_400(string consulta)
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);

        await AssertRecusaAsync(await ana.GetAsync(new Uri($"/api/v1/busca/alunos{consulta}", UriKind.Relative), Ct), HttpStatusCode.BadRequest, "busca-sem-criterio");
        await AssertRecusaAsync(await ana.GetAsync(new Uri("/api/v1/busca/alunos?semestre=9", UriKind.Relative), Ct), HttpStatusCode.BadRequest, "dados-invalidos");
    }

    [Fact]
    public async Task Busca_pagina_de_20_em_20_pela_ordem_do_nome_sem_acento()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        await AlunosAsync(mundo, 25);

        var primeira = await BuscarAsync(ana, "termo=colega");
        var segunda = await BuscarAsync(ana, $"termo=colega&cursor={primeira.Proximo}");

        Assert.Equal(20, primeira.Itens.Count);
        Assert.Equal(5, segunda.Itens.Count);
        Assert.Null(segunda.Proximo);
        var nomes = primeira.Itens.Concat(segunda.Itens).Select(aluno => aluno.NomeDeExibicao).ToList();
        Assert.Equal(25, nomes.Distinct().Count());
        Assert.Equal(nomes.Order(StringComparer.Ordinal), nomes);
    }

    private static async Task AlunosAsync(MundoSocial mundo, int quantos)
    {
        await using var scope = mundo.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var cc = await db.Set<Curso>().SingleAsync(Ct);
        for (var i = 0; i < quantos; i++)
        {
            var cadastro = CadastroElegivel.Aluno($"2026{i + 500:D4}", $"colega{i}@al.unieduk.com.br", $"Colega {i:D2}", cc, 2);
            db.AddRange(cadastro, Usuario.CadastrarAluno(cadastro, cc, $"Colega {i:D2}", $"colega{i}", "hash", DateTimeOffset.UtcNow));
        }

        await db.SaveChangesAsync(Ct);
    }

    private static async Task<PerfilResponse> MeuPerfilAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<PerfilResponse>("/api/v1/eu/perfil", Ct))!;

    private static async Task<PerfilResponse> PerfilAsync(HttpClient client, Guid usuario) =>
        (await client.GetFromJsonAsync<PerfilResponse>($"/api/v1/perfis/{usuario}", Ct))!;

    private static async Task<PaginaDoFeedResponse> GradeAsync(HttpClient client, Guid usuario, string? cursor = null) =>
        (await client.GetFromJsonAsync<PaginaDoFeedResponse>(
            $"/api/v1/perfis/{usuario}/posts{(cursor is null ? string.Empty : $"?cursor={cursor}")}", Ct))!;

    private static async Task<PaginaDeAlunosResponse> BuscarAsync(HttpClient client, string consulta) =>
        (await client.GetFromJsonAsync<PaginaDeAlunosResponse>($"/api/v1/busca/alunos?{consulta}", Ct))!;

    private static Task<HttpResponseMessage> EditarAsync(HttpClient client, object pedido) =>
        client.PatchAsJsonAsync(new Uri("/api/v1/eu/perfil", UriKind.Relative), pedido, Ct);

    private static async Task<PerfilResponse> EditadoAsync(HttpClient client, object pedido)
    {
        var resposta = await EditarAsync(client, pedido);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<PerfilResponse>(Ct))!;
    }

    private static async Task<bool> DisponivelAsync(HttpClient client, string prefixo) =>
        (await client.GetFromJsonAsync<DisponibilidadeDoPrefixoResponse>($"/api/v1/eu/perfil/disponibilidade?prefixo={prefixo}", Ct))!.Disponivel;

    private static async Task SeguirAsync(HttpClient client, Guid usuario) =>
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync(new Uri($"/api/v1/seguindo/{usuario}", UriKind.Relative), null, Ct)).StatusCode);

    private static async Task<Guid> PublicarAsync(HttpClient client, string legenda)
    {
        var resposta = await client.PostAsJsonAsync(new Uri("/api/v1/posts", UriKind.Relative), new { legenda }, Ct);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<Posts.PostResponse>(Ct))!.Id;
    }

    // Pedido de upload de foto de perfil e os dois PUTs.
    private static async Task<UploadResponse> FotoDePerfilAsync(HttpClient client, bool enviar = true)
    {
        var resposta = await client.PostAsJsonAsync(
            new Uri("/api/v1/uploads", UriKind.Relative),
            new { finalidade = "fotoDePerfil", tipoDeConteudo = "image/jpeg", tamanhoDoFeed = TamanhoDoFeed, tamanhoDaGrade = TamanhoDaGrade },
            Ct);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var upload = (await resposta.Content.ReadFromJsonAsync<UploadResponse>(Ct))!;
        if (enviar)
        {
            using var http = new HttpClient();
            foreach (var (envio, tamanho) in new[] { (upload.Feed, TamanhoDoFeed), (upload.Grade, TamanhoDaGrade) })
            {
                using var corpo = new ByteArrayContent(new byte[tamanho]);
                corpo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                Assert.Equal(HttpStatusCode.OK, (await http.PutAsync(new Uri(envio.Url), corpo, Ct)).StatusCode);
            }
        }

        return upload;
    }
}
