using System.Diagnostics;
using Maxx.Api.IntegrationTests.Fotos;
using Maxx.Application.Conquistas;
using Maxx.Application.Fotos;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Identidade;
using Maxx.Domain.Mural;
using Maxx.Domain.Notificacoes;
using Maxx.Domain.Premios;
using Maxx.Domain.Ranking;
using Maxx.Domain.Social;
using Maxx.Infrastructure.Conquistas;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Ranking;
using Maxx.Infrastructure.RaridadeDosItens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.BancoDeTeste;

namespace Maxx.Api.IntegrationTests;

// A atividade do seed demo (passo 12, etapa 5), contra o Postgres real e o calendário de 2026:
// medalhas, prêmios, a contestação e os avisos de data/demo/atividade.json, e as conquistas,
// que o seed não grava: nascem do worker, a partir dos fatos, com evidências e progresso. Um
// banco só para a classe inteira, porque esvaziar a fila do mundo completo leva tempo.
public sealed class SeedAtividadeTests(SeedAtividadeTests.MundoCompleto mundo) : IClassFixture<SeedAtividadeTests.MundoCompleto>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void O_seed_grava_os_fatos_e_nenhuma_conquista()
    {
        Assert.True(mundo.Resultado.MedalhasNovas > 800, $"{mundo.Resultado.MedalhasNovas} medalhas");
        Assert.True(mundo.Resultado.PremiosNovos > 250, $"{mundo.Resultado.PremiosNovos} prêmios");
        Assert.Equal(9, mundo.Resultado.AvisosNovos);
        Assert.Equal(0, mundo.ConquistasAntesDoWorker);
        Assert.True(mundo.ReavaliacoesEnfileiradas > 0);
        Assert.True(mundo.FechamentosEnfileirados > 0);
    }

    [Fact]
    public async Task O_worker_concede_as_conquistas_com_evidencias_e_nada_fica_falho()
    {
        await using var scope = mundo.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();

        var conquistas = await db.Set<Conquista>().Include(conquista => conquista.Evidencias).ToListAsync(Ct);
        Assert.True(conquistas.Count > 50, $"{conquistas.Count} conquistas");
        Assert.All(conquistas, conquista => Assert.NotEmpty(conquista.Evidencias));
        Assert.True(conquistas.Select(conquista => conquista.ItemCodigo).Distinct().Count() >= 4);
        Assert.True(await db.Set<ProgressoDeConquistaRow>().AnyAsync(Ct));
        Assert.Empty(await db.Set<Trabalho>().Where(trabalho => trabalho.Estado == EstadoDoTrabalho.Falho).ToListAsync(Ct));
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Fila esvaziada em {mundo.Drenagem.TotalSeconds:F0} s, {mundo.TrabalhosProcessados} trabalhos.");
    }

    [Fact]
    public async Task As_quatro_raridades_aparecem_em_itens_que_alguem_tem()
    {
        await using var scope = mundo.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();

        var comDono = await db.Set<RaridadeDoItemRow>().Where(raridade => raridade.AlunosComItem > 0).ToListAsync(Ct);

        Assert.Equal(
            ["comum", "epico", "lendario", "raro"],
            comDono.Select(raridade => raridade.Raridade).Distinct().Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task O_ranking_tem_os_alunos_e_o_ex_aluno_so_no_historico()
    {
        await using var scope = mundo.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var exAlunos = await db.Set<Usuario>().Where(usuario => usuario.ExAlunoDesde != null).Select(usuario => usuario.Id).ToListAsync(Ct);
        var linhas = await db.Set<PosicaoNoRankingRow>().ToListAsync(Ct);

        var historico = linhas.Where(linha => linha.Escopo == EscopoDoRanking.Geral && linha.Periodo == PeriodoDoRanking.Historico).ToList();
        Assert.True(historico.Count > 100, $"{historico.Count} no histórico geral");
        Assert.Contains(historico, linha => exAlunos.Contains(linha.AlunoId));
        Assert.DoesNotContain(linhas, linha => linha.Periodo == PeriodoDoRanking.Semestre && exAlunos.Contains(linha.AlunoId));
    }

    [Fact]
    public async Task A_contestacao_fica_aberta_e_os_avisos_chegam_pelo_worker()
    {
        await using var scope = mundo.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();

        var contestacao = Assert.Single(await db.Set<Contestacao>().ToListAsync(Ct));
        Assert.Null(contestacao.DecididaEm);
        var premio = await db.Set<PremioParticipacao>().SingleAsync(premio => premio.Id == contestacao.PremioParticipacaoId, Ct);
        Assert.Equal(EstadoDoPremioParticipacao.EmAveriguacao, premio.Estado);

        Assert.Equal(9, await db.Set<Aviso>().CountAsync(Ct));
        Assert.True(await db.Set<Notificacao>().CountAsync(notificacao => notificacao.Tipo == TipoDeNotificacao.AvisoNovo, Ct) > 100);
    }

    [Fact]
    public async Task Os_posts_com_foto_estao_no_bucket_e_as_duas_comprovacoes_esperam_na_fila()
    {
        await using var scope = mundo.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var bucket = scope.ServiceProvider.GetRequiredService<IArmazenamentoDeFotos>();

        var posts = await db.Set<Post>().ToListAsync(Ct);
        Assert.True(posts.Count >= 45, $"{posts.Count} posts");
        Assert.All(posts, post => Assert.True(post.TemFoto));
        Assert.Contains(posts, post => post.MedalhaId is not null);
        Assert.Contains(posts, post => post.PremioParticipacaoId is not null);
        Assert.Contains(posts, post => !post.TemPremio && post.TemLegenda);
        foreach (var chave in posts.SelectMany(post => post.ChavesDaFoto))
        {
            Assert.True(await bucket.ExisteAsync(chave, Ct), chave);
        }

        // As duas pendentes, de reivindicação com foto, ainda sem decisão.
        var pendentes = await db.Set<PremioParticipacao>().Where(premio => premio.Estado == EstadoDoPremioParticipacao.Reivindicado).ToListAsync(Ct);
        Assert.Equal(2, pendentes.Count);
        var comprovacoes = await db.Set<Comprovacao>().ToListAsync(Ct);
        Assert.All(pendentes, premio => Assert.Contains(comprovacoes, comprovacao => comprovacao.PremioParticipacaoId == premio.Id));
        Assert.Equal(mundo.Resultado.PostsNovos, posts.Count);
    }

    [Fact]
    public async Task Ha_seguimentos_curtidas_e_comentarios_entre_colegas()
    {
        await using var scope = mundo.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();

        Assert.True(await db.Set<Seguimento>().CountAsync(Ct) > 300);
        Assert.True(await db.Set<Curtida>().CountAsync(Ct) > 200);
        Assert.InRange(await db.Set<Comentario>().CountAsync(Ct), 30, 40);
    }

    [Fact]
    public async Task Rodar_de_novo_nao_repete_a_atividade()
    {
        var resultado = await mundo.Factory.SeedDemoAsync(Ct);

        Assert.Equal(
            (0, 0, 0, 0, 0), (resultado.ContasDeAlunoNovas, resultado.MedalhasNovas, resultado.PremiosNovos, resultado.AvisosNovos, resultado.PostsNovos));
    }

    // Seed base e demo, e a fila esvaziada pelo consumidor do worker.
    public sealed class MundoCompleto : IAsyncLifetime
    {
        private readonly PostgresFixture _postgres = new();
        private readonly ArmazenamentoFixture _armazenamento = new();

        public MaxxApiFactory Factory { get; private set; } = null!;

        public Maxx.Infrastructure.Seed.ResultadoDoSeedDemo Resultado { get; private set; } = null!;

        public int ConquistasAntesDoWorker { get; private set; }

        public int ReavaliacoesEnfileiradas { get; private set; }

        public int FechamentosEnfileirados { get; private set; }

        public TimeSpan Drenagem { get; private set; }

        public int TrabalhosProcessados { get; private set; }

        public async ValueTask InitializeAsync()
        {
            await _postgres.InitializeAsync();
            await _armazenamento.InitializeAsync();
            Factory = await MigradoAsync(_postgres, configuracao: _armazenamento.Configuracao);
            await Factory.SeedBaseAsync(CancellationToken.None);
            Resultado = await Factory.SeedDemoAsync(CancellationToken.None);

            await using (var scope = Factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
                ConquistasAntesDoWorker = await db.Set<Conquista>().CountAsync();
                ReavaliacoesEnfileiradas = await db.Set<Trabalho>().CountAsync(trabalho => trabalho.Tipo == ReavaliarConquistas.Tipo);
                FechamentosEnfileirados = await db.Set<Trabalho>().CountAsync(trabalho => trabalho.Tipo == AvaliarFechamentoDoAluno.Tipo);
            }

            // Os recálculos de ranking e raridade pedidos menos de 10 s depois do anterior ficam
            // adiados (coalescência, ADR 0005): espera a janela e esvazia de novo.
            var consumidor = Factory.Services.GetRequiredService<ConsumidorDeTrabalhos>();
            var relogio = Stopwatch.StartNew();
            for (var rodada = 0; rodada < 5; rodada++)
            {
                while (await consumidor.ProcessarProximoAsync(CancellationToken.None))
                {
                    TrabalhosProcessados++;
                }

                await using var scope = Factory.Services.CreateAsyncScope();
                var proximo = await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<Trabalho>()
                    .Where(trabalho => trabalho.Estado == EstadoDoTrabalho.Pendente && trabalho.DisponivelEm <= DateTimeOffset.UtcNow.AddSeconds(15))
                    .MinAsync(trabalho => (DateTimeOffset?)trabalho.DisponivelEm);
                if (proximo is null)
                {
                    break;
                }

                await Task.Delay(proximo.Value - DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(200));
            }

            Drenagem = relogio.Elapsed;
        }

        public async ValueTask DisposeAsync()
        {
            await Factory.DisposeAsync();
            await _armazenamento.DisposeAsync();
            await _postgres.DisposeAsync();
        }
    }
}
