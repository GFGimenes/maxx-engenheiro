using Maxx.Application;
using Maxx.Application.Conquistas;
using Maxx.Application.Medalhas;
using Maxx.Application.Perfil;
using Maxx.Application.Premios;
using Maxx.Application.PremiosParticipacao;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Notificacoes;
using Maxx.Domain.Premios;
using Maxx.Infrastructure.Conquistas;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.RaridadeDosItens;
using Microsoft.Extensions.DependencyInjection;

namespace Maxx.Api.IntegrationTests.Conquistas;

// O gatilho por fato contra o Postgres real (regras-conquista.md 5): a medalha concedida e o
// prêmio que fica definitivo, pelo fim da janela ou pela confirmação da contestação,
// enfileiram a reavaliação, e o worker concede, grava evidências e progresso, notifica, e a
// conquista passa a pontuar e a contar na raridade.
public sealed class ReavaliacaoDeConquistasTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Quinta_medalha_no_bimestre_concede_C01_com_as_cinco_evidencias_e_notifica()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        var medalhas = await mundo.CincoNoBimestreAsync(mundo.Ana);

        await mundo.EsvaziarAsync();

        var conquista = Assert.Single(await mundo.LinhasAsync<Conquista>());
        Assert.Equal((mundo.Ana.Id, "C01", Tier.Bronze, 25), (conquista.AlunoId, conquista.ItemCodigo, conquista.Tier, conquista.Pontos));
        Assert.Equal(new InstanciaDePeriodo(Periodo.Bimestre, mundo.BimestreDeHoje.Id), conquista.Instancia);
        Assert.Equal(mundo.Relogio.GetUtcNow(), conquista.ConcedidaEm);
        Assert.True(conquista.EstaAtiva);

        var evidencias = await mundo.LinhasAsync<EvidenciaDaConquista>();
        Assert.All(evidencias, evidencia => Assert.Equal(conquista.Id, evidencia.ConquistaId));
        Assert.Equal(medalhas.Order(), evidencias.Select(evidencia => evidencia.MedalhaId!.Value).Order());

        var notificacao = Assert.Single(
            await mundo.LinhasAsync<Notificacao>(), linha => linha.Tipo == TipoDeNotificacao.ConquistaObtida);
        Assert.Equal((mundo.Ana.Id, conquista.Id), (notificacao.UsuarioId, notificacao.ReferenciaId));
    }

    [Fact]
    public async Task Abaixo_do_alvo_nada_e_concedido_e_o_progresso_fica_gravado()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        foreach (var item in new[] { "M01", "M01", "M02", "M02" })
        {
            await mundo.ConcederAsync(mundo.Ana, item);
        }

        await mundo.EsvaziarAsync();

        Assert.Empty(await mundo.LinhasAsync<Conquista>());
        var progresso = (await mundo.LinhasAsync<ProgressoDeConquistaRow>()).ToDictionary(linha => linha.ItemCodigo);
        Assert.Equal((4, 5, "bimestre", mundo.BimestreDeHoje.Id), Linha(progresso["C01"]));
        Assert.Equal((2, 3, "bimestre", mundo.BimestreDeHoje.Id), Linha(progresso["C02"]));
        Assert.Equal((4, 10, "semestre", mundo.Semestre1.Id), Linha(progresso["C07"]));
        Assert.Equal((2, 5, "semestre", mundo.Semestre1.Id), Linha(progresso["C08"]));
        Assert.Equal((4, 20, "ano", mundo.Ano.Id), Linha(progresso["C12"]));
        Assert.Equal((4, 50, "curso", mundo.Ana.Id), Linha(progresso["C16"]));

        // Nota 10 em tudo no bimestre: conta por matéria, e a Ana tem duas.
        Assert.Equal((1, 2, "bimestre", mundo.BimestreDeHoje.Id), Linha(progresso["C04"]));
    }

    [Fact]
    public async Task Conquista_de_fechamento_so_ganha_progresso_mesmo_com_o_alvo_alcancado()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.ConcederAsync(mundo.Ana, "M03", mundo.BancoDeDados);
        await mundo.ConcederAsync(mundo.Ana, "M03", mundo.Redes);

        await mundo.EsvaziarAsync();

        Assert.Empty(await mundo.LinhasAsync<Conquista>());
        var c03 = Assert.Single(await mundo.LinhasAsync<ProgressoDeConquistaRow>(), linha => linha.ItemCodigo == "C03");
        Assert.Equal((2, 2), (c03.Contagem, c03.Alvo));
    }

    [Fact]
    public async Task Conquista_pontua_no_perfil_e_em_trofeus_e_entra_na_raridade()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.CincoNoBimestreAsync(mundo.Ana);

        await mundo.EsvaziarAsync();
        mundo.Relogio.Avancar(TimeSpan.FromSeconds(11));
        await mundo.EnfileirarAsync(new RecalcularRaridade());
        await mundo.EsvaziarAsync();

        // Duas M01 de 30, duas M02 de 25 e uma M03 de 25, mais a C01 de 25.
        const int esperado = 30 + 30 + 25 + 25 + 25 + 25;
        await using var scope = mundo.Factory.Services.CreateAsyncScope();
        var premios = await scope.ServiceProvider.GetRequiredService<IConsultaDePremios>().DoAlunoAsync(mundo.Ana.Id, Ct);
        var perfil = await scope.ServiceProvider.GetRequiredService<IConsultaDePerfil>().PerfilAsync(mundo.Ana.Id, mundo.Bruno.Id, Ct);
        Assert.Equal(esperado, premios!.Pontos);
        Assert.Equal(esperado, perfil!.Pontos);

        var raridade = Assert.Single(await mundo.LinhasAsync<RaridadeDoItemRow>(), linha => linha.ItemCodigo == "C01");
        Assert.Equal((1, 2), (raridade.AlunosComItem, raridade.AlunosElegiveis));
    }

    [Fact]
    public async Task Trabalho_que_roda_de_novo_nao_concede_outra_vez()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        var medalhas = await mundo.CincoNoBimestreAsync(mundo.Ana);
        await mundo.EsvaziarAsync();

        await mundo.EnfileirarAsync(ReavaliarConquistas.PelaMedalha(mundo.Ana.Id, medalhas[^1]));
        await mundo.EnfileirarAsync(ReavaliarConquistas.PelaMedalha(mundo.Ana.Id, medalhas[0]));
        await mundo.EsvaziarAsync();

        Assert.Single(await mundo.LinhasAsync<Conquista>());
        Assert.Single(await mundo.LinhasAsync<Notificacao>(), linha => linha.Tipo == TipoDeNotificacao.ConquistaObtida);
        AssertNenhumTrabalhoFalhou(await mundo.LinhasAsync<Trabalho>());
    }

    [Fact]
    public async Task Dois_trabalhos_simultaneos_do_mesmo_aluno_concedem_uma_conquista_sem_falhar()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.CincoNoBimestreAsync(mundo.Ana);

        // As cinco reavaliações (e os outros trabalhos da concessão) consumidas por dois
        // consumidores ao mesmo tempo: cada reavaliação já vê as cinco medalhas, e todas
        // tentariam conceder a C01.
        var consumidor = mundo.Factory.Services.GetRequiredService<ConsumidorDeTrabalhos>();
        async Task ConsumirAsync()
        {
            while (await consumidor.ProcessarProximoAsync(Ct))
            {
            }
        }

        await Task.WhenAll(ConsumirAsync(), ConsumirAsync(), ConsumirAsync());

        Assert.Single(await mundo.LinhasAsync<Conquista>());
        Assert.Equal(5, (await mundo.LinhasAsync<EvidenciaDaConquista>()).Count);

        // Nenhum trabalho falhou e voltou para a fila: a trava por aluno serializou as
        // avaliações, e o índice único nem chegou a ser testado.
        AssertNenhumTrabalhoFalhou(await mundo.LinhasAsync<Trabalho>());
    }

    [Fact]
    public async Task Indice_unico_recusa_a_mesma_conquista_ativa_na_mesma_instancia()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.CincoNoBimestreAsync(mundo.Ana);
        await mundo.EsvaziarAsync();
        var existente = Assert.Single(await mundo.LinhasAsync<Conquista>());

        await using var scope = mundo.Factory.Services.CreateAsyncScope();
        var repositorio = scope.ServiceProvider.GetRequiredService<IConquistaRepository>();
        var calendario = await repositorio.CalendarioAsync(Ct);
        var fatos = await repositorio.FatosAsync(mundo.Ana.Id, calendario, Ct);
        var c01 = (await repositorio.CatalogoAsync(Ct)).Single(conquista => conquista.Item.Codigo == "C01");
        var avaliacao = scope.ServiceProvider.GetRequiredService<AvaliadorDeConquista>()
            .Avaliar(c01.Item, existente.Instancia, fatos, new ContextoDaAvaliacao(mundo.BimestreDeHoje.Fim, "CC"));
        repositorio.Adicionar(Conquista.Conceder(c01.Item, c01.Versao, mundo.Ana.Id, existente.Instancia, avaliacao, mundo.Relogio.GetUtcNow()));

        var falha = await Assert.ThrowsAsync<ConflitoDeUnicidadeException>(
            () => scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SalvarAsync(Ct));
        Assert.Equal(Unicidade.ConquistaAtiva, falha.Unicidade);
    }

    [Fact]
    public async Task Medalha_anulada_antes_da_reavaliacao_nao_fecha_a_conquista()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        var medalhas = await mundo.CincoNoBimestreAsync(mundo.Ana);
        await mundo.AnularAsync(medalhas[^1]);

        await mundo.EsvaziarAsync();

        Assert.Empty(await mundo.LinhasAsync<Conquista>());
        var c01 = Assert.Single(await mundo.LinhasAsync<ProgressoDeConquistaRow>(), linha => linha.ItemCodigo == "C01");
        Assert.Equal((4, 5), (c01.Contagem, c01.Alvo));
    }

    [Fact]
    public async Task Premio_de_participacao_conta_so_quando_fica_definitivo_pelo_fim_da_janela()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        var premios = new[]
        {
            await mundo.AprovadoAsync(mundo.Ana, "P01", "Palestra de abertura"),
            await mundo.AprovadoAsync(mundo.Ana, "P02", "Doação de sangue"),
            await mundo.AprovadoAsync(mundo.Ana, "P07", "Minicurso de Git"),
        };

        // Válidos pontuam, mas ainda não contam para conquista.
        await mundo.EsvaziarAsync();
        Assert.Empty(await mundo.LinhasAsync<Conquista>());
        Assert.Empty(await mundo.LinhasAsync<ProgressoDeConquistaRow>());

        mundo.Relogio.Avancar(PremioParticipacao.JanelaDeContestacao);
        await mundo.EsvaziarAsync();

        var conquistas = await mundo.LinhasAsync<Conquista>();
        var c05 = Assert.Single(conquistas, conquista => conquista.ItemCodigo == "C05");
        var c06 = Assert.Single(conquistas, conquista => conquista.ItemCodigo == "C06");
        Assert.Equal(2, conquistas.Count);
        Assert.Equal(new InstanciaDePeriodo(Periodo.Semestre, mundo.Semestre1.Id), c05.Instancia);
        Assert.Equal(c05.Instancia, c06.Instancia);
        var evidencias = (await mundo.LinhasAsync<EvidenciaDaConquista>()).Where(evidencia => evidencia.ConquistaId == c05.Id);
        Assert.Equal(premios.Select(premio => premio.Id).Order(), evidencias.Select(evidencia => evidencia.PremioParticipacaoId!.Value).Order());
    }

    [Fact]
    public async Task Confirmacao_da_contestacao_torna_definitivo_e_reavalia_e_a_averiguacao_nao_conta()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.AprovadoAsync(mundo.Ana, "P01", "Palestra de abertura");
        await mundo.AprovadoAsync(mundo.Ana, "P03", "Workshop de testes");
        mundo.Relogio.Avancar(PremioParticipacao.JanelaDeContestacao);
        await mundo.EsvaziarAsync();

        var terceiro = await mundo.AprovadoAsync(mundo.Ana, "P07", "Minicurso de Git");
        Guid contestacaoId;
        await using (var scope = mundo.Factory.Services.CreateAsyncScope())
        {
            contestacaoId = (await scope.ServiceProvider.GetRequiredService<ContestacaoHandler>()
                .ContestarAsync(mundo.Secretaria.Id, terceiro.Id, "Revi a foto.", Ct)).Id;
        }

        await mundo.EsvaziarAsync();
        Assert.Empty(await mundo.LinhasAsync<Conquista>());
        Assert.Equal(2, Assert.Single(await mundo.LinhasAsync<ProgressoDeConquistaRow>(), linha => linha.ItemCodigo == "C05").Contagem);

        await using (var scope = mundo.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ContestacaoHandler>().ConfirmarAsync(mundo.Secretaria.Id, contestacaoId, Ct);
        }

        await mundo.EsvaziarAsync();
        var c05 = Assert.Single(await mundo.LinhasAsync<Conquista>());
        Assert.Equal("C05", c05.ItemCodigo);
        Assert.Equal(3, (await mundo.LinhasAsync<EvidenciaDaConquista>()).Count);
    }

    // O que sobra na fila é só o recálculo de raridade adiado pela coalescência de 10 s (ADR
    // 0005), nunca um trabalho que falhou e voltou com recuo.
    private static void AssertNenhumTrabalhoFalhou(List<Trabalho> trabalhos) =>
        Assert.All(trabalhos, trabalho => Assert.Equal((RecalcularRaridade.Tipo, 0), (trabalho.Tipo, trabalho.Tentativas)));

    private static (int, int, string, Guid) Linha(ProgressoDeConquistaRow linha) =>
        (linha.Contagem, linha.Alvo, linha.TipoDePeriodo, linha.ReferenciaDoPeriodo);
}
