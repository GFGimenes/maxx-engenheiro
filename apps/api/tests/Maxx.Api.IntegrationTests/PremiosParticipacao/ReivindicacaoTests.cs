using System.Net;
using System.Net.Http.Json;
using Maxx.Api.IntegrationTests.Fotos;
using Maxx.Api.Perfil;
using Maxx.Api.Posts;
using Maxx.Domain.Fotos;
using Maxx.Domain.Premios;
using Maxx.Domain.Social;
using static Maxx.Api.IntegrationTests.Social.MundoSocial;

namespace Maxx.Api.IntegrationTests.PremiosParticipacao;

// POST /v1/reivindicacoes (fluxos/02-post-com-comprovacao.md, até antes da fila): o post com
// foto, público com a marca de pendente, e o prêmio reivindicado, que não pontua. Postgres e
// bucket reais. No MundoSocial, hoje está no primeiro semestre do calendário, e nenhum
// semestre terminou ainda: o limite inferior da D3 fica nos testes de domínio.
public sealed class ReivindicacaoTests(PostgresFixture postgres, ArmazenamentoFixture armazenamento)
    : IClassFixture<PostgresFixture>, IClassFixture<ArmazenamentoFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reivindicacao_com_evento_semeado_vira_post_publico_pendente_que_nao_pontua()
    {
        await using var mundo = await MundoAsync();
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var bruno = await mundo.Social.ComoAsync(Bruno);
        var upload = await FotoEnviadaAsync(ana);

        var resposta = await ReivindicarAsync(ana, new { item = "P06", eventoId = mundo.Semana, uploadId = upload.Id, legenda = " Semana acadêmica! " });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var post = (await resposta.Content.ReadFromJsonAsync<PostResponse>(Ct))!;
        Assert.Equal("Semana acadêmica!", post.Legenda);
        Assert.NotNull(post.Foto);
        var premio = post.Premio!;
        Assert.Equal(
            ("premioParticipacao", "P06", "Semana acadêmica", "bronze", 30, "reivindicado", "Semana Acadêmica de Computação", (string?)null, (int?)null),
            (premio.Tipo, premio.Item, premio.Nome, premio.Tier, premio.Pontos, premio.Estado, premio.Evento, premio.Materia, premio.Bimestre));

        // Público desde o envio, com a marca, para qualquer um.
        var lidoPeloBruno = (await bruno.GetFromJsonAsync<PostResponse>(new Uri($"/api/v1/posts/{post.Id}", UriKind.Relative), Ct))!;
        Assert.Equal("reivindicado", lidoPeloBruno.Premio!.Estado);

        // Reivindicado não pontua.
        var perfil = (await ana.GetFromJsonAsync<PerfilResponse>(new Uri("/api/v1/eu/perfil", UriKind.Relative), Ct))!;
        Assert.Equal(0, perfil.Pontos);

        var gravado = Assert.Single(await mundo.Social.LinhasAsync<PremioParticipacao>());
        Assert.Equal((EstadoDoPremioParticipacao.Reivindicado, mundo.Semana, (Guid?)null), (gravado.Estado, gravado.EventoId, gravado.MateriaId));
        var comprovacao = Assert.Single(await mundo.Social.LinhasAsync<Comprovacao>());
        Assert.Equal((gravado.Id, mundo.Semana, (Guid?)post.Id), (comprovacao.PremioParticipacaoId, comprovacao.EventoId, comprovacao.PostId));
        Assert.NotNull(Assert.Single(await mundo.Social.LinhasAsync<UploadPendente>()).ConfirmadoEm);
        Assert.Equal(gravado.Id, Assert.Single(await mundo.Social.LinhasAsync<Post>()).PremioParticipacaoId);
    }

    [Fact]
    public async Task Evento_digitado_e_gravado_com_quem_digitou_e_a_apresentacao_em_sala_leva_a_materia()
    {
        await using var mundo = await MundoAsync();
        using var ana = await mundo.Social.ComoAsync(Ana);

        var resposta = await ReivindicarAsync(ana, new
        {
            item = "P17",
            eventoDigitado = new { nome = "Seminário de normalização", inicio = mundo.Hoje.AddDays(-3), fim = mundo.Hoje.AddDays(-3) },
            materiaId = mundo.BancoDeDados,
            uploadId = (await FotoEnviadaAsync(ana)).Id,
        });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var premio = (await resposta.Content.ReadFromJsonAsync<PostResponse>(Ct))!.Premio!;
        Assert.Equal(("P17", "Seminário de normalização", "Banco de Dados"), (premio.Item, premio.Evento, premio.Materia));
        var evento = Assert.Single(await mundo.Social.LinhasAsync<Evento>(), evento => evento.Origem == OrigemDoEvento.TextoLivre);
        Assert.Equal((mundo.Social.Id(Ana), (string?)null, (Guid?)null), (evento.CriadoPorId!.Value, evento.Codigo, evento.CursoId));
    }

    [Fact]
    public async Task Um_premio_por_evento_e_no_digitado_um_por_item_e_data()
    {
        await using var mundo = await MundoAsync();
        using var ana = await mundo.Social.ComoAsync(Ana);
        object Digitado(string nome, int diasAtras) => new { nome, inicio = mundo.Hoje.AddDays(-diasAtras), fim = mundo.Hoje.AddDays(-diasAtras) };

        Assert.Equal(HttpStatusCode.Created, (await ReivindicarAsync(ana, new { item = "P06", eventoId = mundo.Semana, uploadId = (await FotoEnviadaAsync(ana)).Id })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await ReivindicarAsync(ana, new { item = "P01", eventoDigitado = Digitado("Palestra de LGPD", 5), uploadId = (await FotoEnviadaAsync(ana)).Id })).StatusCode);

        await AssertRecusaAsync(
            await ReivindicarAsync(ana, new { item = "P06", eventoId = mundo.Semana, uploadId = (await FotoEnviadaAsync(ana)).Id }),
            HttpStatusCode.Conflict,
            "premio-ja-reivindicado");

        // Outro nome, mesmo item e mesma data de início: o sistema bloqueia (produto.md 4.6).
        await AssertRecusaAsync(
            await ReivindicarAsync(ana, new { item = "P01", eventoDigitado = Digitado("Palestra sobre dados pessoais", 5), uploadId = (await FotoEnviadaAsync(ana)).Id }),
            HttpStatusCode.Conflict,
            "premio-ja-reivindicado");

        // Outra data passa.
        Assert.Equal(HttpStatusCode.Created, (await ReivindicarAsync(ana, new { item = "P01", eventoDigitado = Digitado("Palestra de LGPD, parte 2", 4), uploadId = (await FotoEnviadaAsync(ana)).Id })).StatusCode);
        Assert.Equal(3, (await mundo.Social.LinhasAsync<PremioParticipacao>()).Count);

        // A recusa não deixa evento digitado nem post órfão.
        Assert.Equal(2, (await mundo.Social.LinhasAsync<Evento>()).Count(evento => evento.Origem == OrigemDoEvento.TextoLivre));
        Assert.Equal(3, (await mundo.Social.LinhasAsync<Post>()).Count);
    }

    [Fact]
    public async Task Duas_reivindicacoes_ao_mesmo_tempo_do_mesmo_evento_rendem_uma_so()
    {
        await using var mundo = await MundoAsync();
        using var ana = await mundo.Social.ComoAsync(Ana);
        var uploads = new List<Guid>();
        for (var i = 0; i < 6; i++)
        {
            uploads.Add((await FotoEnviadaAsync(ana)).Id);
        }

        var respostas = await Task.WhenAll(uploads.Select(upload =>
            ReivindicarAsync(ana, new { item = "P06", eventoId = mundo.Semana, uploadId = upload })));

        Assert.Single(respostas, resposta => resposta.StatusCode == HttpStatusCode.Created);
        foreach (var recusada in respostas.Where(resposta => resposta.StatusCode != HttpStatusCode.Created))
        {
            await AssertRecusaAsync(recusada, HttpStatusCode.Conflict, "premio-ja-reivindicado");
        }

        Assert.Single(await mundo.Social.LinhasAsync<PremioParticipacao>());
        Assert.Single(await mundo.Social.LinhasAsync<Post>());
        Assert.Single(await mundo.Social.LinhasAsync<Comprovacao>());
    }

    public static TheoryData<string, HttpStatusCode, string> Recusas => new()
    {
        { "semEvento", HttpStatusCode.BadRequest, "evento-obrigatorio" },
        { "doisEventos", HttpStatusCode.BadRequest, "evento-ambiguo" },
        { "nomeCurto", HttpStatusCode.BadRequest, "evento-invalido" },
        { "fimAntesDoInicio", HttpStatusCode.BadRequest, "evento-invalido" },
        { "eventoInexistente", HttpStatusCode.NotFound, "evento-nao-encontrado" },
        { "eventoDeOutroCurso", HttpStatusCode.NotFound, "evento-nao-encontrado" },
        { "itemQueOEventoNaoAceita", HttpStatusCode.Conflict, "evento-nao-aceita-o-item" },
        { "eventoFuturo", HttpStatusCode.Conflict, "evento-nao-comecou" },
        { "medalha", HttpStatusCode.NotFound, "item-nao-encontrado" },
        { "salaSemMateria", HttpStatusCode.BadRequest, "materia-obrigatoria" },
        { "materiaSemSala", HttpStatusCode.BadRequest, "materia-nao-se-aplica" },
        { "materiaQueNaoCursa", HttpStatusCode.NotFound, "materia-nao-encontrada" },
        { "fotoQueNaoSubiu", HttpStatusCode.Conflict, "foto-nao-enviada" },
        { "fotoDeOutro", HttpStatusCode.NotFound, "upload-nao-encontrado" },
    };

    [Theory]
    [MemberData(nameof(Recusas))]
    public async Task Pedido_recusado_nao_grava_nada(string caso, HttpStatusCode status, string codigo)
    {
        await using var mundo = await MundoAsync();
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var bruno = await mundo.Social.ComoAsync(Bruno);
        var foto = (await FotoEnviadaAsync(ana)).Id;
        object Dia(string nome, int deslocamento, int duracao = 0) =>
            new { nome, inicio = mundo.Hoje.AddDays(deslocamento), fim = mundo.Hoje.AddDays(deslocamento + duracao) };

        object pedido = caso switch
        {
            "semEvento" => new { item = "P01", uploadId = foto },
            "doisEventos" => new { item = "P06", eventoId = mundo.Semana, eventoDigitado = Dia("Palestra", -1), uploadId = foto },
            "nomeCurto" => new { item = "P01", eventoDigitado = Dia("TI", -1), uploadId = foto },
            "fimAntesDoInicio" => new { item = "P01", eventoDigitado = Dia("Palestra", -1, -2), uploadId = foto },
            "eventoInexistente" => new { item = "P06", eventoId = Guid.NewGuid(), uploadId = foto },
            "eventoDeOutroCurso" => new { item = "P06", eventoId = mundo.SemanaDeAdm, uploadId = foto },
            "itemQueOEventoNaoAceita" => new { item = "P01", eventoId = mundo.Semana, uploadId = foto },
            "eventoFuturo" => new { item = "P01", eventoDigitado = Dia("Palestra de amanhã", 1), uploadId = foto },
            "medalha" => new { item = "M01", eventoDigitado = Dia("Palestra", -1), uploadId = foto },
            "salaSemMateria" => new { item = "P17", eventoDigitado = Dia("Seminário", -1), uploadId = foto },
            "materiaSemSala" => new { item = "P01", eventoDigitado = Dia("Palestra", -1), materiaId = mundo.BancoDeDados, uploadId = foto },
            "materiaQueNaoCursa" => new { item = "P17", eventoDigitado = Dia("Seminário", -1), materiaId = Guid.NewGuid(), uploadId = foto },
            "fotoQueNaoSubiu" => new { item = "P01", eventoDigitado = Dia("Palestra", -1), uploadId = (await FotoEnviadaAsync(ana, enviar: false)).Id },
            "fotoDeOutro" => new { item = "P01", eventoDigitado = Dia("Palestra", -1), uploadId = (await FotoEnviadaAsync(bruno)).Id },
            _ => throw new ArgumentOutOfRangeException(nameof(caso)),
        };

        await AssertRecusaAsync(await ReivindicarAsync(ana, pedido), status, codigo);
        Assert.Empty(await mundo.Social.LinhasAsync<PremioParticipacao>());
        Assert.Empty(await mundo.Social.LinhasAsync<Post>());
        Assert.DoesNotContain(await mundo.Social.LinhasAsync<Evento>(), evento => evento.Origem == OrigemDoEvento.TextoLivre);
    }

    [Fact]
    public async Task So_aluno_ativo_reivindica()
    {
        await using var mundo = await MundoAsync();
        using var renata = await mundo.Social.ComoAsync(Renata);
        using var carla = await mundo.Social.ComoAsync(Carla);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        object Pedido(Guid upload) => new { item = "P06", eventoId = mundo.Semana, uploadId = upload };

        Assert.Equal(HttpStatusCode.Forbidden, (await ReivindicarAsync(renata, Pedido(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ReivindicarAsync(coordenacao, Pedido(Guid.NewGuid()))).StatusCode);
        await AssertRecusaAsync(await ReivindicarAsync(carla, Pedido(Guid.NewGuid())), HttpStatusCode.Conflict, "ex-aluno");
    }

    [Fact]
    public async Task O_post_pendente_nao_sai_nem_pelo_autor_nem_pela_instituicao()
    {
        await using var mundo = await MundoAsync();
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var bruno = await mundo.Social.ComoAsync(Bruno);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var resposta = await ReivindicarAsync(ana, new { item = "P06", eventoId = mundo.Semana, uploadId = (await FotoEnviadaAsync(ana)).Id });
        var post = (await resposta.Content.ReadFromJsonAsync<PostResponse>(Ct))!;
        var caminho = new Uri($"/api/v1/posts/{post.Id}", UriKind.Relative);

        await AssertRecusaAsync(await ana.DeleteAsync(caminho, Ct), HttpStatusCode.Conflict, "comprovacao-em-andamento");
        await AssertRecusaAsync(await coordenacao.DeleteAsync(caminho, Ct), HttpStatusCode.Conflict, "comprovacao-em-andamento");
        await AssertRecusaAsync(await bruno.DeleteAsync(caminho, Ct), HttpStatusCode.Forbidden, "remocao-nao-permitida");

        var gravado = Assert.Single(await mundo.Social.LinhasAsync<Post>());
        Assert.Null(gravado.RemovidoEm);
        await mundo.Social.EsvaziarFilaAsync();
        foreach (var chave in gravado.ChavesDaFoto)
        {
            Assert.True(await mundo.Social.Bucket.ExisteAsync(chave, Ct));
        }
    }

    private static Task<HttpResponseMessage> ReivindicarAsync(HttpClient client, object pedido) =>
        client.PostAsJsonAsync(new Uri("/api/v1/reivindicacoes", UriKind.Relative), pedido, Ct);

    private Task<MundoDePremios> MundoAsync() => MundoDePremios.CriarAsync(postgres, armazenamento);
}
