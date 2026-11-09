using System.Net;
using System.Net.Http.Json;
using Maxx.Api.IntegrationTests.Fotos;
using Maxx.Api.IntegrationTests.PremiosParticipacao;
using Maxx.Application.Ranking;
using Maxx.Domain.Academico;
using Maxx.Domain.Ranking;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Ranking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.PremiosParticipacao.MundoDePremios;
using static Maxx.Api.IntegrationTests.Social.MundoSocial;

namespace Maxx.Api.IntegrationTests.Ranking;

// Quem pede o recálculo do ranking (ADR 0005), do lado dos prêmios de participação: a
// aprovação da comprovação, a contestação, a decisão da instituição e a remoção do post pela
// instituição quando revoga. Reprovar e remover o pendente não mudam pontos e não pedem; a
// recusa não deixa pedido.
public sealed class PedidosDosPremiosTests(PostgresFixture postgres, ArmazenamentoFixture armazenamento)
    : IClassFixture<PostgresFixture>, IClassFixture<ArmazenamentoFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Aprovacao_pede_o_recalculo_e_a_segunda_aprovacao_recusada_nao()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var post = await NaSemanaAsync(ana, mundo.Semana);
        var comprovacao = (await mundo.ComprovacaoAsync(post.Premio!.Id)).Id;
        Assert.Equal(0, await PedidosAsync(mundo));

        Assert.Equal(HttpStatusCode.NoContent, (await AprovarAsync(coordenacao, comprovacao)).StatusCode);
        Assert.Equal(1, await PedidosAsync(mundo));

        Assert.Equal(HttpStatusCode.Conflict, (await AprovarAsync(coordenacao, comprovacao)).StatusCode);
        Assert.Equal(1, await PedidosAsync(mundo));
    }

    [Fact]
    public async Task Reprovacao_nao_pede_o_recalculo()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var post = await NaSemanaAsync(ana, mundo.Semana);

        var resposta = await coordenacao.PostAsJsonAsync(
            new Uri($"/api/v1/comprovacoes/{(await mundo.ComprovacaoAsync(post.Premio!.Id)).Id}/reprovacao", UriKind.Relative),
            new { motivo = "Foto de outro evento." },
            Ct);

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        Assert.Equal(0, await PedidosAsync(mundo));
    }

    [Fact]
    public async Task Contestacao_pede_o_recalculo_e_a_segunda_contestacao_recusada_nao()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var premio = await AprovadoAsync(mundo, ana, coordenacao);
        await LimparPedidosAsync(mundo);

        Assert.Equal(HttpStatusCode.Created, (await ContestarAsync(coordenacao, premio)).StatusCode);
        Assert.Equal(1, await PedidosAsync(mundo));

        Assert.Equal(HttpStatusCode.Conflict, (await ContestarAsync(coordenacao, premio)).StatusCode);
        Assert.Equal(1, await PedidosAsync(mundo));
    }

    [Theory]
    [InlineData("confirmacao")]
    [InlineData("revogacao")]
    public async Task Decisao_da_instituicao_pede_o_recalculo(string decisao)
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var premio = await AprovadoAsync(mundo, ana, coordenacao);
        var contestacao = (await (await ContestarAsync(coordenacao, premio)).Content.ReadFromJsonAsync<IdResponse>(Ct))!.Id;
        await LimparPedidosAsync(mundo);

        var resposta = await coordenacao.PostAsJsonAsync(
            new Uri($"/api/v1/contestacoes/{contestacao}/{decisao}", UriKind.Relative), new { motivo = "Foto de outro evento." }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        Assert.Equal(1, await PedidosAsync(mundo));
    }

    [Theory]
    [InlineData("pendente", 0)]
    [InlineData("valido", 1)]
    [InlineData("emAveriguacao", 1)]
    public async Task Remocao_do_post_pela_instituicao_pede_o_recalculo_quando_revoga(string estado, int pedidos)
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        var post = await NaSemanaAsync(ana, mundo.Semana);
        if (estado != "pendente")
        {
            await AprovarAsync(coordenacao, (await mundo.ComprovacaoAsync(post.Premio!.Id)).Id);
        }

        if (estado == "emAveriguacao")
        {
            await ContestarAsync(coordenacao, post.Premio!.Id);
        }

        await LimparPedidosAsync(mundo);

        var resposta = await coordenacao.DeleteAsync(new Uri($"/api/v1/posts/{post.Id}", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        Assert.Equal(pedidos, await PedidosAsync(mundo));
    }

    [Fact]
    public async Task Pelo_worker_o_premio_aprovado_entra_no_semestre_do_evento()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var coordenacao = await mundo.Social.ComoAsync(Coordenacao);
        await AprovadoAsync(mundo, ana, coordenacao);

        await mundo.Social.EsvaziarFilaAsync();

        // A semana acadêmica começou há 10 dias: conta no semestre que contém essa data.
        var doEvento = await SemestreQueContemAsync(mundo, mundo.Hoje.AddDays(-10));
        var daAna = (await mundo.Social.LinhasAsync<PosicaoNoRankingRow>())
            .Where(linha => linha.AlunoId == mundo.Social.Id(Ana))
            .ToList();
        Assert.Equal(4, daAna.Count);
        Assert.All(daAna, linha => Assert.Equal((1, 30), (linha.Posicao, linha.Pontos)));
        Assert.All(
            daAna.Where(linha => linha.Periodo == PeriodoDoRanking.Semestre),
            linha => Assert.Equal(doEvento.Id, linha.SemestreLetivoId));
    }

    // Reivindicação da semana acadêmica aprovada pela instituição; devolve o id do prêmio.
    private static async Task<Guid> AprovadoAsync(MundoDePremios mundo, HttpClient aluno, HttpClient instituicao)
    {
        var post = await NaSemanaAsync(aluno, mundo.Semana);
        Assert.Equal(HttpStatusCode.NoContent, (await AprovarAsync(instituicao, (await mundo.ComprovacaoAsync(post.Premio!.Id)).Id)).StatusCode);
        return post.Premio.Id;
    }

    private static Task<HttpResponseMessage> AprovarAsync(HttpClient client, Guid comprovacao) =>
        client.PostAsync(new Uri($"/api/v1/comprovacoes/{comprovacao}/aprovacao", UriKind.Relative), null, Ct);

    private static Task<HttpResponseMessage> ContestarAsync(HttpClient client, Guid premio) =>
        client.PostAsJsonAsync(
            new Uri($"/api/v1/premios-participacao/{premio}/contestacao", UriKind.Relative), new { motivo = "Revi a foto." }, Ct);

    private static async Task<SemestreLetivo> SemestreQueContemAsync(MundoDePremios mundo, DateOnly data)
    {
        await using var scope = mundo.Social.Factory.Services.CreateAsyncScope();
        var semestres = await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<SemestreLetivo>()
            .Include(semestre => semestre.Bimestres)
            .AsNoTracking()
            .ToListAsync(Ct);
        return semestres.Single(semestre => semestre.Contem(data));
    }

    private static async Task<int> PedidosAsync(MundoDePremios mundo) =>
        (await mundo.Social.LinhasAsync<Trabalho>()).Count(trabalho => trabalho.Tipo == RecalcularRanking.Tipo);

    private static async Task LimparPedidosAsync(MundoDePremios mundo)
    {
        await using var scope = mundo.Social.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Database
            .ExecuteSqlAsync($"DELETE FROM trabalho WHERE tipo = {RecalcularRanking.Tipo}", Ct);
    }

    private sealed record IdResponse(Guid Id);
}
