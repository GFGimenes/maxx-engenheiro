using Maxx.Application.Conquistas;
using Maxx.Application.Medalhas;
using Maxx.Application.Premios;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Notificacoes;
using Maxx.Infrastructure.Conquistas;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.RaridadeDosItens;
using Microsoft.Extensions.DependencyInjection;

namespace Maxx.Api.IntegrationTests.Conquistas;

// O recálculo na anulação de medalha contra o Postgres real (regras-conquista.md 6): só as
// conquistas que têm a medalha nas evidências são reavaliadas; a que deixou de ser satisfeita
// sai, com os pontos, e o aluno é notificado; a que continua fica, com as evidências do
// conjunto atual.
public sealed class AnulacaoEConquistasTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Três notas 10 no bimestre de hoje: duas em Banco de Dados e uma em Redes (o limite de
    // M01 é dois por matéria e bimestre).
    private static async Task<List<Guid>> TresNotas10Async(MundoDeConquistas mundo) =>
    [
        await mundo.ConcederAsync(mundo.Ana, "M01", mundo.BancoDeDados),
        await mundo.ConcederAsync(mundo.Ana, "M01", mundo.BancoDeDados),
        await mundo.ConcederAsync(mundo.Ana, "M01", mundo.Redes),
    ];

    [Fact]
    public async Task Anular_uma_das_tres_notas_10_remove_a_C02_tira_os_pontos_e_notifica()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        var notas = await TresNotas10Async(mundo);
        await mundo.EsvaziarAsync();
        var concedida = Assert.Single(await mundo.LinhasAsync<Conquista>());
        Assert.Equal((3 * 30) + 40, await PontosAsync(mundo));

        mundo.Relogio.Avancar(TimeSpan.FromMinutes(1));
        await mundo.AnularAsync(notas[0]);
        await mundo.EsvaziarAsync();

        var removida = Assert.Single(await mundo.LinhasAsync<Conquista>());
        Assert.Equal(concedida.Id, removida.Id);
        Assert.Equal(mundo.Relogio.GetUtcNow(), removida.RemovidaEm);
        Assert.Equal(2 * 30, await PontosAsync(mundo));

        var notificacao = Assert.Single(await mundo.LinhasAsync<Notificacao>(), linha => linha.Tipo == TipoDeNotificacao.ConquistaRemovida);
        Assert.Equal((mundo.Ana.Id, removida.Id), (notificacao.UsuarioId, notificacao.ReferenciaId));
        var c02 = Assert.Single(await mundo.LinhasAsync<ProgressoDeConquistaRow>(), linha => linha.ItemCodigo == "C02");
        Assert.Equal((2, 3), (c02.Contagem, c02.Alvo));

        // Passada a janela da coalescência, a raridade já não conta a Ana na C02.
        mundo.Relogio.Avancar(TimeSpan.FromSeconds(11));
        await mundo.EnfileirarAsync(new RecalcularRaridade());
        await mundo.EsvaziarAsync();
        Assert.Equal(0, Assert.Single(await mundo.LinhasAsync<RaridadeDoItemRow>(), linha => linha.ItemCodigo == "C02").AlunosComItem);
    }

    [Fact]
    public async Task Com_quatro_notas_10_anular_uma_evidencia_mantem_a_C02_com_as_evidencias_do_conjunto_atual()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        var notas = await TresNotas10Async(mundo);
        await mundo.EsvaziarAsync();
        var quarta = await mundo.ConcederAsync(mundo.Ana, "M01", mundo.Redes);
        await mundo.EsvaziarAsync();
        Assert.Equal(notas.Order(), (await EvidenciasAsync(mundo)).Order());

        await mundo.AnularAsync(notas[0]);
        await mundo.EsvaziarAsync();

        var conquista = Assert.Single(await mundo.LinhasAsync<Conquista>());
        Assert.True(conquista.EstaAtiva);
        Assert.Equal(new[] { notas[1], notas[2], quarta }.Order(), (await EvidenciasAsync(mundo)).Order());
        Assert.DoesNotContain(await mundo.LinhasAsync<Notificacao>(), linha => linha.Tipo == TipoDeNotificacao.ConquistaRemovida);
    }

    [Fact]
    public async Task Conquista_sem_a_medalha_anulada_nas_evidencias_nao_e_tocada()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await TresNotas10Async(mundo);
        var melhorNota = await mundo.ConcederAsync(mundo.Ana, "M02", mundo.BancoDeDados);
        await mundo.ConcederAsync(mundo.Ana, "M02", mundo.BancoDeDados);
        await mundo.EsvaziarAsync();
        var antes = (await mundo.LinhasAsync<EvidenciaDaConquista>()).Where(evidencia => evidencia.MedalhaId != melhorNota).ToList();
        var porItem = (await mundo.LinhasAsync<Conquista>()).ToDictionary(conquista => conquista.ItemCodigo);
        Assert.Equal(["C01", "C02"], porItem.Keys.Order(StringComparer.Ordinal));

        // A M02 compõe a C01 (cinco medalhas no bimestre), não a C02 (três notas 10).
        await mundo.AnularAsync(melhorNota);
        await mundo.EsvaziarAsync();

        var depois = (await mundo.LinhasAsync<Conquista>()).ToDictionary(conquista => conquista.ItemCodigo);
        Assert.False(depois["C01"].EstaAtiva);
        Assert.True(depois["C02"].EstaAtiva);
        var evidenciasDaC02 = (await mundo.LinhasAsync<EvidenciaDaConquista>()).Where(evidencia => evidencia.ConquistaId == porItem["C02"].Id);
        Assert.Equal(
            antes.Where(evidencia => evidencia.ConquistaId == porItem["C02"].Id).Select(evidencia => evidencia.Id).Order(),
            evidenciasDaC02.Select(evidencia => evidencia.Id).Order());
    }

    [Fact]
    public async Task Anular_uma_nota_10_remove_a_C04_ja_fechada()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        var bd = await mundo.ConcederAsync(mundo.Ana, "M01", mundo.BancoDeDados);
        await mundo.ConcederAsync(mundo.Ana, "M01", mundo.Redes);
        await mundo.EsvaziarAsync();

        // O fechamento do bimestre chega na etapa 4: aqui a C04 é gravada como ele gravaria,
        // com a nota 10 nas duas matérias da Ana.
        var instancia = new InstanciaDePeriodo(Periodo.Bimestre, mundo.BimestreDeHoje.Id);
        await using (var scope = mundo.Factory.Services.CreateAsyncScope())
        {
            var repositorio = scope.ServiceProvider.GetRequiredService<IConquistaRepository>();
            var calendario = await repositorio.CalendarioAsync(Ct);
            var c04 = (await repositorio.CatalogoAsync(Ct)).Single(conquista => conquista.Item.Codigo == "C04");
            var contexto = new ContextoDaAvaliacao(
                mundo.BimestreDeHoje.Fim, "CC", new HashSet<Guid> { mundo.BancoDeDados.MateriaId, mundo.Redes.MateriaId });
            var avaliacao = scope.ServiceProvider.GetRequiredService<AvaliadorDeConquista>()
                .Avaliar(c04.Item, instancia, await repositorio.FatosAsync(mundo.Ana.Id, calendario, Ct), contexto);
            repositorio.Adicionar(Conquista.Conceder(c04.Item, c04.Versao, mundo.Ana.Id, instancia, avaliacao, mundo.Relogio.GetUtcNow()));
            await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().SaveChangesAsync(Ct);
        }

        await mundo.AnularAsync(bd);
        await mundo.EsvaziarAsync();

        var c04Depois = Assert.Single(await mundo.LinhasAsync<Conquista>());
        Assert.Equal("C04", c04Depois.ItemCodigo);
        Assert.False(c04Depois.EstaAtiva);
        var progresso = Assert.Single(await mundo.LinhasAsync<ProgressoDeConquistaRow>(), linha => linha.ItemCodigo == "C04");
        Assert.Equal((1, 2), (progresso.Contagem, progresso.Alvo));
    }

    [Fact]
    public async Task Recalculo_repetido_nao_muda_nada_e_a_vaga_liberada_concede_outra_C02()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        var notas = await TresNotas10Async(mundo);
        await mundo.EsvaziarAsync();
        await mundo.AnularAsync(notas[0]);
        await mundo.EsvaziarAsync();
        var removida = Assert.Single(await mundo.LinhasAsync<Conquista>());

        mundo.Relogio.Avancar(TimeSpan.FromMinutes(1));
        await mundo.EnfileirarAsync(new RecalcularConquistasDaMedalha(notas[0]));
        await mundo.EsvaziarAsync();
        Assert.Equal(removida.RemovidaEm, Assert.Single(await mundo.LinhasAsync<Conquista>()).RemovidaEm);
        Assert.Single(await mundo.LinhasAsync<Notificacao>(), linha => linha.Tipo == TipoDeNotificacao.ConquistaRemovida);

        // A anulada liberou a vaga de M01 em Banco de Dados: a nova nota 10 fecha a C02 de
        // novo, numa linha nova, e a removida fica no histórico.
        await mundo.ConcederAsync(mundo.Ana, "M01", mundo.BancoDeDados);
        await mundo.EsvaziarAsync();
        var conquistas = await mundo.LinhasAsync<Conquista>();
        Assert.Equal(2, conquistas.Count);
        Assert.Single(conquistas, conquista => conquista.EstaAtiva && conquista.Id != removida.Id);
    }

    private static async Task<int> PontosAsync(MundoDeConquistas mundo)
    {
        await using var scope = mundo.Factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IConsultaDePremios>().DoAlunoAsync(mundo.Ana.Id, Ct))!.Pontos;
    }

    private static async Task<List<Guid>> EvidenciasAsync(MundoDeConquistas mundo) =>
        [.. (await mundo.LinhasAsync<EvidenciaDaConquista>()).Select(evidencia => evidencia.MedalhaId!.Value)];
}
