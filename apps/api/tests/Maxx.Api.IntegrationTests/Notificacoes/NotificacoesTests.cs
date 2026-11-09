using System.Net;
using System.Net.Http.Json;
using Maxx.Api.IntegrationTests.Fotos;
using Maxx.Api.IntegrationTests.PremiosParticipacao;
using Maxx.Api.Mural;
using Maxx.Api.Notificacoes;
using Maxx.Api.PremiosParticipacao;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Identidade;
using Maxx.Domain.Notificacoes;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.Social.MundoSocial;

namespace Maxx.Api.IntegrationTests.Notificacoes;

// O sininho, as bolinhas e o marcador de Troféus (produto.md 8; passo 10, perguntas 5 e 6),
// contra o Postgres e o bucket reais. As notificações nascem pelos fluxos de verdade (API e
// worker); só as de conquista são gravadas à mão, com a conquista, pelo custo de montar o
// catálogo de conquistas aqui.
public sealed class NotificacoesTests(PostgresFixture postgres, ArmazenamentoFixture armazenamento)
    : IClassFixture<PostgresFixture>, IClassFixture<ArmazenamentoFixture>
{
    private static readonly Uri _notificacoes = new("/api/v1/eu/notificacoes", UriKind.Relative);
    private static readonly Uri _leitura = new("/api/v1/eu/notificacoes/leitura", UriKind.Relative);
    private static readonly Uri _novidades = new("/api/v1/eu/novidades", UriKind.Relative);
    private static readonly Uri _trofeusVistos = new("/api/v1/eu/trofeus/visto", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly ItemConquista _c90 = new()
    {
        Codigo = "C90",
        Nome = "Uma presença no curso",
        Descricao = "Receber uma medalha de presença integral.",
        Tier = Tier.Bronze,
        Pontos = 5,
        Periodo = Periodo.Curso,
        Gatilho = Gatilho.PorFato,
        Regra = new() { Forma = FormaDeContagem.Contagem, Fonte = FonteDeContagem.Medalha, FiltroItens = ["M03"], Alvo = AlvoDeConquista.De(1) },
    };

    [Fact]
    public async Task O_sininho_traz_os_nove_tipos_do_mais_novo_ao_mais_antigo_com_o_que_cada_frase_pede()
    {
        await using var mundo = await MundoDePremios.CriarAsync(postgres, armazenamento);
        var social = mundo.Social;
        using var ana = await social.ComoAsync(Ana);
        using var renata = await social.ComoAsync(Renata);
        using var coordenacao = await social.ComoAsync(Coordenacao);

        // Medalha recebida.
        var medalha = await social.MedalhaAsync(Ana);
        await Passo(mundo);

        // Reprovada com motivo: o post perde o vínculo.
        var reprovado = (await MundoDePremios.NaSemanaAsync(ana, mundo.Semana)).Premio!.Id;
        await PostarAsync(coordenacao, $"/api/v1/comprovacoes/{(await mundo.ComprovacaoAsync(reprovado)).Id}/reprovacao", new { motivo = "Foto de outro evento." });
        await Passo(mundo);

        // Nova tentativa aprovada, contestada pela professora e confirmada pela instituição.
        var postConfirmado = await MundoDePremios.NaSemanaAsync(ana, mundo.Semana);
        var confirmado = postConfirmado.Premio!.Id;
        await PostarAsync(coordenacao, $"/api/v1/comprovacoes/{(await mundo.ComprovacaoAsync(confirmado)).Id}/aprovacao");
        await Passo(mundo);
        var contestacao = await PostarAsync(renata, $"/api/v1/premios-participacao/{confirmado}/contestacao", new { motivo = "Revi a foto." });
        var contestacaoId = (await contestacao.Content.ReadFromJsonAsync<ContestacaoCriadaResponse>(Ct))!.Id;
        await Passo(mundo);
        await PostarAsync(coordenacao, $"/api/v1/contestacoes/{contestacaoId}/confirmacao");
        await Passo(mundo);

        // Um terceiro, aprovado e revogado pela remoção do post pela instituição.
        var upload = await FotoEnviadaAsync(ana);
        var resposta = await ana.PostAsJsonAsync(
            new Uri("/api/v1/reivindicacoes", UriKind.Relative),
            new { item = "P01", eventoDigitado = new { nome = "Palestra de abertura", inicio = mundo.Hoje.AddDays(-1), fim = mundo.Hoje.AddDays(-1) }, uploadId = upload.Id },
            Ct);
        var postRevogado = (await resposta.Content.ReadFromJsonAsync<Maxx.Api.Posts.PostResponse>(Ct))!;
        await PostarAsync(coordenacao, $"/api/v1/comprovacoes/{(await mundo.ComprovacaoAsync(postRevogado.Premio!.Id)).Id}/aprovacao");
        await Passo(mundo);
        Assert.Equal(
            HttpStatusCode.NoContent, (await coordenacao.DeleteAsync(new Uri($"/api/v1/posts/{postRevogado.Id}", UriKind.Relative), Ct)).StatusCode);
        await Passo(mundo);

        // Conquista obtida e conquista removida.
        var (obtida, removida) = await ConquistasAsync(social, Ana);
        await Passo(mundo);

        // Aviso novo da faculdade.
        var aviso = await PostarAsync(coordenacao, "/api/v1/avisos", new { escopo = "faculdade", titulo = "Matrícula até 30/07", texto = "Renove pelo portal." });
        var avisoId = (await aviso.Content.ReadFromJsonAsync<AvisoPublicadoResponse>(Ct))!.Id;
        await Passo(mundo);

        var pagina = (await ana.GetFromJsonAsync<PaginaDeNotificacoesResponse>(_notificacoes, Ct))!;

        Assert.Equal(
            [
                "avisoNovo",
                "conquistaRemovida",
                "conquistaObtida",
                "premioRevogado",
                "comprovacaoAprovada",
                "decisaoDeContestacao",
                "contestacaoAberta",
                "comprovacaoAprovada",
                "comprovacaoReprovada",
                "medalhaRecebida",
            ],
            pagina.Itens.Select(notificacao => notificacao.Tipo));
        Assert.Null(pagina.Proximo);
        Assert.All(pagina.Itens, notificacao => Assert.False(notificacao.Lida));

        var porTipo = pagina.Itens.ToLookup(notificacao => notificacao.Tipo);
        Assert.Equal(Resumo(avisoId, tituloDoAviso: "Matrícula até 30/07"), Resumo(porTipo["avisoNovo"].Single()));
        Assert.Equal(Resumo(removida, item: "Uma presença no curso"), Resumo(porTipo["conquistaRemovida"].Single()));
        Assert.Equal(Resumo(obtida, item: "Uma presença no curso"), Resumo(porTipo["conquistaObtida"].Single()));
        Assert.Equal(
            Resumo(postRevogado.Premio.Id, item: postRevogado.Premio.Nome, motivo: "Removido pela instituição."),
            Resumo(porTipo["premioRevogado"].Single()));
        Assert.Equal(
            Resumo(confirmado, item: postConfirmado.Premio.Nome, decisao: "confirmada", postId: postConfirmado.Id),
            Resumo(porTipo["decisaoDeContestacao"].Single()));
        Assert.Equal(Resumo(confirmado, item: postConfirmado.Premio.Nome, postId: postConfirmado.Id), Resumo(porTipo["contestacaoAberta"].Single()));
        Assert.Equal(
            Resumo(reprovado, item: postConfirmado.Premio.Nome, motivo: "Foto de outro evento."),
            Resumo(porTipo["comprovacaoReprovada"].Single()));
        Assert.Equal(Resumo(medalha, item: "Nota 10 em prova"), Resumo(porTipo["medalhaRecebida"].Single()));
    }

    [Fact]
    public async Task Cada_um_ve_so_as_proprias_paginadas_de_20_em_20()
    {
        await using var mundo = await MundoDePremios.CriarAsync(postgres, armazenamento);
        var social = mundo.Social;
        using var ana = await social.ComoAsync(Ana);
        using var bruno = await social.ComoAsync(Bruno);
        await AvisosAsync(social, 25);

        var primeira = (await ana.GetFromJsonAsync<PaginaDeNotificacoesResponse>(_notificacoes, Ct))!;
        var segunda = (await ana.GetFromJsonAsync<PaginaDeNotificacoesResponse>(
            new Uri($"/api/v1/eu/notificacoes?cursor={primeira.Proximo}", UriKind.Relative), Ct))!;

        Assert.Equal(20, primeira.Itens.Count);
        Assert.Equal(5, segunda.Itens.Count);
        Assert.Null(segunda.Proximo);
        var daAna = primeira.Itens.Concat(segunda.Itens).Select(notificacao => notificacao.Id).ToList();
        Assert.Equal(25, daAna.Distinct().Count());

        var doBruno = (await bruno.GetFromJsonAsync<PaginaDeNotificacoesResponse>(_notificacoes, Ct))!.Itens.Select(notificacao => notificacao.Id);
        Assert.Empty(daAna.Intersect(doBruno));
        var linhas = await social.LinhasAsync<Notificacao>();
        Assert.All(daAna, id => Assert.Equal(social.Id(Ana), linhas.Single(linha => linha.Id == id).UsuarioId));

        await AssertRecusaAsync(
            await ana.GetAsync(new Uri("/api/v1/eu/notificacoes?cursor=xyz", UriKind.Relative), Ct), HttpStatusCode.BadRequest, "cursor-invalido");
    }

    [Fact]
    public async Task Abrir_o_sininho_marca_como_lidas_ate_a_mais_nova_mostrada()
    {
        await using var mundo = await MundoDePremios.CriarAsync(postgres, armazenamento);
        var social = mundo.Social;
        using var ana = await social.ComoAsync(Ana);
        await AvisosAsync(social, 2);
        var mostradas = (await ana.GetFromJsonAsync<PaginaDeNotificacoesResponse>(_notificacoes, Ct))!.Itens;

        // Chega outra entre a lista e a leitura: fica acesa.
        social.Relogio.Avancar(TimeSpan.FromSeconds(5));
        await AvisosAsync(social, 1);
        var leitura = await ana.PostAsJsonAsync(_leitura, new { ate = mostradas[0].CriadaEm }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, leitura.StatusCode);

        var depois = (await ana.GetFromJsonAsync<PaginaDeNotificacoesResponse>(_notificacoes, Ct))!.Itens;
        Assert.Equal([false, true, true], depois.Select(notificacao => notificacao.Lida));
        Assert.True((await NovidadesAsync(ana)).Sininho);

        // Sem corpo, todas as que existem agora.
        Assert.Equal(HttpStatusCode.NoContent, (await ana.PostAsync(_leitura, null, Ct)).StatusCode);
        Assert.All((await ana.GetFromJsonAsync<PaginaDeNotificacoesResponse>(_notificacoes, Ct))!.Itens, notificacao => Assert.True(notificacao.Lida));
        Assert.False((await NovidadesAsync(ana)).Sininho);

        // As do Bruno continuam acesas.
        using var bruno = await social.ComoAsync(Bruno);
        Assert.True((await NovidadesAsync(bruno)).Sininho);
    }

    [Fact]
    public async Task A_bolinha_do_mural_acende_com_aviso_nao_lido_e_apaga_ao_ler_todos()
    {
        await using var mundo = await MundoDePremios.CriarAsync(postgres, armazenamento);
        var social = mundo.Social;
        using var ana = await social.ComoAsync(Ana);
        Assert.Equal(new NovidadesResponse(false, false, false), await NovidadesAsync(ana));

        await AvisosAsync(social, 1);
        Assert.True((await NovidadesAsync(ana)).Mural);

        await ana.PostAsync(new Uri("/api/v1/mural/leitura", UriKind.Relative), null, Ct);
        var novidades = await NovidadesAsync(ana);
        Assert.False(novidades.Mural);

        // Ler o aviso apaga também a notificação dele: o sininho apaga junto.
        Assert.False(novidades.Sininho);
    }

    [Fact]
    public async Task A_bolinha_de_trofeus_acende_com_premio_novo_e_apaga_ao_abrir_a_aba()
    {
        await using var mundo = await MundoDePremios.CriarAsync(postgres, armazenamento);
        var social = mundo.Social;
        using var ana = await social.ComoAsync(Ana);
        using var renata = await social.ComoAsync(Renata);

        await social.MedalhaAsync(Ana);
        Assert.True((await NovidadesAsync(ana)).Trofeus);

        social.Relogio.Avancar(TimeSpan.FromSeconds(1));
        Assert.Equal(HttpStatusCode.NoContent, (await ana.PutAsync(_trofeusVistos, null, Ct)).StatusCode);
        var vistos = await NovidadesAsync(ana);
        Assert.False(vistos.Trofeus);

        // Abrir Troféus não lê o sininho: a notificação da medalha continua acesa.
        await social.EsvaziarFilaAsync();
        Assert.True((await NovidadesAsync(ana)).Sininho);
        Assert.Equal(social.Relogio.GetUtcNow(), (await social.LinhasAsync<Usuario>()).Single(usuario => usuario.Id == social.Id(Ana)).TrofeusVistoEm);

        // Prêmio que chega depois acende de novo; a medalha anulada não conta.
        social.Relogio.Avancar(TimeSpan.FromSeconds(1));
        var outra = await social.MedalhaAsync(Ana, "M03");
        Assert.True((await NovidadesAsync(ana)).Trofeus);
        await social.AnularAsync(outra);
        Assert.False((await NovidadesAsync(ana)).Trofeus);

        // Professor não tem Troféus: a bolinha nunca acende.
        Assert.False((await NovidadesAsync(renata)).Trofeus);
    }

    private static async Task Passo(MundoDePremios mundo)
    {
        await mundo.Social.EsvaziarFilaAsync();
        mundo.Social.Relogio.Avancar(TimeSpan.FromSeconds(1));
    }

    private static async Task<HttpResponseMessage> PostarAsync(HttpClient client, string caminho, object? corpo = null)
    {
        var resposta = corpo is null
            ? await client.PostAsync(new Uri(caminho, UriKind.Relative), null, Ct)
            : await client.PostAsJsonAsync(new Uri(caminho, UriKind.Relative), corpo, Ct);
        Assert.True(resposta.IsSuccessStatusCode, $"{caminho}: {resposta.StatusCode}");
        return resposta;
    }

    private static async Task AvisosAsync(Social.MundoSocial social, int quantos)
    {
        // As contas do MundoSocial nascem na hora real, e o relógio do teste parte do segundo
        // cheio em que o mundo começou a ser montado, antes da migração. Sem alcançar a hora
        // real, o aviso sairia antes das contas, e os destinatários, fixados na publicação,
        // não as incluiriam.
        var atraso = DateTimeOffset.UtcNow - social.Relogio.GetUtcNow();
        if (atraso > TimeSpan.Zero)
        {
            social.Relogio.Avancar(TimeSpan.FromSeconds(Math.Ceiling(atraso.TotalSeconds)));
        }

        using var coordenacao = await social.ComoAsync(Coordenacao);
        for (var i = 0; i < quantos; i++)
        {
            social.Relogio.Avancar(TimeSpan.FromSeconds(1));
            await PostarAsync(coordenacao, "/api/v1/avisos", new { escopo = "faculdade", titulo = $"Aviso {i}", texto = "Texto." });
        }

        await social.EsvaziarFilaAsync();
    }

    // Uma conquista concedida e outra concedida e removida pelo recálculo, com as
    // notificações gravadas como o handler do worker as grava.
    private static async Task<(Guid Obtida, Guid Removida)> ConquistasAsync(Social.MundoSocial social, string aluno)
    {
        await using var scope = social.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var agora = social.Relogio.GetUtcNow();
        var alunoId = social.Id(aluno);
        db.Add(CatalogoRowMapper.ParaRow(_c90));
        var obtida = Conquista.Conceder(_c90, 1, alunoId, new InstanciaDePeriodo(Periodo.Curso, alunoId), Avaliacao.De(1, 1, []), agora);
        var removida = Conquista.Conceder(
            _c90, 1, alunoId, new InstanciaDePeriodo(Periodo.Curso, Guid.CreateVersion7()), Avaliacao.De(1, 1, []), agora.AddSeconds(-1));
        Assert.True(removida.Recalcular(Avaliacao.De(0, 1, []), agora));
        db.AddRange(
            obtida,
            removida,
            Notificacao.ConquistaObtida(obtida, agora),
            Notificacao.ConquistaRemovida(removida, agora.AddMilliseconds(1)));
        await db.SaveChangesAsync(Ct);
        return (obtida.Id, removida.Id);
    }

    private static async Task<NovidadesResponse> NovidadesAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<NovidadesResponse>(_novidades, Ct))!;

    private sealed record ResumoDaNotificacao(
        Guid ReferenciaId, string? Item, string? Motivo, string? Decisao, Guid? PostId, string? TituloDoAviso);

    private static ResumoDaNotificacao Resumo(
        Guid referencia, string? item = null, string? motivo = null, string? decisao = null, Guid? postId = null, string? tituloDoAviso = null) =>
        new(referencia, item, motivo, decisao, postId, tituloDoAviso);

    private static ResumoDaNotificacao Resumo(NotificacaoResponse notificacao) =>
        new(notificacao.ReferenciaId, notificacao.Item, notificacao.Motivo, notificacao.Decisao, notificacao.PostId, notificacao.TituloDoAviso);
}
