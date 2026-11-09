using System.Text.Json;
using Maxx.Application.Conquistas;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Notificacoes;
using Maxx.Infrastructure.Conquistas;
using Maxx.Infrastructure.Fila;
using Microsoft.Extensions.DependencyInjection;

namespace Maxx.Api.IntegrationTests.Conquistas;

// O gatilho de fechamento de período contra o Postgres real (regras-conquista.md 5, D7 e D8
// do passo 9). No calendário do mundo, hoje cai no bimestre 2, que é o último do semestre 1:
// o bimestre 1 já encerrou; em 26 dias encerram o bimestre 2 e o semestre 1; o congelamento
// do semestre 1 é em 55 dias; o ano encerra em 128.
public sealed class FechamentoDePeriodoTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly TimeSpan _bimestreEncerrado = TimeSpan.FromDays(26);
    private static readonly TimeSpan _depoisDoCongelamento = TimeSpan.FromDays(60);
    private static readonly TimeSpan _anoEncerrado = TimeSpan.FromDays(130);

    [Fact]
    public async Task Verificacao_fecha_cada_instancia_encerrada_uma_vez_e_deixa_uma_proxima_verificacao()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);

        await mundo.VerificarFechamentosAsync();
        await mundo.EnfileirarAsync(new VerificarFechamentos());
        await mundo.VerificarFechamentosAsync();

        var fechada = Assert.Single(await mundo.LinhasAsync<FechamentoDePeriodoRow>());
        Assert.Equal(("bimestre", mundo.Ano.Bimestres.Single(bimestre => bimestre.Numero == 1).Id), (fechada.TipoDePeriodo, fechada.ReferenciaDoPeriodo));
        var proxima = Assert.Single(await mundo.LinhasAsync<Trabalho>());
        Assert.Equal((VerificarFechamentos.Tipo, mundo.Relogio.GetUtcNow() + TimeSpan.FromHours(1)), (proxima.Tipo, proxima.DisponivelEm));
    }

    // O tipo de período vai na carga pelo nome, não pela posição no enum: reordenar o enum
    // com trabalho pendente não muda o que o trabalho quer dizer.
    [Fact]
    public async Task Carga_do_fechamento_leva_o_tipo_de_periodo_pelo_nome()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.EnfileirarAsync(new VerificarFechamentos());

        var consumidor = mundo.Factory.Services.GetRequiredService<ConsumidorDeTrabalhos>();
        Assert.True(await consumidor.ProcessarProximoAsync(TestContext.Current.CancellationToken));
        var fechar = Assert.Single(await mundo.LinhasAsync<Trabalho>(), trabalho => trabalho.Tipo == FecharPeriodo.Tipo);
        var bimestre1 = mundo.Ano.Bimestres.Single(bimestre => bimestre.Numero == 1).Id;
        using var carga = JsonDocument.Parse(fechar.Carga);
        Assert.Equal(
            [("tipoDePeriodo", "Bimestre"), ("referenciaDoPeriodo", bimestre1.ToString())],
            carga.RootElement.EnumerateObject().Select(campo => (campo.Name, campo.Value.GetString())));

        await mundo.EsvaziarAsync();
        var avaliar = new AvaliarFechamentoDoAluno(Periodo.Ano, mundo.Ano.Id, mundo.Ana.Id);
        await mundo.EnfileirarAsync(avaliar);
        var pendente = Assert.Single(await mundo.LinhasAsync<Trabalho>(), trabalho => trabalho.Tipo == AvaliarFechamentoDoAluno.Tipo);
        using var cargaPendente = JsonDocument.Parse(pendente.Carga);
        Assert.Equal("Ano", cargaPendente.RootElement.GetProperty("tipoDePeriodo").GetString());
        Assert.Equal(avaliar, JsonSerializer.Deserialize<AvaliarFechamentoDoAluno>(pendente.Carga, JsonSerializerOptions.Web));
    }

    [Fact]
    public async Task Primeira_verificacao_fecha_bimestres_semestres_e_o_ano_que_ficaram_para_tras()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        mundo.Relogio.Avancar(_anoEncerrado);

        await mundo.VerificarFechamentosAsync();

        var fechadas = (await mundo.LinhasAsync<FechamentoDePeriodoRow>()).Select(linha => (linha.TipoDePeriodo, linha.ReferenciaDoPeriodo)).ToHashSet();
        Assert.Equal(7, fechadas.Count);
        Assert.All(mundo.Ano.Bimestres, bimestre => Assert.Contains(("bimestre", bimestre.Id), fechadas));
        Assert.All(mundo.Ano.Semestres, semestre => Assert.Contains(("semestre", semestre.Id), fechadas));
        Assert.Contains(("ano", mundo.Ano.Id), fechadas);
    }

    [Fact]
    public async Task C03_e_concedida_no_fechamento_do_bimestre_com_presenca_em_todas_as_materias()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.ConcederAsync(mundo.Ana, "M03", mundo.BancoDeDados);
        await mundo.ConcederAsync(mundo.Ana, "M03", mundo.Redes);
        await mundo.EsvaziarAsync();
        Assert.Empty(await mundo.LinhasAsync<Conquista>());

        mundo.Relogio.Avancar(_bimestreEncerrado);
        await mundo.VerificarFechamentosAsync();

        var c03 = Assert.Single(await mundo.LinhasAsync<Conquista>());
        Assert.Equal((mundo.Ana.Id, "C03"), (c03.AlunoId, c03.ItemCodigo));
        Assert.Equal(new InstanciaDePeriodo(Periodo.Bimestre, mundo.BimestreDeHoje.Id), c03.Instancia);
        Assert.Equal(2, (await mundo.LinhasAsync<EvidenciaDaConquista>()).Count);
        Assert.Single(await mundo.LinhasAsync<Notificacao>(), linha => linha.Tipo == TipoDeNotificacao.ConquistaObtida);
    }

    [Fact]
    public async Task Uma_materia_sem_presenca_integral_nao_concede_a_C03_e_o_progresso_fica()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.ConcederAsync(mundo.Ana, "M03", mundo.BancoDeDados);

        mundo.Relogio.Avancar(_bimestreEncerrado);
        await mundo.VerificarFechamentosAsync();

        Assert.Empty(await mundo.LinhasAsync<Conquista>());
        var c03 = Assert.Single(
            await mundo.LinhasAsync<ProgressoDeConquistaRow>(),
            linha => linha.AlunoId == mundo.Ana.Id && linha.ItemCodigo == "C03" && linha.ReferenciaDoPeriodo == mundo.BimestreDeHoje.Id);

        // O Bruno também está matriculado no período, sem nenhuma presença integral: 0 de 1.
        var doBruno = Assert.Single(
            await mundo.LinhasAsync<ProgressoDeConquistaRow>(),
            linha => linha.AlunoId == mundo.Bruno.Id && linha.ItemCodigo == "C03" && linha.ReferenciaDoPeriodo == mundo.BimestreDeHoje.Id);
        Assert.Equal((0, 1), (doBruno.Contagem, doBruno.Alvo));
        Assert.Equal((1, 2), (c03.Contagem, c03.Alvo));
    }

    [Fact]
    public async Task C10_no_fechamento_do_semestre_e_C15_no_fechamento_do_ano()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.ConcederAsync(mundo.Ana, "M11", mundo.BancoDeDados);
        await mundo.ConcederAsync(mundo.Ana, "M11", mundo.Redes);

        // A Ana só tem matrícula no semestre 1: as duas matérias são todas as do ano.
        mundo.Relogio.Avancar(_anoEncerrado);
        await mundo.VerificarFechamentosAsync();

        var porItem = (await mundo.LinhasAsync<Conquista>()).ToDictionary(conquista => conquista.ItemCodigo);
        Assert.Equal(["C10", "C15"], porItem.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(new InstanciaDePeriodo(Periodo.Semestre, mundo.Semestre1.Id), porItem["C10"].Instancia);
        Assert.Equal(new InstanciaDePeriodo(Periodo.Ano, mundo.Ano.Id), porItem["C15"].Instancia);
    }

    [Fact]
    public async Task Fechar_o_mesmo_periodo_duas_vezes_nao_concede_de_novo()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.ConcederAsync(mundo.Ana, "M03", mundo.BancoDeDados);
        await mundo.ConcederAsync(mundo.Ana, "M03", mundo.Redes);
        mundo.Relogio.Avancar(_bimestreEncerrado);
        await mundo.VerificarFechamentosAsync();

        await mundo.EnfileirarAsync(new FecharPeriodo(Periodo.Bimestre, mundo.BimestreDeHoje.Id));
        await mundo.EsvaziarAsync();

        Assert.Single(await mundo.LinhasAsync<Conquista>());
        Assert.Single(await mundo.LinhasAsync<Notificacao>(), linha => linha.Tipo == TipoDeNotificacao.ConquistaObtida);
    }

    [Fact]
    public async Task Primeiro_fechamento_concede_mesmo_depois_do_congelamento()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.ConcederAsync(mundo.Ana, "M03", mundo.BancoDeDados);
        await mundo.ConcederAsync(mundo.Ana, "M03", mundo.Redes);
        await mundo.EsvaziarAsync();

        mundo.Relogio.Avancar(_depoisDoCongelamento);
        await mundo.VerificarFechamentosAsync();

        Assert.Equal("C03", Assert.Single(await mundo.LinhasAsync<Conquista>()).ItemCodigo);
    }

    // O fato que fica contável numa instância já fechada (o trabalho dele chegou depois do
    // fechamento) concede a conquista de fechamento até o congelamento, e não depois.
    [Theory]
    [InlineData(26, true)]
    [InlineData(54, true)]
    [InlineData(55, false)]
    [InlineData(60, false)]
    public async Task Fato_numa_instancia_ja_fechada_concede_so_antes_do_congelamento(int dias, bool concede)
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.ConcederAsync(mundo.Ana, "M03", mundo.BancoDeDados);
        var redes = await mundo.ConcederAsync(mundo.Ana, "M03", mundo.Redes);
        await mundo.EsvaziarAsync();
        Assert.Empty(await mundo.LinhasAsync<Conquista>());

        mundo.Relogio.Avancar(TimeSpan.FromDays(dias));
        await mundo.RegistrarFechamentoAsync(new InstanciaDePeriodo(Periodo.Bimestre, mundo.BimestreDeHoje.Id));
        await mundo.EnfileirarAsync(ReavaliarConquistas.PelaMedalha(mundo.Ana.Id, redes));
        await mundo.EsvaziarAsync();

        Assert.Equal(concede, (await mundo.LinhasAsync<Conquista>()).Any(conquista => conquista.ItemCodigo == "C03"));
    }

    [Fact]
    public async Task Fato_numa_instancia_ainda_aberta_so_atualiza_o_progresso_da_conquista_de_fechamento()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.ConcederAsync(mundo.Ana, "M03", mundo.BancoDeDados);
        var redes = await mundo.ConcederAsync(mundo.Ana, "M03", mundo.Redes);
        await mundo.EsvaziarAsync();

        // Encerrada pela data, mas sem o registro do fechamento: quem diz que fechou é o worker.
        mundo.Relogio.Avancar(_bimestreEncerrado);
        await mundo.EnfileirarAsync(ReavaliarConquistas.PelaMedalha(mundo.Ana.Id, redes));
        await mundo.EsvaziarAsync();

        Assert.Empty(await mundo.LinhasAsync<Conquista>());
    }
}
