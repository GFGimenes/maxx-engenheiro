using System.Net;
using System.Net.Http.Json;
using Maxx.Api.IntegrationTests.Fotos;
using Maxx.Api.Perfil;
using Maxx.Api.Posts;
using Maxx.Api.PremiosParticipacao;
using Maxx.Domain.Notificacoes;
using Maxx.Domain.Premios;
using Maxx.Domain.Social;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.RaridadeDosItens;
using static Maxx.Api.IntegrationTests.PremiosParticipacao.MundoDePremios;
using static Maxx.Api.IntegrationTests.Social.MundoSocial;

namespace Maxx.Api.IntegrationTests.PremiosParticipacao;

// Contestação, decisão da instituição, histórico do aluno e remoção do post pela instituição
// (fluxos/04-contestacao.md), contra o Postgres e o bucket reais.
public sealed class ContestacoesTests(PostgresFixture postgres, ArmazenamentoFixture armazenamento)
    : IClassFixture<PostgresFixture>, IClassFixture<ArmazenamentoFixture>
{
    private static readonly Uri _fila = new("/api/v1/contestacoes", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Contestado_vai_a_averiguacao_para_de_pontuar_e_some_para_quem_nao_e_o_aluno_nem_a_instituicao()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var bruno = await mundo.Social.ComoAsync(Bruno);
        using var renata = await mundo.Social.ComoAsync(Renata);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var post = await AprovadoAsync(mundo, ana, coordenacao);

        var resposta = await ContestarAsync(renata, post.Premio!.Id, "  A foto é de outro evento.  ");

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var criada = (await resposta.Content.ReadFromJsonAsync<ContestacaoCriadaResponse>(Ct))!;
        var contestacao = Assert.Single(await mundo.Social.LinhasAsync<Contestacao>());
        Assert.Equal(
            (criada.Id, post.Premio.Id, mundo.Social.Id(Renata), "A foto é de outro evento.", (DecisaoDaContestacao?)null),
            (contestacao.Id, contestacao.PremioParticipacaoId, contestacao.ContestadaPorId, contestacao.Motivo, contestacao.Decisao));
        Assert.Equal(EstadoDoPremioParticipacao.EmAveriguacao, (await mundo.PremioAsync(post.Premio.Id)).Estado);
        Assert.Equal(0, await PontosAsync(ana));

        // O post segue no ar. A marca é do aluno e da instituição; para os outros, sem o prêmio.
        Assert.Equal("emAveriguacao", (await PostAsync(ana, post.Id)).Premio!.Estado);
        Assert.Equal("emAveriguacao", (await PostAsync(coordenacao, post.Id)).Premio!.Estado);
        var paraOBruno = await PostAsync(bruno, post.Id);
        Assert.Null(paraOBruno.Premio);
        Assert.NotNull(paraOBruno.Foto);
        Assert.Null((await PostAsync(renata, post.Id)).Premio);

        await mundo.Social.EsvaziarFilaAsync();
        Assert.Contains(await mundo.Social.LinhasAsync<Notificacao>(), notificacao => notificacao.Tipo == TipoDeNotificacao.ContestacaoAberta);
        Assert.Equal(0, Assert.Single(await mundo.Social.LinhasAsync<RaridadeDoItemRow>(), linha => linha.ItemCodigo == "P06").AlunosComItem);
    }

    [Fact]
    public async Task Uma_contestacao_por_premio_tambem_na_corrida()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var renata = await mundo.Social.ComoAsync(Renata);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var premio = (await AprovadoAsync(mundo, ana, coordenacao)).Premio!.Id;

        var respostas = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
            ContestarAsync(i % 2 == 0 ? renata : coordenacao, premio, $"Motivo {i}.")));

        Assert.Single(respostas, resposta => resposta.StatusCode == HttpStatusCode.Created);
        foreach (var recusada in respostas.Where(resposta => resposta.StatusCode != HttpStatusCode.Created))
        {
            await AssertRecusaAsync(recusada, HttpStatusCode.Conflict, "premio-ja-contestado");
        }

        Assert.Single(await mundo.Social.LinhasAsync<Contestacao>());
    }

    [Fact]
    public async Task Contestacao_recusada_fora_da_janela_sem_aprovacao_sem_motivo_e_fora_do_escopo()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var elisa = await mundo.Social.ComoAsync(Elisa);
        using var renata = await mundo.Social.ComoAsync(Renata);
        using var marcos = await mundo.Social.ComoAsync(Marcos);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var aprovado = (await AprovadoAsync(mundo, ana, coordenacao)).Premio!.Id;
        var pendente = (await NaSemanaAsync(elisa, mundo.Semana)).Premio!.Id;

        await AssertRecusaAsync(await ContestarAsync(renata, aprovado, "  "), HttpStatusCode.BadRequest, "motivo-invalido");
        await AssertRecusaAsync(await ContestarAsync(coordenacao, pendente, "Pendente."), HttpStatusCode.Conflict, "premio-nao-contestavel");
        await AssertRecusaAsync(await ContestarAsync(marcos, aprovado, "Não é meu aluno."), HttpStatusCode.Forbidden, "fora-do-escopo");
        await AssertRecusaAsync(await ContestarAsync(renata, Guid.NewGuid(), "Inexistente."), HttpStatusCode.NotFound, "premio-nao-encontrado");
        Assert.Equal(HttpStatusCode.Forbidden, (await ContestarAsync(ana, aprovado, "Eu mesma.")).StatusCode);

        mundo.Social.Relogio.Avancar(TimeSpan.FromDays(15));
        await AssertRecusaAsync(await ContestarAsync(renata, aprovado, "Tarde."), HttpStatusCode.Conflict, "fora-da-janela-de-contestacao");
        Assert.Empty(await mundo.Social.LinhasAsync<Contestacao>());
    }

    [Fact]
    public async Task A_fila_de_contestacoes_e_da_instituicao_das_mais_antigas_para_as_mais_novas_com_a_marca_de_7_dias()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var bruno = await mundo.Social.ComoAsync(Bruno);
        using var renata = await mundo.Social.ComoAsync(Renata);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var daAna = await AprovadoAsync(mundo, ana, coordenacao);
        var doBruno = await AprovadoAsync(mundo, bruno, coordenacao);
        await ContestarAsync(renata, daAna.Premio!.Id, "Primeira.");
        mundo.Social.Relogio.Avancar(TimeSpan.FromMinutes(1));
        await ContestarAsync(coordenacao, doBruno.Premio!.Id, "Segunda.");

        var fila = await FilaAsync(coordenacao);

        Assert.Equal([daAna.Premio.Id, doBruno.Premio.Id], fila.Itens.Select(item => item.PremioId));
        var primeira = fila.Itens[0];
        Assert.Equal(("Primeira.", "@renataA-PROF", (Guid?)daAna.Id, "P06", false), (primeira.Motivo, primeira.ContestadaPor, primeira.PostId, primeira.Item, primeira.PassouDoPrazo));
        Assert.Equal(daAna.Foto, primeira.Foto);
        Assert.Equal(HttpStatusCode.Forbidden, (await renata.GetAsync(_fila, Ct)).StatusCode);

        mundo.Social.Relogio.Avancar(TimeSpan.FromDays(7));
        Assert.Equal([true, false], (await FilaAsync(coordenacao)).Itens.Select(item => item.PassouDoPrazo));
    }

    [Fact]
    public async Task Confirmado_fica_definitivo_na_hora_volta_a_pontuar_e_nao_pode_mais_ser_contestado()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var renata = await mundo.Social.ComoAsync(Renata);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var post = await AprovadoAsync(mundo, ana, coordenacao);
        var contestacao = await ContestacaoAsync(renata, post.Premio!.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await ConfirmarAsync(coordenacao, contestacao)).StatusCode);

        var premio = await mundo.PremioAsync(post.Premio.Id);
        Assert.Equal((EstadoDoPremioParticipacao.Definitivo, mundo.Social.Relogio.GetUtcNow()), (premio.Estado, premio.DefinitivoEm!.Value));
        var decidida = Assert.Single(await mundo.Social.LinhasAsync<Contestacao>());
        Assert.Equal((DecisaoDaContestacao.Confirmada, mundo.Social.Id(Coordenacao)), (decidida.Decisao!.Value, decidida.DecididaPorId!.Value));
        Assert.Equal(30, await PontosAsync(ana));
        Assert.Empty((await FilaAsync(coordenacao)).Itens);
        await AssertRecusaAsync(await ConfirmarAsync(coordenacao, contestacao), HttpStatusCode.Conflict, "contestacao-ja-decidida");
        await AssertRecusaAsync(await ContestarAsync(coordenacao, post.Premio.Id, "De novo."), HttpStatusCode.Conflict, "premio-ja-contestado");

        // O trabalho do fim da janela, que ainda estava agendado, não muda nada.
        mundo.Social.Relogio.Avancar(TimeSpan.FromDays(15));
        await mundo.Social.EsvaziarFilaAsync();
        Assert.Equal(EstadoDoPremioParticipacao.Definitivo, (await mundo.PremioAsync(post.Premio.Id)).Estado);
        Assert.DoesNotContain(await mundo.Social.LinhasAsync<Trabalho>(), trabalho => trabalho.Estado == EstadoDoTrabalho.Falho);
        Assert.Contains(await mundo.Social.LinhasAsync<Notificacao>(), notificacao => notificacao.Tipo == TipoDeNotificacao.DecisaoDeContestacao);
    }

    [Fact]
    public async Task Revogado_sai_da_pontuacao_fica_no_historico_com_o_motivo_e_bloqueia_o_mesmo_evento()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var renata = await mundo.Social.ComoAsync(Renata);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var post = await AprovadoAsync(mundo, ana, coordenacao);
        var contestacao = await ContestacaoAsync(renata, post.Premio!.Id);

        await AssertRecusaAsync(await RevogarAsync(coordenacao, contestacao, ""), HttpStatusCode.BadRequest, "motivo-invalido");
        Assert.Equal(HttpStatusCode.NoContent, (await RevogarAsync(coordenacao, contestacao, " Foto de outro evento. ")).StatusCode);

        var premio = await mundo.PremioAsync(post.Premio.Id);
        Assert.Equal((EstadoDoPremioParticipacao.Revogado, "Foto de outro evento."), (premio.Estado, premio.MotivoDaRevogacao));
        Assert.Equal(DecisaoDaContestacao.Revogada, Assert.Single(await mundo.Social.LinhasAsync<Contestacao>()).Decisao);
        Assert.Equal(0, await PontosAsync(ana));
        Assert.Null((await PostAsync(ana, post.Id)).Premio);
        await AssertRecusaAsync(await ConfirmarAsync(coordenacao, contestacao), HttpStatusCode.Conflict, "contestacao-ja-decidida");

        // Revogado segura a chave (D5): o mesmo evento não volta.
        var upload = await FotoEnviadaAsync(ana);
        await AssertRecusaAsync(
            await ReivindicarAsync(ana, new { item = "P06", eventoId = mundo.Semana, uploadId = upload.Id }), HttpStatusCode.Conflict, "premio-ja-reivindicado");

        // O post, já comum, sai pelo autor.
        Assert.Equal(HttpStatusCode.NoContent, (await ana.DeleteAsync(new Uri($"/api/v1/posts/{post.Id}", UriKind.Relative), Ct)).StatusCode);
    }

    [Fact]
    public async Task Duas_decisoes_ao_mesmo_tempo_valem_uma_so()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var post = await AprovadoAsync(mundo, ana, coordenacao);
        var contestacao = await ContestacaoAsync(coordenacao, post.Premio!.Id);

        var respostas = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
            i % 2 == 0 ? ConfirmarAsync(coordenacao, contestacao) : RevogarAsync(coordenacao, contestacao, "Outro evento.")));

        Assert.Single(respostas, resposta => resposta.StatusCode == HttpStatusCode.NoContent);
        foreach (var recusada in respostas.Where(resposta => resposta.StatusCode != HttpStatusCode.NoContent))
        {
            await AssertRecusaAsync(recusada, HttpStatusCode.Conflict, "contestacao-ja-decidida");
        }
    }

    [Fact]
    public async Task So_a_instituicao_decide_e_contestacao_inexistente_e_404()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var renata = await mundo.Social.ComoAsync(Renata);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var contestacao = await ContestacaoAsync(renata, (await AprovadoAsync(mundo, ana, coordenacao)).Premio!.Id);

        Assert.Equal(HttpStatusCode.Forbidden, (await ConfirmarAsync(renata, contestacao)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await RevogarAsync(ana, contestacao, "Não.")).StatusCode);
        await AssertRecusaAsync(await ConfirmarAsync(coordenacao, Guid.NewGuid()), HttpStatusCode.NotFound, "contestacao-nao-encontrada");
    }

    [Fact]
    public async Task O_aluno_ve_suas_reivindicacoes_pendente_em_averiguacao_reprovada_e_revogada_com_o_motivo()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var bruno = await mundo.Social.ComoAsync(Bruno);
        using var renata = await mundo.Social.ComoAsync(Renata);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        object Digitado(string item, int diasAtras) => new
        {
            item,
            eventoDigitado = new { nome = $"Palestra {diasAtras}", inicio = mundo.Hoje.AddDays(-diasAtras), fim = mundo.Hoje.AddDays(-diasAtras) },
        };

        // Válido (não aparece), em averiguação, reprovado, revogado e pendente, nessa ordem.
        await AprovadoAsync(mundo, ana, coordenacao);
        var averiguacao = await ReivindicadoAsync(ana, Digitado("P01", 1));
        await AprovarAsync(coordenacao, (await mundo.ComprovacaoAsync(averiguacao)).Id);
        await ContestarAsync(renata, averiguacao, "Revi.");
        mundo.Social.Relogio.Avancar(TimeSpan.FromMinutes(1));
        var reprovado = await ReivindicadoAsync(ana, Digitado("P01", 2));
        await ReprovarAsync(coordenacao, (await mundo.ComprovacaoAsync(reprovado)).Id, "Foto ilegível.");
        mundo.Social.Relogio.Avancar(TimeSpan.FromMinutes(1));
        var revogado = await ReivindicadoAsync(ana, Digitado("P01", 3));
        await AprovarAsync(coordenacao, (await mundo.ComprovacaoAsync(revogado)).Id);
        await RevogarAsync(coordenacao, await ContestacaoAsync(renata, revogado), "Outro evento.");
        mundo.Social.Relogio.Avancar(TimeSpan.FromMinutes(1));
        var pendente = await ReivindicadoAsync(ana, Digitado("P01", 4));
        await NaSemanaAsync(bruno, mundo.Semana);

        var lista = (await ana.GetFromJsonAsync<List<ReivindicacaoResponse>>(new Uri("/api/v1/eu/reivindicacoes", UriKind.Relative), Ct))!;

        Assert.Equal(
            [(pendente, "reivindicado", (string?)null), (revogado, "revogado", "Outro evento."), (reprovado, "reprovado", "Foto ilegível."), (averiguacao, "emAveriguacao", null)],
            lista.Select(item => (item.PremioId, item.Estado, item.Motivo)));
        Assert.Equal(("P01", "Palestra 4", 10), (lista[0].Item, lista[0].Evento, lista[0].Pontos));
        Assert.Equal(HttpStatusCode.Forbidden, (await renata.GetAsync(new Uri("/api/v1/eu/reivindicacoes", UriKind.Relative), Ct)).StatusCode);
    }

    public static TheoryData<string, EstadoDoPremioParticipacao, TipoDeNotificacao> Remocoes => new()
    {
        { "pendente", EstadoDoPremioParticipacao.Reprovado, TipoDeNotificacao.ComprovacaoReprovada },
        { "valido", EstadoDoPremioParticipacao.Revogado, TipoDeNotificacao.PremioRevogado },
        { "emAveriguacao", EstadoDoPremioParticipacao.Revogado, TipoDeNotificacao.DecisaoDeContestacao },
    };

    [Theory]
    [MemberData(nameof(Remocoes))]
    public async Task A_instituicao_remove_o_post_e_resolve_o_premio_no_mesmo_ato(
        string estado, EstadoDoPremioParticipacao esperado, TipoDeNotificacao notificacao)
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var renata = await mundo.Social.ComoAsync(Renata);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var post = estado == "pendente" ? await NaSemanaAsync(ana, mundo.Semana) : await AprovadoAsync(mundo, ana, coordenacao);
        if (estado == "emAveriguacao")
        {
            await ContestarAsync(renata, post.Premio!.Id, "Revi.");
        }

        var resposta = await coordenacao.DeleteAsync(new Uri($"/api/v1/posts/{post.Id}", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        var premio = await mundo.PremioAsync(post.Premio!.Id);
        Assert.Equal(esperado, premio.Estado);
        Assert.Equal("Removido pela instituição.", premio.MotivoDaReprovacao ?? premio.MotivoDaRevogacao);
        var removido = Assert.Single(await mundo.Social.LinhasAsync<Post>());
        Assert.Equal((mundo.Social.Id(Coordenacao), (Guid?)null), (removido.RemovidoPorId!.Value, removido.PremioParticipacaoId));
        Assert.Equal(0, await PontosAsync(ana));
        if (estado == "emAveriguacao")
        {
            Assert.Equal(DecisaoDaContestacao.Revogada, Assert.Single(await mundo.Social.LinhasAsync<Contestacao>()).Decisao);
        }

        // A foto sai do bucket pela fila, e o aluno é avisado.
        await mundo.Social.EsvaziarFilaAsync();
        foreach (var chave in removido.ChavesDaFoto)
        {
            Assert.False(await mundo.Social.Bucket.ExisteAsync(chave, Ct));
        }

        Assert.Contains(await mundo.Social.LinhasAsync<Notificacao>(), linha => linha.Tipo == notificacao && linha.ReferenciaId == premio.Id);
        Assert.DoesNotContain(await mundo.Social.LinhasAsync<Trabalho>(), trabalho => trabalho.Estado == EstadoDoTrabalho.Falho);
    }

    // Reivindicação da semana acadêmica aprovada pela instituição; devolve o post lido de novo.
    private static async Task<PostResponse> AprovadoAsync(MundoDePremios mundo, HttpClient aluno, HttpClient instituicao)
    {
        var post = await NaSemanaAsync(aluno, mundo.Semana);
        Assert.Equal(HttpStatusCode.NoContent, (await AprovarAsync(instituicao, (await mundo.ComprovacaoAsync(post.Premio!.Id)).Id)).StatusCode);
        return await PostAsync(aluno, post.Id);
    }

    private static async Task<Guid> ReivindicadoAsync(HttpClient aluno, object pedido)
    {
        var upload = await FotoEnviadaAsync(aluno);
        var corpo = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(pedido))!.AsObject();
        corpo["uploadId"] = upload.Id;
        var resposta = await aluno.PostAsJsonAsync(new Uri("/api/v1/reivindicacoes", UriKind.Relative), corpo, Ct);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<PostResponse>(Ct))!.Premio!.Id;
    }

    private static async Task<Guid> ContestacaoAsync(HttpClient client, Guid premio)
    {
        var resposta = await ContestarAsync(client, premio, "Revi a foto.");
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<ContestacaoCriadaResponse>(Ct))!.Id;
    }

    private static async Task<PostResponse> PostAsync(HttpClient client, Guid post) =>
        (await client.GetFromJsonAsync<PostResponse>(new Uri($"/api/v1/posts/{post}", UriKind.Relative), Ct))!;

    private static async Task<int?> PontosAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<PerfilResponse>(new Uri("/api/v1/eu/perfil", UriKind.Relative), Ct))!.Pontos;

    private static async Task<PaginaDeContestacoesResponse> FilaAsync(HttpClient client)
    {
        var resposta = await client.GetAsync(_fila, Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<PaginaDeContestacoesResponse>(Ct))!;
    }

    private static Task<HttpResponseMessage> ContestarAsync(HttpClient client, Guid premio, string? motivo) =>
        client.PostAsJsonAsync(new Uri($"/api/v1/premios-participacao/{premio}/contestacao", UriKind.Relative), new { motivo }, Ct);

    private static Task<HttpResponseMessage> AprovarAsync(HttpClient client, Guid comprovacao) =>
        client.PostAsync(new Uri($"/api/v1/comprovacoes/{comprovacao}/aprovacao", UriKind.Relative), null, Ct);

    private static Task<HttpResponseMessage> ReprovarAsync(HttpClient client, Guid comprovacao, string motivo) =>
        client.PostAsJsonAsync(new Uri($"/api/v1/comprovacoes/{comprovacao}/reprovacao", UriKind.Relative), new { motivo }, Ct);

    private static Task<HttpResponseMessage> ConfirmarAsync(HttpClient client, Guid contestacao) =>
        client.PostAsync(new Uri($"/api/v1/contestacoes/{contestacao}/confirmacao", UriKind.Relative), null, Ct);

    private static Task<HttpResponseMessage> RevogarAsync(HttpClient client, Guid contestacao, string motivo) =>
        client.PostAsJsonAsync(new Uri($"/api/v1/contestacoes/{contestacao}/revogacao", UriKind.Relative), new { motivo }, Ct);
}
