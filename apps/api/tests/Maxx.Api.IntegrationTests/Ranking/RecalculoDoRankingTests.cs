using Maxx.Api.IntegrationTests.Conquistas;
using Maxx.Application.Ranking;
using Maxx.Domain.Academico;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Domain.Ranking;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Ranking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.BancoDeTeste;

namespace Maxx.Api.IntegrationTests.Ranking;

// O worker que reescreve as quatro visões do ranking, contra o Postgres real (ADR 0005):
// soma, ordem, semestre de cada fonte, ex-aluno só no histórico, curso atual, congelamento
// e coalescência. O pedido aqui é enfileirado à mão; quem o enfileira na escrita é a etapa 3.
//
// No mundo, hoje cai no bimestre 2 do semestre 1, que termina daqui a 25 dias e congela
// daqui a 55; o semestre 2 começa daqui a 27.
public sealed class RecalculoDoRankingTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string _recalculado = "Ranking recalculado";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Quatro_visoes_somam_os_pontos_e_ordenam_cada_uma_com_posicao_propria()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        var diego = await AlunoDeOutroCursoAsync(mundo);
        await mundo.ConcederAsync(mundo.Ana, "M01");
        await mundo.ConcederAsync(mundo.Ana, "M01");
        await mundo.ConcederAsync(mundo.Bruno, "M01");
        await mundo.AprovadoAsync(diego.Usuario, "P01", "Palestra de abertura");

        await RecalcularAsync(mundo);

        var linhas = await mundo.LinhasAsync<PosicaoNoRankingRow>();
        var semestre = mundo.Semestre1.Id;
        Assert.Equal(
            [(mundo.Ana.Id, 1, 60), (mundo.Bruno.Id, 2, 30), (diego.Usuario.Id, 3, 10)],
            Visao(linhas, EscopoDoRanking.Geral, PeriodoDoRanking.Semestre, semestre, null));
        Assert.Equal(
            [(mundo.Ana.Id, 1, 60), (mundo.Bruno.Id, 2, 30)],
            Visao(linhas, EscopoDoRanking.Curso, PeriodoDoRanking.Semestre, semestre, mundo.Cc.Id));
        Assert.Equal(
            [(diego.Usuario.Id, 1, 10)],
            Visao(linhas, EscopoDoRanking.Curso, PeriodoDoRanking.Semestre, semestre, diego.Curso.Id));
        Assert.Equal(
            Visao(linhas, EscopoDoRanking.Geral, PeriodoDoRanking.Semestre, semestre, null),
            Visao(linhas, EscopoDoRanking.Geral, PeriodoDoRanking.Historico, null, null));
        Assert.Equal(
            [(mundo.Ana.Id, 1, 60), (mundo.Bruno.Id, 2, 30)],
            Visao(linhas, EscopoDoRanking.Curso, PeriodoDoRanking.Historico, null, mundo.Cc.Id));

        // Aluno sem pontos não tem linha, e o semestre 2 ainda não tem pontos de ninguém.
        Assert.Equal(12, linhas.Count);
        Assert.DoesNotContain(linhas, linha => linha.SemestreLetivoId == mundo.Ano.Semestres[1].Id);
    }

    [Fact]
    public async Task Desempate_guarda_o_instante_da_ultima_concessao_e_poe_na_frente_quem_parou_antes()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.ConcederAsync(mundo.Bruno, "M01");
        var doBruno = mundo.Relogio.GetUtcNow();
        mundo.Relogio.Avancar(TimeSpan.FromHours(1));
        await mundo.ConcederAsync(mundo.Ana, "M01");

        await RecalcularAsync(mundo);

        var geral = (await mundo.LinhasAsync<PosicaoNoRankingRow>())
            .Where(linha => linha.Escopo == EscopoDoRanking.Geral && linha.Periodo == PeriodoDoRanking.Historico)
            .OrderBy(linha => linha.Posicao)
            .ToList();
        Assert.Equal([mundo.Bruno.Id, mundo.Ana.Id], geral.Select(linha => linha.AlunoId));
        Assert.Equal([doBruno, doBruno.AddHours(1)], geral.Select(linha => linha.UltimaConcessaoEm));
    }

    [Fact]
    public async Task Conquista_pontua_e_medalha_anulada_e_premio_em_averiguacao_nao()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);

        // As cinco medalhas do Bruno fecham a C01 (e o que mais o catálogo der por elas). A
        // medalha da Ana é anulada, e o prêmio dela é contestado e vai a averiguação.
        await mundo.CincoNoBimestreAsync(mundo.Bruno);
        var anulada = await mundo.ConcederAsync(mundo.Ana, "M01");
        var premio = await mundo.AprovadoAsync(mundo.Ana, "P01", "Palestra de abertura");
        await mundo.EsvaziarAsync();
        await mundo.AnularAsync(anulada);
        await ContestarAsync(mundo, premio.Id);

        await RecalcularAsync(mundo);

        var conquistas = (await mundo.LinhasAsync<Conquista>()).Where(conquista => conquista.EstaAtiva).ToList();
        Assert.Contains(conquistas, conquista => (conquista.AlunoId, conquista.ItemCodigo) == (mundo.Bruno.Id, "C01"));
        var esperados = (await mundo.LinhasAsync<Medalha>()).Where(medalha => medalha.AlunoId == mundo.Bruno.Id).Sum(medalha => medalha.Pontos)
            + conquistas.Where(conquista => conquista.AlunoId == mundo.Bruno.Id).Sum(conquista => conquista.Pontos);
        var historico = (await mundo.LinhasAsync<PosicaoNoRankingRow>())
            .Where(linha => linha.Escopo == EscopoDoRanking.Geral && linha.Periodo == PeriodoDoRanking.Historico)
            .ToList();
        Assert.Equal([(mundo.Bruno.Id, 1, esperados)], historico.Select(linha => (linha.AlunoId, linha.Posicao, linha.Pontos)));
    }

    [Fact]
    public async Task Premio_conta_no_semestre_do_inicio_do_evento_e_nao_no_da_aprovacao()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        var inicioDoEvento = FusoHorario.DataLocal(mundo.Relogio.GetUtcNow());

        // Aprovado já no semestre 2, antes do congelamento do semestre 1.
        mundo.Relogio.Avancar(TimeSpan.FromDays(30));
        await mundo.AprovadoAsync(mundo.Ana, "P01", "Palestra de abertura", inicioDoEvento);

        await RecalcularAsync(mundo);

        var linhas = await mundo.LinhasAsync<PosicaoNoRankingRow>();
        Assert.Single(linhas, linha => linha.Periodo == PeriodoDoRanking.Semestre
            && linha.Escopo == EscopoDoRanking.Geral && linha.SemestreLetivoId == mundo.Semestre1.Id && linha.Pontos == 10);
        Assert.DoesNotContain(linhas, linha => linha.SemestreLetivoId == mundo.Ano.Semestres[1].Id);
    }

    [Fact]
    public async Task Semestre_congelado_fica_como_estava_e_a_aprovacao_posterior_entra_so_no_historico()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        var inicioDoEvento = FusoHorario.DataLocal(mundo.Relogio.GetUtcNow());
        await mundo.ConcederAsync(mundo.Ana, "M01");
        await RecalcularAsync(mundo);
        var antes = Semestre1(await mundo.LinhasAsync<PosicaoNoRankingRow>(), mundo);

        // O semestre 1 termina daqui a 25 dias e congela no fim mais 30.
        mundo.Relogio.Avancar(TimeSpan.FromDays(55));
        await mundo.AprovadoAsync(mundo.Bruno, "P01", "Palestra de abertura", inicioDoEvento);
        await RecalcularAsync(mundo);

        var linhas = await mundo.LinhasAsync<PosicaoNoRankingRow>();
        Assert.Equal(antes, Semestre1(linhas, mundo));
        Assert.Contains(linhas, linha => linha.AlunoId == mundo.Bruno.Id && linha.Periodo == PeriodoDoRanking.Historico);
        Assert.DoesNotContain(linhas, linha => linha.AlunoId == mundo.Bruno.Id && linha.Periodo == PeriodoDoRanking.Semestre);
    }

    [Fact]
    public async Task Na_vespera_do_congelamento_o_semestre_ainda_e_reescrito()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        var inicioDoEvento = FusoHorario.DataLocal(mundo.Relogio.GetUtcNow());
        await RecalcularAsync(mundo);

        mundo.Relogio.Avancar(TimeSpan.FromDays(54));
        await mundo.AprovadoAsync(mundo.Bruno, "P01", "Palestra de abertura", inicioDoEvento);
        await RecalcularAsync(mundo);

        Assert.Contains(
            await mundo.LinhasAsync<PosicaoNoRankingRow>(),
            linha => linha.AlunoId == mundo.Bruno.Id && linha.SemestreLetivoId == mundo.Semestre1.Id);
    }

    [Fact]
    public async Task Ex_aluno_sai_do_semestre_e_fica_no_historico()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.ConcederAsync(mundo.Ana, "M01");
        await mundo.ConcederAsync(mundo.Bruno, "M01");
        await RecalcularAsync(mundo);

        await ExecutarAsync(mundo, async db =>
            (await db.Set<Usuario>().SingleAsync(usuario => usuario.Id == mundo.Bruno.Id, Ct)).MarcarExAluno(mundo.Relogio.GetUtcNow()));
        mundo.Relogio.Avancar(TimeSpan.FromSeconds(10));
        await RecalcularAsync(mundo);

        var doBruno = (await mundo.LinhasAsync<PosicaoNoRankingRow>()).Where(linha => linha.AlunoId == mundo.Bruno.Id).ToList();
        Assert.Equal(
            [(EscopoDoRanking.Geral, PeriodoDoRanking.Historico), (EscopoDoRanking.Curso, PeriodoDoRanking.Historico)],
            doBruno.Select(linha => (linha.Escopo, linha.Periodo)).Order());
    }

    [Fact]
    public async Task Quem_troca_de_curso_leva_os_pontos_para_o_curso_novo()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        var diego = await AlunoDeOutroCursoAsync(mundo);
        await mundo.ConcederAsync(mundo.Ana, "M01");
        await RecalcularAsync(mundo);

        // A troca de curso pela área da instituição chega no passo 12; aqui, o cadastro muda
        // direto no banco.
        await ExecutarAsync(mundo, db => db.Database.ExecuteSqlAsync(
            $"UPDATE cadastro_elegivel SET curso_id = {diego.Curso.Id} WHERE id = {mundo.Ana.CadastroElegivelId}", Ct));
        mundo.Relogio.Avancar(TimeSpan.FromSeconds(10));
        await RecalcularAsync(mundo);

        var daAna = (await mundo.LinhasAsync<PosicaoNoRankingRow>())
            .Where(linha => linha.AlunoId == mundo.Ana.Id && linha.Escopo == EscopoDoRanking.Curso)
            .ToList();
        Assert.Equal(2, daAna.Count);
        Assert.All(daAna, linha => Assert.Equal((diego.Curso.Id, 30), (linha.CursoId!.Value, linha.Pontos)));
    }

    [Fact]
    public async Task Recalculo_reescreve_sem_duplicar_e_tira_quem_ficou_sem_pontos()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        var medalha = await mundo.ConcederAsync(mundo.Ana, "M01");
        await mundo.ConcederAsync(mundo.Bruno, "M02");
        await RecalcularAsync(mundo);
        Assert.Equal(8, (await mundo.LinhasAsync<PosicaoNoRankingRow>()).Count);

        await mundo.AnularAsync(medalha);
        mundo.Relogio.Avancar(TimeSpan.FromSeconds(10));
        await RecalcularAsync(mundo);

        var linhas = await mundo.LinhasAsync<PosicaoNoRankingRow>();
        Assert.Equal(4, linhas.Count);
        Assert.All(linhas, linha => Assert.Equal((mundo.Bruno.Id, 1), (linha.AlunoId, linha.Posicao)));
    }

    [Fact]
    public async Task Rajada_de_pedidos_vira_um_recalculo_so()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.ConcederAsync(mundo.Ana, "M01");
        await mundo.EnfileirarAsync(new RecalcularRanking());
        await mundo.EnfileirarAsync(new RecalcularRanking());
        await mundo.EnfileirarAsync(new RecalcularRanking());

        await mundo.EsvaziarAsync();

        Assert.Single(mundo.Factory.Log.Linhas, linha => linha.Contains(_recalculado, StringComparison.Ordinal));
        Assert.DoesNotContain(await mundo.LinhasAsync<Trabalho>(), trabalho => trabalho.Tipo == RecalcularRanking.Tipo);
        Assert.Equal(4, (await mundo.LinhasAsync<PosicaoNoRankingRow>()).Count);
    }

    [Fact]
    public async Task Pedido_antes_de_dez_segundos_espera_a_janela_fechar()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.ConcederAsync(mundo.Ana, "M01");
        await RecalcularAsync(mundo);
        var primeira = mundo.Relogio.GetUtcNow();

        mundo.Relogio.Avancar(TimeSpan.FromSeconds(3));
        await mundo.ConcederAsync(mundo.Bruno, "M01");
        await RecalcularAsync(mundo);

        // Não recalculou: deixou um pedido só, para quando os dez segundos fecharem.
        Assert.Single(mundo.Factory.Log.Linhas, linha => linha.Contains(_recalculado, StringComparison.Ordinal));
        var adiado = Assert.Single(await mundo.LinhasAsync<Trabalho>(), trabalho => trabalho.Tipo == RecalcularRanking.Tipo);
        Assert.Equal(primeira + TimeSpan.FromSeconds(10), adiado.DisponivelEm);
        Assert.DoesNotContain(await mundo.LinhasAsync<PosicaoNoRankingRow>(), linha => linha.AlunoId == mundo.Bruno.Id);

        mundo.Relogio.Avancar(TimeSpan.FromSeconds(7));
        await mundo.EsvaziarAsync();

        Assert.Equal(2, mundo.Factory.Log.Linhas.Count(linha => linha.Contains(_recalculado, StringComparison.Ordinal)));
        Assert.Contains(await mundo.LinhasAsync<PosicaoNoRankingRow>(), linha => linha.AlunoId == mundo.Bruno.Id);
        Assert.DoesNotContain(await mundo.LinhasAsync<Trabalho>(), trabalho => trabalho.Tipo == RecalcularRanking.Tipo);
    }

    [Fact]
    public async Task Dois_workers_ao_mesmo_tempo_calculam_uma_vez_so()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await mundo.ConcederAsync(mundo.Ana, "M01");
        await mundo.EsvaziarAsync();
        await mundo.EnfileirarAsync(new RecalcularRanking());
        await mundo.EnfileirarAsync(new RecalcularRanking());
        var consumidor = mundo.Factory.Services.GetRequiredService<ConsumidorDeTrabalhos>();

        await Task.WhenAll(consumidor.ProcessarProximoAsync(Ct), consumidor.ProcessarProximoAsync(Ct));
        await mundo.EsvaziarAsync();

        Assert.Single(mundo.Factory.Log.Linhas, linha => linha.Contains(_recalculado, StringComparison.Ordinal));
        Assert.Equal(4, (await mundo.LinhasAsync<PosicaoNoRankingRow>()).Count);
        Assert.DoesNotContain(await mundo.LinhasAsync<Trabalho>(), trabalho => trabalho.Estado == EstadoDoTrabalho.Falho);
    }

    private static async Task RecalcularAsync(MundoDeConquistas mundo)
    {
        await mundo.EnfileirarAsync(new RecalcularRanking());
        await mundo.EsvaziarAsync();
    }

    private static List<(Guid, int, int)> Visao(
        List<PosicaoNoRankingRow> linhas, EscopoDoRanking escopo, PeriodoDoRanking periodo, Guid? semestre, Guid? curso) =>
        [.. linhas
            .Where(linha => linha.Escopo == escopo && linha.Periodo == periodo && linha.SemestreLetivoId == semestre && linha.CursoId == curso)
            .OrderBy(linha => linha.Posicao)
            .Select(linha => (linha.AlunoId, linha.Posicao, linha.Pontos))];

    private static List<(Guid, EscopoDoRanking, int, int, DateTimeOffset)> Semestre1(List<PosicaoNoRankingRow> linhas, MundoDeConquistas mundo) =>
        [.. linhas
            .Where(linha => linha.SemestreLetivoId == mundo.Semestre1.Id)
            .OrderBy(linha => linha.Escopo).ThenBy(linha => linha.Posicao)
            .Select(linha => (linha.AlunoId, linha.Escopo, linha.Posicao, linha.Pontos, linha.UltimaConcessaoEm))];

    // Diego, de Administração, com conta: o segundo curso do mundo.
    private static async Task<(Usuario Usuario, Curso Curso)> AlunoDeOutroCursoAsync(MundoDeConquistas mundo)
    {
        var adm = new Curso("Administração", "ADM", 8);
        var cadastro = CadastroElegivel.Aluno("20260104", "diego.reis@al.unieduk.com.br", "Diego Reis", adm, 3);
        var diego = Usuario.CadastrarAluno(cadastro, adm, "Diego", "diegoReis", "hash", mundo.Relogio.GetUtcNow());
        await SalvarAsync(mundo.Factory, adm, cadastro, diego);
        return (diego, adm);
    }

    private static async Task ContestarAsync(MundoDeConquistas mundo, Guid premioId) =>
        await ExecutarAsync(mundo, async db =>
        {
            var premio = await db.Set<PremioParticipacao>().SingleAsync(linha => linha.Id == premioId, Ct);
            var resultado = premio.Contestar(mundo.Secretaria, "A foto não mostra o aluno.", mundo.Relogio.GetUtcNow());
            db.Add(resultado.Contestacao!);
        });

    private static async Task ExecutarAsync(MundoDeConquistas mundo, Func<MaxxDbContext, Task> acao)
    {
        await using var scope = mundo.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        await acao(db);
        await db.SaveChangesAsync(Ct);
    }
}
