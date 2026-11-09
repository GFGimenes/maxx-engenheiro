using System.Net;
using System.Net.Http.Json;
using Maxx.Api.Instituicao;
using Maxx.Api.IntegrationTests.Fotos;
using Maxx.Api.IntegrationTests.PremiosParticipacao;
using Maxx.Api.Notificacoes;
using Maxx.Api.Posts;
using Maxx.Api.PremiosParticipacao;
using Maxx.Application.Conquistas;
using Maxx.Application.Medalhas;
using Maxx.Application.PremiosParticipacao;
using Maxx.Application.Ranking;
using Maxx.Domain.Academico;
using Maxx.Domain.Premios;
using Maxx.Domain.Ranking;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Ranking;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.Social.MundoSocial;

namespace Maxx.Api.IntegrationTests.Instituicao;

// A concessão direta de prêmio de participação e a anulação de medalha pela área da
// instituição (passo 12, etapa 2). A concessão nasce válida, sem comprovação, com a
// notificação, os recálculos e o fim da janela na mesma transação; o instante dela é o do
// desempate. A anulação é o mesmo ato do comando administrativo. Postgres e bucket reais.
public sealed class ConcessaoEAnulacaoTests(PostgresFixture postgres, ArmazenamentoFixture armazenamento)
    : IClassFixture<PostgresFixture>, IClassFixture<ArmazenamentoFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Concessao_nasce_valida_sem_comprovacao_e_enfileira_notificacao_recalculo_e_fim_da_janela()
    {
        await using var mundo = await MundoDePremios.CriarAsync(postgres, armazenamento);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var agora = mundo.Social.Relogio.GetUtcNow();

        var resposta = await ConcederAsync(coordenacao, await CadastroAsync(mundo, Ana), new { item = "P06", eventoId = mundo.Semana });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var concedido = (await resposta.Content.ReadFromJsonAsync<PremioConcedidoResponse>(Ct))!;
        Assert.Equal((mundo.Social.Id(Ana), "P06", "bronze", agora), (concedido.AlunoId, concedido.Item, concedido.Tier, concedido.ConcedidoEm));
        var premio = await mundo.PremioAsync(concedido.Id);
        Assert.Equal(EstadoDoPremioParticipacao.Valido, premio.Estado);
        Assert.Equal((mundo.Social.Id(Coordenacao), agora), (premio.AprovadoPorId, premio.AprovadoEm));
        Assert.Empty(await mundo.Social.LinhasAsync<Comprovacao>());

        var trabalhos = await mundo.Social.LinhasAsync<Trabalho>();
        Assert.Single(trabalhos, trabalho => trabalho.Tipo == NotificarPremioConcedido.Tipo);
        Assert.Single(trabalhos, trabalho => trabalho.Tipo == RecalcularRanking.Tipo);
        Assert.Single(trabalhos, trabalho => trabalho.Tipo == RecalcularRaridade.Tipo);
        Assert.Equal(
            agora + PremioParticipacao.JanelaDeContestacao,
            Assert.Single(trabalhos, trabalho => trabalho.Tipo == TornarPremioDefinitivo.Tipo).DisponivelEm);

        // A notificação chega pelo worker, com o nome do item para a frase.
        await mundo.Social.EsvaziarFilaAsync();
        using var ana = await mundo.Social.ComoAsync(Ana);
        var notificacoes = (await ana.GetFromJsonAsync<PaginaDeNotificacoesResponse>(new Uri("/api/v1/eu/notificacoes", UriKind.Relative), Ct))!;
        var notificacao = Assert.Single(notificacoes.Itens);
        Assert.Equal(("premioConcedido", premio.Id), (notificacao.Tipo, notificacao.ReferenciaId));
        Assert.NotNull(notificacao.Item);
    }

    [Fact]
    public async Task O_instante_da_concessao_e_o_do_desempate_no_ranking()
    {
        await using var mundo = await MundoDePremios.CriarAsync(postgres, armazenamento);
        // O login vem antes de o relógio andar: o token é validado no relógio de verdade.
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        await mundo.Social.MedalhaAsync(Bruno);
        var daMedalha = mundo.Social.Relogio.GetUtcNow();
        await mundo.Social.EsvaziarFilaAsync();
        mundo.Social.Relogio.Avancar(TimeSpan.FromHours(1));

        Assert.Equal(
            HttpStatusCode.Created,
            (await ConcederAsync(coordenacao, await CadastroAsync(mundo, Ana), new { item = "P06", eventoId = mundo.Semana })).StatusCode);
        await mundo.Social.EsvaziarFilaAsync();

        var historico = (await mundo.Social.LinhasAsync<PosicaoNoRankingRow>())
            .Where(linha => linha.Escopo == EscopoDoRanking.Geral && linha.Periodo == PeriodoDoRanking.Historico)
            .OrderBy(linha => linha.Posicao)
            .Select(linha => (linha.AlunoId, linha.Pontos, linha.UltimaConcessaoEm))
            .ToList();
        Assert.Equal(
            [(mundo.Social.Id(Bruno), 30, daMedalha), (mundo.Social.Id(Ana), 30, daMedalha.AddHours(1))],
            historico);
    }

    [Fact]
    public async Task O_fim_da_janela_torna_definitivo_e_reavalia_as_conquistas()
    {
        await using var mundo = await MundoDePremios.CriarAsync(postgres, armazenamento);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var resposta = await ConcederAsync(coordenacao, await CadastroAsync(mundo, Ana), new { item = "P06", eventoId = mundo.Semana });
        var id = (await resposta.Content.ReadFromJsonAsync<PremioConcedidoResponse>(Ct))!.Id;
        await mundo.Social.EsvaziarFilaAsync();

        mundo.Social.Relogio.Avancar(PremioParticipacao.JanelaDeContestacao);
        var consumidor = mundo.Social.Factory.Services.GetRequiredService<ConsumidorDeTrabalhos>();
        // Os tipos que passaram pela fila enquanto ela esvaziava.
        var tipos = new HashSet<string>();
        do
        {
            tipos.UnionWith((await mundo.Social.LinhasAsync<Trabalho>()).Select(trabalho => trabalho.Tipo));
        }
        while (await consumidor.ProcessarProximoAsync(Ct));

        Assert.Equal(EstadoDoPremioParticipacao.Definitivo, (await mundo.PremioAsync(id)).Estado);
        Assert.Contains(ReavaliarConquistas.Tipo, tipos);
        Assert.DoesNotContain(await mundo.Social.LinhasAsync<Trabalho>(), trabalho => trabalho.Estado == EstadoDoTrabalho.Falho);
    }

    [Fact]
    public async Task Concessao_repetida_e_recusada_tambem_na_corrida_e_sem_pedir_outro_recalculo()
    {
        await using var mundo = await MundoDePremios.CriarAsync(postgres, armazenamento);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var ana = await CadastroAsync(mundo, Ana);
        var bruno = await CadastroAsync(mundo, Bruno);

        Assert.Equal(HttpStatusCode.Created, (await ConcederAsync(coordenacao, ana, new { item = "P06", eventoId = mundo.Semana })).StatusCode);
        await AssertRecusaAsync(
            await ConcederAsync(coordenacao, ana, new { item = "P06", eventoId = mundo.Semana }), HttpStatusCode.Conflict, "premio-ja-reivindicado");

        var corrida = await Task.WhenAll(
            ConcederAsync(coordenacao, bruno, new { item = "P06", eventoId = mundo.Semana }),
            ConcederAsync(coordenacao, bruno, new { item = "P06", eventoId = mundo.Semana }));
        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], corrida.Select(resposta => resposta.StatusCode).Order());
        Assert.Equal(2, (await mundo.Social.LinhasAsync<Trabalho>()).Count(trabalho => trabalho.Tipo == NotificarPremioConcedido.Tipo));
    }

    [Fact]
    public async Task Concessao_recusa_evento_de_outro_curso_ex_aluno_aluno_sem_conta_e_quem_nao_e_da_instituicao()
    {
        await using var mundo = await MundoDePremios.CriarAsync(postgres, armazenamento);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        using var renata = await mundo.Social.ComoAsync(Renata);
        var ana = await CadastroAsync(mundo, Ana);
        var semConta = CadastroElegivel.Aluno("20260199", "sem.conta@al.unieduk.com.br", "Sem Conta", Assert.Single(await mundo.Social.LinhasAsync<Curso>(), curso => curso.Sigla == "CC"), 1);
        await BancoDeTeste.SalvarAsync(mundo.Social.Factory, semConta);

        await AssertRecusaAsync(
            await ConcederAsync(coordenacao, ana, new { item = "P06", eventoId = mundo.SemanaDeAdm }), HttpStatusCode.NotFound, "evento-nao-encontrado");
        await AssertRecusaAsync(
            await ConcederAsync(coordenacao, await CadastroAsync(mundo, Carla), new { item = "P06", eventoId = mundo.Semana }), HttpStatusCode.Conflict, "ex-aluno");
        await AssertRecusaAsync(
            await ConcederAsync(coordenacao, semConta.Id, new { item = "P06", eventoId = mundo.Semana }), HttpStatusCode.Conflict, "aluno-sem-conta");
        await AssertRecusaAsync(
            await ConcederAsync(coordenacao, ana, new { item = "M01", eventoId = mundo.Semana }), HttpStatusCode.NotFound, "item-nao-encontrado");
        Assert.Equal(HttpStatusCode.Forbidden, (await ConcederAsync(renata, ana, new { item = "P06", eventoId = mundo.Semana })).StatusCode);

        Assert.Empty(await mundo.Social.LinhasAsync<PremioParticipacao>());
        Assert.Empty(await mundo.Social.LinhasAsync<Trabalho>());
    }

    [Fact]
    public async Task Concessao_com_evento_digitado_grava_o_evento_como_da_instituicao_e_a_lista_oferece_eventos_e_materias()
    {
        await using var mundo = await MundoDePremios.CriarAsync(postgres, armazenamento);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var ana = await CadastroAsync(mundo, Ana);

        var eventos = (await coordenacao.GetFromJsonAsync<List<EventoResponse>>(Rota($"/alunos/{ana}/eventos?item=P06"), Ct))!;
        Assert.Equal([mundo.Semana], eventos.Select(evento => evento.Id));
        var materias = (await coordenacao.GetFromJsonAsync<List<MateriaResponse>>(Rota($"/alunos/{ana}/materias"), Ct))!;
        Assert.Equal(["Banco de Dados"], materias.Select(materia => materia.Nome));

        var resposta = await ConcederAsync(
            coordenacao,
            ana,
            new { item = "P01", eventoDigitado = new { nome = "Monitoria de Banco de Dados", inicio = mundo.Hoje.AddDays(-3), fim = mundo.Hoje } });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var digitado = Assert.Single(await mundo.Social.LinhasAsync<Evento>(), evento => evento.Origem == OrigemDoEvento.TextoLivre);
        Assert.Equal(mundo.Social.Id(Coordenacao), digitado.CriadoPorId);
    }

    [Fact]
    public async Task O_aluno_posta_o_premio_concedido_uma_vez_e_nao_posta_o_que_esta_em_averiguacao()
    {
        await using var mundo = await MundoDePremios.CriarAsync(postgres, armazenamento);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        using var ana = await mundo.Social.ComoAsync(Ana);
        var cadastro = await CadastroAsync(mundo, Ana);
        var postado = await ConcedidoAsync(coordenacao, cadastro, new { item = "P06", eventoId = mundo.Semana });
        var contestado = await ConcedidoAsync(
            coordenacao, cadastro, new { item = "P01", eventoDigitado = new { nome = "Palestra de LGPD", inicio = mundo.Hoje, fim = mundo.Hoje } });
        Assert.Equal(
            HttpStatusCode.Created,
            (await coordenacao.PostAsJsonAsync(
                new Uri($"/api/v1/premios-participacao/{contestado}/contestacao", UriKind.Relative), new { motivo = "Conferir a presença." }, Ct)).StatusCode);

        var resposta = await PostarAsync(ana, postado);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var post = (await resposta.Content.ReadFromJsonAsync<PostResponse>(Ct))!;
        Assert.Equal((postado, "P06", "valido"), (post.Premio!.Id, post.Premio.Item, post.Premio.Estado));

        await AssertRecusaAsync(await PostarAsync(ana, postado), HttpStatusCode.Conflict, "premio-ja-postado");
        await AssertRecusaAsync(await PostarAsync(ana, contestado), HttpStatusCode.Conflict, "premio-indisponivel");
        using var bruno = await mundo.Social.ComoAsync(Bruno);
        await AssertRecusaAsync(await PostarAsync(bruno, postado), HttpStatusCode.NotFound, "premio-nao-encontrado");
    }

    [Fact]
    public async Task Anulacao_pela_area_e_o_ato_do_comando_com_quem_anulou_e_os_mesmos_recalculos()
    {
        await using var mundo = await MundoDePremios.CriarAsync(postgres, armazenamento);
        var medalha = await mundo.Social.MedalhaAsync(Ana);
        await mundo.Social.EsvaziarFilaAsync();
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        using var renata = await mundo.Social.ComoAsync(Renata);

        Assert.Equal(HttpStatusCode.Forbidden, (await AnularAsync(renata, medalha)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await AnularAsync(coordenacao, medalha)).StatusCode);

        var anulada = Assert.Single(await mundo.Social.LinhasAsync<Medalha>());
        Assert.Equal((mundo.Social.Id(Coordenacao), mundo.Social.Relogio.GetUtcNow()), (anulada.AnuladaPorId, anulada.AnuladaEm));
        var trabalhos = await mundo.Social.LinhasAsync<Trabalho>();
        Assert.Single(trabalhos, trabalho => trabalho.Tipo == RecalcularConquistasDaMedalha.Tipo);
        Assert.Single(trabalhos, trabalho => trabalho.Tipo == RecalcularRanking.Tipo);

        // Anular de novo não muda nada e responde igual; a que não existe, 404.
        Assert.Equal(HttpStatusCode.NoContent, (await AnularAsync(coordenacao, medalha)).StatusCode);
        Assert.Single(await mundo.Social.LinhasAsync<Trabalho>(), trabalho => trabalho.Tipo == RecalcularConquistasDaMedalha.Tipo);
        await AssertRecusaAsync(await AnularAsync(coordenacao, Guid.CreateVersion7()), HttpStatusCode.NotFound, "medalha-nao-encontrada");
    }

    private static Uri Rota(string resto) => new($"/api/v1/instituicao{resto}", UriKind.Relative);

    private static Task<HttpResponseMessage> ConcederAsync(HttpClient client, Guid cadastro, object pedido) =>
        client.PostAsJsonAsync(Rota($"/alunos/{cadastro}/premios-participacao"), pedido, Ct);

    private static async Task<Guid> ConcedidoAsync(HttpClient client, Guid cadastro, object pedido)
    {
        var resposta = await ConcederAsync(client, cadastro, pedido);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<PremioConcedidoResponse>(Ct))!.Id;
    }

    private static Task<HttpResponseMessage> PostarAsync(HttpClient client, Guid premio) =>
        client.PostAsJsonAsync(new Uri("/api/v1/posts", UriKind.Relative), new { premioParticipacaoId = premio }, Ct);

    private static Task<HttpResponseMessage> AnularAsync(HttpClient client, Guid medalha) =>
        client.PostAsync(Rota($"/medalhas/{medalha}/anulacao"), null, Ct);

    private static async Task<Guid> CadastroAsync(MundoDePremios mundo, string email) =>
        Assert.Single(await mundo.Social.LinhasAsync<CadastroElegivel>(), linha => linha.Email == email).Id;
}
