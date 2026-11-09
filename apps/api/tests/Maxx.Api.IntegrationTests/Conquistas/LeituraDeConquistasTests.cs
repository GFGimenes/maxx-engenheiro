using System.Net.Http.Json;
using Maxx.Api.Premios;
using Maxx.Application.Premios;
using Microsoft.Extensions.DependencyInjection;

namespace Maxx.Api.IntegrationTests.Conquistas;

// A leitura das conquistas (passo 9, etapa 5): em Troféus, a obtida com a instância em que foi
// obtida e o progresso ("2 de 3") das que faltam na instância vigente; no perfil público, só
// as obtidas, nunca o progresso (produto.md 9.1).
public sealed class LeituraDeConquistasTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Trofeus_traz_a_conquista_obtida_com_a_instancia_e_o_progresso_das_que_faltam()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.CincoNoBimestreAsync(mundo.Ana);
        await mundo.EsvaziarAsync();
        using var ana = await mundo.ComoAsync("ana.souza@al.unieduk.com.br");

        var premios = (await ana.GetFromJsonAsync<PremiosDoAlunoResponse>("/api/v1/eu/premios", Ct))!;
        var itens = premios.Itens.ToDictionary(item => item.Codigo);

        var c01 = Assert.Single(itens["C01"].Obtidos);
        Assert.Equal(("bronze", 25), (c01.Tier, c01.Pontos));
        Assert.Equal(new InstanciaResponse("bimestre", mundo.Ano.Ano, 1, 2), c01.Instancia);
        Assert.Null(itens["C01"].Progresso);

        // Contagem gravada pelo worker, na instância vigente de cada período.
        Assert.Equal(new ProgressoResponse("bimestre", 2, 3), itens["C02"].Progresso);
        Assert.Equal(new ProgressoResponse("bimestre", 1, 2), itens["C03"].Progresso);
        Assert.Equal(new ProgressoResponse("semestre", 5, 10), itens["C07"].Progresso);
        Assert.Equal(new ProgressoResponse("ano", 5, 20), itens["C12"].Progresso);
        Assert.Equal(new ProgressoResponse("curso", 5, 50), itens["C16"].Progresso);

        // Nenhum fato ainda: zero, com o alvo do catálogo ou as matérias do período.
        Assert.Equal(new ProgressoResponse("semestre", 0, 3), itens["C05"].Progresso);
        Assert.Equal(new ProgressoResponse("semestre", 0, 2), itens["C10"].Progresso);

        // Medalha e prêmio de participação não têm progresso nem instância.
        Assert.Null(itens["M01"].Progresso);
        Assert.All(itens["M01"].Obtidos, obtido => Assert.Null(obtido.Instancia));
        Assert.Null(itens["P01"].Progresso);
    }

    [Fact]
    public async Task Perfil_publico_mostra_a_conquista_obtida_e_nunca_o_progresso()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.CincoNoBimestreAsync(mundo.Ana);
        await mundo.EsvaziarAsync();
        using var bruno = await mundo.ComoAsync("bruno.lima@al.unieduk.com.br");

        var publicos = (await bruno.GetFromJsonAsync<PremiosDoAlunoResponse>($"/api/v1/perfis/{mundo.Ana.Id}/premios", Ct))!;

        var c01 = Assert.Single(publicos.Itens, item => item.Codigo == "C01");
        Assert.Equal("bimestre", Assert.Single(c01.Obtidos).Instancia?.Periodo);
        Assert.All(publicos.Itens, item => Assert.Null(item.Progresso));
        Assert.DoesNotContain(publicos.Itens, item => item.Codigo == "C02");
    }

    [Fact]
    public async Task No_recesso_nao_ha_progresso_de_bimestre_nem_de_semestre()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.CincoNoBimestreAsync(mundo.Ana);
        await mundo.EsvaziarAsync();

        // 26 dias depois de hoje: o semestre 1 acabou ontem e o 2 ainda não começou.
        mundo.Relogio.Avancar(TimeSpan.FromDays(26));
        var itens = (await PremiosAsync(mundo)).Itens.ToDictionary(item => item.Codigo);

        Assert.Null(itens["C02"].Progresso);
        Assert.Null(itens["C03"].Progresso);
        Assert.Null(itens["C07"].Progresso);
        Assert.Null(itens["C05"].Progresso);
        Assert.Equal(new ProgressoDaConquista(Domain.Catalogo.Periodo.Ano, 5, 20), itens["C12"].Progresso);
        Assert.Equal(new ProgressoDaConquista(Domain.Catalogo.Periodo.Curso, 5, 50), itens["C16"].Progresso);
    }

    [Fact]
    public async Task Novo_bimestre_comeca_do_zero_e_sem_matricula_no_periodo_o_alvo_dinamico_some()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.CincoNoBimestreAsync(mundo.Ana);
        await mundo.EsvaziarAsync();

        // No bimestre 3, do semestre 2, em que a Ana não tem matrícula.
        mundo.Relogio.Avancar(TimeSpan.FromDays(30));
        var itens = (await PremiosAsync(mundo)).Itens.ToDictionary(item => item.Codigo);

        Assert.Equal(new ProgressoDaConquista(Domain.Catalogo.Periodo.Bimestre, 0, 5), itens["C01"].Progresso);
        Assert.Equal(new ProgressoDaConquista(Domain.Catalogo.Periodo.Bimestre, 0, 3), itens["C02"].Progresso);
        Assert.Null(itens["C03"].Progresso);
        Assert.Null(itens["C04"].Progresso);
        Assert.Null(itens["C10"].Progresso);
        Assert.Null(itens["C11"].Progresso);
    }

    private static async Task<PremiosDoAluno> PremiosAsync(MundoDeConquistas mundo)
    {
        await using var scope = mundo.Factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IConsultaDePremios>().DoAlunoAsync(mundo.Ana.Id, Ct))!;
    }
}
