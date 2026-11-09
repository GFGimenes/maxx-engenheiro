using Maxx.Api.IntegrationTests.Conquistas;
using Maxx.Application;
using Maxx.Application.Conquistas;
using Maxx.Application.Fila;
using Maxx.Application.Ranking;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Ranking;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Ranking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Maxx.Api.IntegrationTests.Ranking;

// Quem pede o recálculo do ranking (ADR 0005), do lado das medalhas e das conquistas: a
// concessão e a anulação, e a conquista concedida ou removida pelo worker. O pedido nasce
// na mesma transação da escrita: a recusa não deixa pedido.
public sealed class PedidosDasMedalhasEConquistasTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Concessao_de_medalha_pede_o_recalculo_e_a_concessao_recusada_nao()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);

        await mundo.ConcederAsync(mundo.Ana, "M03");
        Assert.Equal(1, await PedidosAsync(mundo));

        // M03 tem uma vaga por bimestre na matéria: a segunda é recusada.
        await Assert.ThrowsAsync<RecusaException>(() => mundo.ConcederAsync(mundo.Ana, "M03"));
        Assert.Equal(1, await PedidosAsync(mundo));
    }

    [Fact]
    public async Task Anulacao_de_medalha_pede_o_recalculo()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        var medalha = await mundo.ConcederAsync(mundo.Ana, "M01");
        await LimparPedidosAsync(mundo);

        await mundo.AnularAsync(medalha);

        Assert.Equal(1, await PedidosAsync(mundo));
    }

    [Fact]
    public async Task Conquista_concedida_por_fato_pede_o_recalculo_e_sem_conceder_nao()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        // Quatro medalhas não fecham nada; a quinta fecha a C01.
        List<Guid> medalhas = [];
        foreach (var item in new[] { "M01", "M01", "M02", "M02" })
        {
            medalhas.Add(await mundo.ConcederAsync(mundo.Ana, item));
        }

        await LimparPedidosAsync(mundo);
        await ExecutarAsync(mundo, ReavaliarConquistas.PelaMedalha(mundo.Ana.Id, medalhas[3]));
        Assert.Equal(0, await PedidosAsync(mundo));
        Assert.Empty(await mundo.LinhasAsync<Domain.Conquistas.Conquista>());

        var quinta = await mundo.ConcederAsync(mundo.Ana, "M03");
        await LimparPedidosAsync(mundo);
        await ExecutarAsync(mundo, ReavaliarConquistas.PelaMedalha(mundo.Ana.Id, quinta));
        Assert.NotEmpty(await mundo.LinhasAsync<Domain.Conquistas.Conquista>());
        Assert.Equal(1, await PedidosAsync(mundo));
    }

    [Fact]
    public async Task Conquista_concedida_no_fechamento_pede_o_recalculo()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.ConcederAsync(mundo.Ana, "M03", mundo.BancoDeDados);
        await mundo.ConcederAsync(mundo.Ana, "M03", mundo.Redes);
        await mundo.EsvaziarAsync();
        await LimparPedidosAsync(mundo);

        await ExecutarAsync(mundo, new AvaliarFechamentoDoAluno(Periodo.Bimestre, mundo.BimestreDeHoje.Id, mundo.Ana.Id));

        Assert.Single(await mundo.LinhasAsync<Domain.Conquistas.Conquista>(), conquista => conquista.ItemCodigo == "C03");
        Assert.Equal(1, await PedidosAsync(mundo));
    }

    [Fact]
    public async Task Conquista_removida_pelo_recalculo_da_anulacao_pede_o_recalculo()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        var medalhas = await mundo.CincoNoBimestreAsync(mundo.Ana);
        await mundo.EsvaziarAsync();
        await mundo.AnularAsync(medalhas[0]);
        await LimparPedidosAsync(mundo);

        await ExecutarAsync(mundo, new RecalcularConquistasDaMedalha(medalhas[0]));

        Assert.Contains(await mundo.LinhasAsync<Domain.Conquistas.Conquista>(), conquista => !conquista.EstaAtiva);
        Assert.Equal(1, await PedidosAsync(mundo));
    }

    [Fact]
    public async Task Pelo_worker_a_concessao_move_o_ranking_sem_pedido_manual()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.ConcederAsync(mundo.Ana, "M01");

        await mundo.EsvaziarAsync();

        var linhas = await mundo.LinhasAsync<PosicaoNoRankingRow>();
        Assert.Equal(4, linhas.Count);
        Assert.All(linhas, linha => Assert.Equal((mundo.Ana.Id, 1, 30), (linha.AlunoId, linha.Posicao, linha.Pontos)));
        Assert.Contains(linhas, linha => linha.Periodo == PeriodoDoRanking.Semestre && linha.SemestreLetivoId == mundo.Semestre1.Id);
    }

    private static async Task<int> PedidosAsync(MundoDeConquistas mundo) =>
        (await mundo.LinhasAsync<Trabalho>()).Count(trabalho => trabalho.Tipo == RecalcularRanking.Tipo);

    private static async Task LimparPedidosAsync(MundoDeConquistas mundo)
    {
        await using var scope = mundo.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Database
            .ExecuteSqlAsync($"DELETE FROM trabalho WHERE tipo = {RecalcularRanking.Tipo}", Ct);
    }

    // Roda um trabalho direto no handler, numa transação própria, como o worker faria: a
    // trava de conquistas do aluno só existe dentro de uma.
    private static async Task ExecutarAsync<TCarga>(MundoDeConquistas mundo, TCarga carga)
        where TCarga : ICargaDeTrabalho
    {
        await using var scope = mundo.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        await using var transacao = await db.Database.BeginTransactionAsync(Ct);
        await scope.ServiceProvider.GetRequiredService<ITrabalhoHandler<TCarga>>().ExecutarAsync(carga, Ct);
        await db.SaveChangesAsync(Ct);
        await transacao.CommitAsync(Ct);
    }
}
