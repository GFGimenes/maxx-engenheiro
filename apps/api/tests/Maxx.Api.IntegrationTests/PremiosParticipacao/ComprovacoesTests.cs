using System.Net;
using System.Net.Http.Json;
using Maxx.Api.IntegrationTests.Fotos;
using Maxx.Api.Perfil;
using Maxx.Api.Posts;
using Maxx.Api.Premios;
using Maxx.Api.PremiosParticipacao;
using Maxx.Application.Fila;
using Maxx.Application.Medalhas;
using Maxx.Application.PremiosParticipacao;
using Maxx.Domain.Identidade;
using Maxx.Domain.Notificacoes;
using Maxx.Domain.Premios;
using Maxx.Domain.Social;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.RaridadeDosItens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.PremiosParticipacao.MundoDePremios;
using static Maxx.Api.IntegrationTests.Social.MundoSocial;

namespace Maxx.Api.IntegrationTests.PremiosParticipacao;

// A fila de comprovações e a decisão (fluxos/02-post-com-comprovacao.md, da fila em diante),
// contra o Postgres e o bucket reais. A Renata leciona Banco de Dados para Ana, Bruno e Diego;
// a Elisa tem conta e não está na turma; o Marcos é professor sem turma.
public sealed class ComprovacoesTests(PostgresFixture postgres, ArmazenamentoFixture armazenamento)
    : IClassFixture<PostgresFixture>, IClassFixture<ArmazenamentoFixture>
{
    private static readonly Uri _fila = new("/api/v1/comprovacoes", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_professora_ve_so_os_alunos_dela_e_a_instituicao_ve_todos_das_mais_antigas_para_as_mais_novas()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var elisa = await mundo.Social.ComoAsync(Elisa);
        using var renata = await mundo.Social.ComoAsync(Renata);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var daElisa = await NaSemanaAsync(elisa, mundo.Semana);
        mundo.Social.Relogio.Avancar(TimeSpan.FromMinutes(1));
        var daAna = await NaSemanaAsync(ana, mundo.Semana, "Na semana acadêmica");

        var daRenata = await FilaAsync(renata);
        var daCoordenacao = await FilaAsync(coordenacao);

        var unica = Assert.Single(daRenata.Itens);
        Assert.Equal((await mundo.ComprovacaoAsync(daAna.Premio!.Id)).Id, unica.Id);
        Assert.Equal(
            (daAna.Premio.Id, (Guid?)daAna.Id, "@anaSouza-CC", "P06", "Semana acadêmica", "bronze", 30, "Na semana acadêmica", false),
            (unica.PremioId, unica.PostId, unica.Aluno.Arroba, unica.Item, unica.NomeDoItem, unica.Tier, unica.Pontos, unica.Legenda, unica.PassouDoPrazo));
        Assert.Equal(("Semana Acadêmica de Computação", false), (unica.Evento.Nome, unica.Evento.Digitado));
        Assert.Equal(daAna.Foto, unica.Foto);
        Assert.Equal(["Banco de Dados"], unica.Materias);
        Assert.Null(daRenata.Proximo);

        Assert.Equal([daElisa.Premio!.Id, daAna.Premio.Id], daCoordenacao.Itens.Select(item => item.PremioId));
        Assert.Empty(daCoordenacao.Itens[0].Materias);
    }

    [Fact]
    public async Task A_marca_aparece_depois_de_7_vezes_24_horas()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        await NaSemanaAsync(ana, mundo.Semana);

        mundo.Social.Relogio.Avancar(TimeSpan.FromDays(7));
        Assert.False(Assert.Single((await FilaAsync(coordenacao)).Itens).PassouDoPrazo);

        mundo.Social.Relogio.Avancar(TimeSpan.FromSeconds(1));
        Assert.True(Assert.Single((await FilaAsync(coordenacao)).Itens).PassouDoPrazo);
    }

    [Fact]
    public async Task Filtra_por_evento_e_lista_os_eventos_com_pendentes()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var bruno = await mundo.Social.ComoAsync(Bruno);
        using var diego = await mundo.Social.ComoAsync(Diego);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        await NaSemanaAsync(ana, mundo.Semana);
        await NaSemanaAsync(bruno, mundo.Semana);
        var palestra = await ReivindicarAsync(diego, new
        {
            item = "P01",
            eventoDigitado = new { nome = "Palestra de LGPD", inicio = mundo.Hoje.AddDays(-2), fim = mundo.Hoje.AddDays(-2) },
            uploadId = (await FotoEnviadaAsync(diego)).Id,
        });
        Assert.Equal(HttpStatusCode.Created, palestra.StatusCode);

        var daSemana = await FilaAsync(coordenacao, $"?eventoId={mundo.Semana}");
        var eventos = (await coordenacao.GetFromJsonAsync<List<EventoNaFilaResponse>>(new Uri("/api/v1/comprovacoes/eventos", UriKind.Relative), Ct))!;

        Assert.Equal(2, daSemana.Itens.Count);
        Assert.All(daSemana.Itens, item => Assert.Equal(mundo.Semana, item.Evento.Id));
        Assert.Equal(
            [("Palestra de LGPD", 1), ("Semana Acadêmica de Computação", 2)],
            eventos.Select(evento => (evento.Nome, evento.Pendentes)));
        Assert.True((await FilaAsync(coordenacao)).Itens.Single(item => item.Item == "P01").Evento.Digitado);
    }

    [Fact]
    public async Task Pagina_de_20_por_cursor_e_cursor_adulterado_e_400()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        await PendentesNoBancoAsync(mundo, 21);

        var primeira = await FilaAsync(coordenacao);
        var segunda = await FilaAsync(coordenacao, $"?cursor={primeira.Proximo}");

        Assert.Equal(20, primeira.Itens.Count);
        Assert.Single(segunda.Itens);
        Assert.Null(segunda.Proximo);
        Assert.Equal(21, primeira.Itens.Concat(segunda.Itens).Select(item => item.Id).Distinct().Count());
        Assert.True(primeira.Itens.Zip(primeira.Itens.Skip(1)).All(par => par.First.ReivindicadaEm <= par.Second.ReivindicadaEm));
        await AssertRecusaAsync(await coordenacao.GetAsync(new Uri("/api/v1/comprovacoes?cursor=xyz", UriKind.Relative), Ct), HttpStatusCode.BadRequest, "cursor-invalido");
    }

    [Fact]
    public async Task Aprovada_pontua_na_hora_sai_da_fila_e_enfileira_notificacao_raridade_e_o_fim_da_janela()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var renata = await mundo.Social.ComoAsync(Renata);
        var post = await NaSemanaAsync(ana, mundo.Semana);
        var comprovacao = await mundo.ComprovacaoAsync(post.Premio!.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await AprovarAsync(renata, comprovacao.Id)).StatusCode);

        var premio = await mundo.PremioAsync(post.Premio.Id);
        var agora = mundo.Social.Relogio.GetUtcNow();
        Assert.Equal((EstadoDoPremioParticipacao.Valido, mundo.Social.Id(Renata), agora), (premio.Estado, premio.AprovadoPorId!.Value, premio.AprovadoEm!.Value));
        Assert.Empty((await FilaAsync(renata)).Itens);
        Assert.Equal(30, (await ana.GetFromJsonAsync<PerfilResponse>(new Uri("/api/v1/eu/perfil", UriKind.Relative), Ct))!.Pontos);
        var trofeus = (await ana.GetFromJsonAsync<PremiosDoAlunoResponse>(new Uri("/api/v1/eu/premios", UriKind.Relative), Ct))!;
        Assert.Equal(30, trofeus.Pontos);
        var obtido = Assert.Single(trofeus.Itens.Single(item => item.Codigo == "P06").Obtidos);
        Assert.Equal((premio.Id, 30, (Guid?)post.Id, agora), (obtido.Id, obtido.Pontos, obtido.PostId, obtido.ObtidoEm));
        Assert.Equal("valido", (await ana.GetFromJsonAsync<PostResponse>(new Uri($"/api/v1/posts/{post.Id}", UriKind.Relative), Ct))!.Premio!.Estado);

        var trabalhos = await mundo.Social.LinhasAsync<Trabalho>();
        Assert.Contains(trabalhos, trabalho => trabalho.Tipo == NotificarComprovacaoAprovada.Tipo);
        Assert.Contains(trabalhos, trabalho => trabalho.Tipo == RecalcularRaridade.Tipo);
        Assert.Equal(agora.AddDays(15), Assert.Single(trabalhos, trabalho => trabalho.Tipo == TornarPremioDefinitivo.Tipo).DisponivelEm);

        await mundo.Social.EsvaziarFilaAsync();
        var notificacao = Assert.Single(await mundo.Social.LinhasAsync<Notificacao>());
        Assert.Equal((mundo.Social.Id(Ana), TipoDeNotificacao.ComprovacaoAprovada, premio.Id), (notificacao.UsuarioId, notificacao.Tipo, notificacao.ReferenciaId));
        Assert.Equal(1, Assert.Single(await mundo.Social.LinhasAsync<RaridadeDoItemRow>(), linha => linha.ItemCodigo == "P06").AlunosComItem);
        Assert.Equal(EstadoDoPremioParticipacao.Valido, (await mundo.PremioAsync(premio.Id)).Estado);
    }

    [Fact]
    public async Task Sem_contestacao_fica_definitivo_quando_a_janela_fecha_e_continua_pontuando()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var post = await NaSemanaAsync(ana, mundo.Semana);
        await AprovarAsync(coordenacao, (await mundo.ComprovacaoAsync(post.Premio!.Id)).Id);
        var aprovadoEm = mundo.Social.Relogio.GetUtcNow();

        mundo.Social.Relogio.Avancar(TimeSpan.FromDays(15) - TimeSpan.FromSeconds(1));
        await mundo.Social.EsvaziarFilaAsync();
        Assert.Equal(EstadoDoPremioParticipacao.Valido, (await mundo.PremioAsync(post.Premio.Id)).Estado);

        mundo.Social.Relogio.Avancar(TimeSpan.FromSeconds(1));
        await mundo.Social.EsvaziarFilaAsync();
        var premio = await mundo.PremioAsync(post.Premio.Id);
        Assert.Equal((EstadoDoPremioParticipacao.Definitivo, aprovadoEm.AddDays(15)), (premio.Estado, premio.DefinitivoEm!.Value));
        Assert.Equal(30, (await ana.GetFromJsonAsync<PerfilResponse>(new Uri("/api/v1/eu/perfil", UriKind.Relative), Ct))!.Pontos);
        Assert.DoesNotContain(await mundo.Social.LinhasAsync<Trabalho>(), trabalho => trabalho.Estado == EstadoDoTrabalho.Falho);
    }

    [Fact]
    public async Task Trabalho_que_chega_antes_do_fim_da_janela_volta_para_a_fila_no_instante_certo()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var post = await NaSemanaAsync(ana, mundo.Semana);
        await AprovarAsync(coordenacao, (await mundo.ComprovacaoAsync(post.Premio!.Id)).Id);
        var fimDaJanela = mundo.Social.Relogio.GetUtcNow().AddDays(15);

        await using (var scope = mundo.Social.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ITrabalhoHandler<TornarPremioDefinitivo>>()
                .ExecutarAsync(new TornarPremioDefinitivo(post.Premio.Id), Ct);
        }

        Assert.Equal(EstadoDoPremioParticipacao.Valido, (await mundo.PremioAsync(post.Premio.Id)).Estado);
        Assert.Equal(2, (await mundo.Social.LinhasAsync<Trabalho>()).Count(trabalho => trabalho.Tipo == TornarPremioDefinitivo.Tipo && trabalho.DisponivelEm == fimDaJanela));
    }

    [Fact]
    public async Task Reprovada_com_motivo_desvincula_o_post_notifica_e_libera_nova_tentativa_com_outra_foto()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var post = await NaSemanaAsync(ana, mundo.Semana);

        var resposta = await ReprovarAsync(coordenacao, (await mundo.ComprovacaoAsync(post.Premio!.Id)).Id, "  A foto não mostra a semana acadêmica.  ");

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        var premio = await mundo.PremioAsync(post.Premio.Id);
        Assert.Equal(
            (EstadoDoPremioParticipacao.Reprovado, mundo.Social.Id(Coordenacao), "A foto não mostra a semana acadêmica."),
            (premio.Estado, premio.ReprovadoPorId!.Value, premio.MotivoDaReprovacao));

        // O post fica como post comum, com a foto, e agora sai como qualquer outro.
        var comum = (await ana.GetFromJsonAsync<PostResponse>(new Uri($"/api/v1/posts/{post.Id}", UriKind.Relative), Ct))!;
        Assert.Null(comum.Premio);
        Assert.NotNull(comum.Foto);
        Assert.Equal(0, (await ana.GetFromJsonAsync<PerfilResponse>(new Uri("/api/v1/eu/perfil", UriKind.Relative), Ct))!.Pontos);

        await mundo.Social.EsvaziarFilaAsync();
        Assert.Equal(TipoDeNotificacao.ComprovacaoReprovada, Assert.Single(await mundo.Social.LinhasAsync<Notificacao>()).Tipo);

        // Nova tentativa: outro post, outra foto, o mesmo evento.
        var novo = await NaSemanaAsync(ana, mundo.Semana);
        Assert.NotEqual(post.Id, novo.Id);
        Assert.Equal("reivindicado", novo.Premio!.Estado);
        Assert.Equal(HttpStatusCode.NoContent, (await ana.DeleteAsync(new Uri($"/api/v1/posts/{post.Id}", UriKind.Relative), Ct)).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Reprovar_sem_motivo_e_400_e_nada_muda(string? motivo)
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var renata = await mundo.Social.ComoAsync(Renata);
        var post = await NaSemanaAsync(ana, mundo.Semana);

        await AssertRecusaAsync(await ReprovarAsync(renata, (await mundo.ComprovacaoAsync(post.Premio!.Id)).Id, motivo), HttpStatusCode.BadRequest, "motivo-invalido");
        await AssertRecusaAsync(
            await ReprovarAsync(renata, (await mundo.ComprovacaoAsync(post.Premio.Id)).Id, new string('a', 501)), HttpStatusCode.BadRequest, "motivo-invalido");

        Assert.Equal(EstadoDoPremioParticipacao.Reivindicado, (await mundo.PremioAsync(post.Premio.Id)).Estado);
    }

    [Fact]
    public async Task Vale_quem_chega_primeiro_e_os_outros_recebem_ja_decidida()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var renata = await mundo.Social.ComoAsync(Renata);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var comprovacao = (await mundo.ComprovacaoAsync((await NaSemanaAsync(ana, mundo.Semana)).Premio!.Id)).Id;

        var respostas = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => i switch
        {
            0 or 1 => AprovarAsync(renata, comprovacao),
            2 or 3 => AprovarAsync(coordenacao, comprovacao),
            _ => ReprovarAsync(coordenacao, comprovacao, "Outro evento."),
        }));

        Assert.Single(respostas, resposta => resposta.StatusCode == HttpStatusCode.NoContent);
        foreach (var recusada in respostas.Where(resposta => resposta.StatusCode != HttpStatusCode.NoContent))
        {
            await AssertRecusaAsync(recusada, HttpStatusCode.Conflict, "comprovacao-ja-decidida");
        }

        await AssertRecusaAsync(await AprovarAsync(renata, comprovacao), HttpStatusCode.Conflict, "comprovacao-ja-decidida");
    }

    [Fact]
    public async Task Professor_fora_do_escopo_nao_ve_nem_decide_e_aluno_nao_entra_na_fila()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var elisa = await mundo.Social.ComoAsync(Elisa);
        using var renata = await mundo.Social.ComoAsync(Renata);
        using var marcos = await mundo.Social.ComoAsync(Marcos);
        var daAna = (await mundo.ComprovacaoAsync((await NaSemanaAsync(ana, mundo.Semana)).Premio!.Id)).Id;
        var daElisa = (await mundo.ComprovacaoAsync((await NaSemanaAsync(elisa, mundo.Semana)).Premio!.Id)).Id;

        Assert.Empty((await FilaAsync(marcos)).Itens);
        Assert.Empty((await marcos.GetFromJsonAsync<List<EventoNaFilaResponse>>(new Uri("/api/v1/comprovacoes/eventos", UriKind.Relative), Ct))!);
        await AssertRecusaAsync(await AprovarAsync(marcos, daAna), HttpStatusCode.Forbidden, "fora-do-escopo");
        await AssertRecusaAsync(await ReprovarAsync(renata, daElisa, "Não é minha aluna."), HttpStatusCode.Forbidden, "fora-do-escopo");
        await AssertRecusaAsync(await AprovarAsync(renata, Guid.NewGuid()), HttpStatusCode.NotFound, "comprovacao-nao-encontrada");
        Assert.Equal(HttpStatusCode.Forbidden, (await ana.GetAsync(_fila, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ana.PostAsync(new Uri($"/api/v1/comprovacoes/{daAna}/aprovacao", UriKind.Relative), null, Ct)).StatusCode);
    }

    [Fact]
    public async Task Ex_aluno_nao_tem_a_comprovacao_aprovada()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var post = await NaSemanaAsync(ana, mundo.Semana);
        await using (var scope = mundo.Social.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
            (await db.Set<Usuario>().SingleAsync(usuario => usuario.Id == mundo.Social.Id(Ana), Ct)).MarcarExAluno(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(Ct);
        }

        await AssertRecusaAsync(await AprovarAsync(coordenacao, (await mundo.ComprovacaoAsync(post.Premio!.Id)).Id), HttpStatusCode.Conflict, "ex-aluno");
    }

    private static async Task<PaginaDeComprovacoesResponse> FilaAsync(HttpClient client, string consulta = "")
    {
        var resposta = await client.GetAsync(new Uri($"/api/v1/comprovacoes{consulta}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<PaginaDeComprovacoesResponse>(Ct))!;
    }

    private static Task<HttpResponseMessage> AprovarAsync(HttpClient client, Guid comprovacao) =>
        client.PostAsync(new Uri($"/api/v1/comprovacoes/{comprovacao}/aprovacao", UriKind.Relative), null, Ct);

    private static Task<HttpResponseMessage> ReprovarAsync(HttpClient client, Guid comprovacao, string? motivo) =>
        client.PostAsJsonAsync(new Uri($"/api/v1/comprovacoes/{comprovacao}/reprovacao", UriKind.Relative), new { motivo }, Ct);

    // Reivindicações gravadas direto pelo domínio, sem foto no bucket: a paginação só lê o
    // banco, e o limite de upload por hora não deixaria 21 pedidos de uma vez. Uma por dia,
    // com evento digitado (a chave muda com a data), alternando Ana, Bruno e Diego.
    private static async Task PendentesNoBancoAsync(MundoDePremios mundo, int quantas)
    {
        await using var scope = mundo.Social.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var alunos = await db.Set<Usuario>().Where(usuario => usuario.Papeis == Domain.Academico.Papeis.Aluno && usuario.ExAlunoDesde == null).ToListAsync(Ct);
        var agora = mundo.Social.Relogio.GetUtcNow();
        for (var i = 0; i < quantas; i++)
        {
            var aluno = alunos[i % alunos.Count];
            var evento = Evento.TextoLivre($"Palestra {i}", mundo.Hoje.AddDays(-i - 1), mundo.Hoje.AddDays(-i - 1), aluno);
            var premio = PremioParticipacao.Reivindicar(CatalogoDeParticipacao.P01, 1, aluno, null, evento, null, null, agora.AddMinutes(i)).Premio!;
            var upload = Domain.Fotos.UploadPendente.Pedir(aluno, Domain.Fotos.FinalidadeDoUpload.Post, "image/jpeg", 1000, 500, agora).Upload!;
            var post = Post.Reivindicar(aluno, null, upload, premio, agora.AddMinutes(i)).Post!;
            db.AddRange(evento, premio, upload, post, Comprovacao.De(premio, post.Id));
        }

        await db.SaveChangesAsync(Ct);
    }
}
