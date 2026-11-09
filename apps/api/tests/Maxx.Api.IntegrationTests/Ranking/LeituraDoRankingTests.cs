using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Maxx.Api.IntegrationTests.Conquistas;
using Maxx.Api.Perfil;
using Maxx.Api.Ranking;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Domain.Ranking;
using Maxx.Domain.Social;
using Maxx.Infrastructure.Autenticacao;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Ranking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.BancoDeTeste;

namespace Maxx.Api.IntegrationTests.Ranking;

// A leitura do ranking pela API (produto.md 5): só lê a tabela do worker (ADR 0005), e aqui
// as linhas são gravadas direto nela, sem prêmio nenhum por trás. Quem vê o quê segue a
// pergunta 6 do passo 11; a posição do perfil, a pergunta 4.
public sealed class LeituraDoRankingTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string _ana = "ana.souza@al.unieduk.com.br";
    private const string _bruno = "bruno.lima@al.unieduk.com.br";
    private const string _professora = "marta@prof.unieduk.com.br";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Aluno_le_o_topo_e_a_propria_linha_do_semestre_atual()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        using var ana = await mundo.ComoAsync(_ana);
        await PosicoesAsync(mundo, Geral(mundo.Semestre1.Id), (mundo.Bruno, 120), (mundo.Ana, 60));

        var ranking = await RankingAsync(ana, "geral", "semestre");

        Assert.Equal(("geral", "semestre", mundo.Semestre1.Codigo, (string?)null), (ranking.Escopo, ranking.Periodo, ranking.Semestre, ranking.Curso));
        Assert.Equal([(mundo.Bruno.Id, 1, 120), (mundo.Ana.Id, 2, 60)], ranking.Itens.Select(linha => (linha.AlunoId, linha.Posicao, linha.Pontos)));
        Assert.Equal((mundo.Ana.Id, 2, 60, "Ana", "@anaSouza-CC", "CC", "Ciência da Computação", 3, false), Resumo(ranking.Eu!));
        Assert.Null(ranking.Proximo);
    }

    [Fact]
    public async Task Topo_vem_por_cursor_de_vinte_em_vinte_sem_repetir_nem_pular()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        using var ana = await mundo.ComoAsync(_ana);
        var outros = await AlunosAsync(mundo, 23);
        await PosicoesAsync(
            mundo,
            Historico(),
            [.. outros.Select((aluno, indice) => (aluno, 1000 - indice)), (mundo.Ana, 10)]);

        var primeira = await RankingAsync(ana, "geral", "historico");
        var segunda = await RankingAsync(ana, "geral", "historico", primeira.Proximo);

        Assert.Equal(Enumerable.Range(1, 20), primeira.Itens.Select(linha => linha.Posicao));
        Assert.Equal(Enumerable.Range(21, 4), segunda.Itens.Select(linha => linha.Posicao));
        Assert.Null(segunda.Proximo);
        Assert.Equal(24, primeira.Eu!.Posicao);
    }

    [Fact]
    public async Task Quem_eu_sigo_filtra_a_mesma_visao_com_a_posicao_de_todos_e_a_minha_linha()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        using var ana = await mundo.ComoAsync(_ana);
        var outros = await AlunosAsync(mundo, 2);
        await SalvarAsync(mundo.Factory, Seguimento.Seguir(mundo.Ana, outros[1], professorDoSeguido: false, mundo.Relogio.GetUtcNow())!);
        await PosicoesAsync(mundo, Historico(), (outros[0], 300), (outros[1], 200), (mundo.Ana, 100), (mundo.Bruno, 50));

        var ranking = await RankingAsync(ana, "geral", "historico", seguidos: true);

        Assert.Equal([(outros[1].Id, 2), (mundo.Ana.Id, 3)], ranking.Itens.Select(linha => (linha.AlunoId, linha.Posicao)));
    }

    [Fact]
    public async Task Ranking_do_curso_e_o_do_curso_atual_do_aluno()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        using var ana = await mundo.ComoAsync(_ana);
        await PosicoesAsync(mundo, Curso(mundo.Semestre1.Id, mundo.Cc.Id), (mundo.Ana, 60));
        await PosicoesAsync(mundo, Geral(mundo.Semestre1.Id), (mundo.Bruno, 90), (mundo.Ana, 60));

        var ranking = await RankingAsync(ana, "curso", "semestre");

        Assert.Equal(("curso", "CC", "Ciência da Computação"), (ranking.Escopo, ranking.Curso, ranking.NomeDoCurso));
        Assert.Equal([(mundo.Ana.Id, 1)], ranking.Itens.Select(linha => (linha.AlunoId, linha.Posicao)));
        Assert.Equal(1, ranking.Eu!.Posicao);
    }

    [Fact]
    public async Task Aluno_sem_pontos_na_visao_le_o_topo_sem_a_propria_linha()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        using var ana = await mundo.ComoAsync(_ana);
        await PosicoesAsync(mundo, Geral(mundo.Semestre1.Id), (mundo.Bruno, 30));

        var ranking = await RankingAsync(ana, "geral", "semestre");

        Assert.Null(ranking.Eu);
        Assert.Single(ranking.Itens);
    }

    [Fact]
    public async Task Professor_ve_o_geral_sem_linha_propria_e_nao_ve_o_curso()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await ProfessoraComSenhaAsync(mundo);
        using var marta = await mundo.ComoAsync(_professora);
        await PosicoesAsync(mundo, Historico(), (mundo.Ana, 60));

        var geral = await RankingAsync(marta, "geral", "historico");
        var curso = await marta.GetAsync(Rota("curso", "historico"), Ct);

        Assert.Null(geral.Eu);
        Assert.Single(geral.Itens);
        await AssertRecusaAsync(curso, HttpStatusCode.Forbidden, "ranking-de-curso-so-para-aluno");
    }

    [Fact]
    public async Task Ex_aluno_ve_o_historico_do_ultimo_curso_e_nao_o_semestre_do_curso()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        using var bruno = await mundo.ComoAsync(_bruno);
        await ExecutarAsync(mundo, async db =>
            (await db.Set<Usuario>().SingleAsync(usuario => usuario.Id == mundo.Bruno.Id, Ct)).MarcarExAluno(mundo.Relogio.GetUtcNow()));
        await PosicoesAsync(mundo, Curso(null, mundo.Cc.Id), (mundo.Bruno, 60));

        var historico = await RankingAsync(bruno, "curso", "historico");
        var semestre = await bruno.GetAsync(Rota("curso", "semestre"), Ct);
        var geral = await RankingAsync(bruno, "geral", "semestre");

        Assert.True(historico.Eu!.ExAluno);
        Assert.Equal("@brunoLima-EX", historico.Eu.Arroba);
        await AssertRecusaAsync(semestre, HttpStatusCode.Forbidden, "ex-aluno-so-no-historico");
        Assert.Null(geral.Eu);
    }

    [Theory]
    [InlineData("escopo=turma&periodo=semestre")]
    [InlineData("escopo=geral&periodo=ano")]
    [InlineData("periodo=semestre")]
    public async Task Visao_que_nao_existe_e_400(string consulta)
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        using var ana = await mundo.ComoAsync(_ana);

        var resposta = await ana.GetAsync(new Uri($"/api/v1/rankings?{consulta}", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Theory]
    [InlineData("xyz")]
    [InlineData("ci4w")]
    public async Task Cursor_que_a_api_nao_emitiu_e_400(string cursor)
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        using var ana = await mundo.ComoAsync(_ana);

        var resposta = await ana.GetAsync(Rota("geral", "historico", cursor), Ct);

        await AssertRecusaAsync(resposta, HttpStatusCode.BadRequest, "cursor-invalido");
    }

    [Fact]
    public async Task Perfil_mostra_a_posicao_do_historico_geral_e_nula_sem_pontos()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        using var ana = await mundo.ComoAsync(_ana);
        await PosicoesAsync(mundo, Historico(), (mundo.Bruno, 90), (mundo.Ana, 60));
        await PosicoesAsync(mundo, Geral(mundo.Semestre1.Id), (mundo.Ana, 60));

        var meu = await ana.GetFromJsonAsync<PerfilResponse>(new Uri("/api/v1/eu/perfil", UriKind.Relative), Ct);
        var doBruno = await ana.GetFromJsonAsync<PerfilResponse>(new Uri($"/api/v1/perfis/{mundo.Bruno.Id}", UriKind.Relative), Ct);

        Assert.Equal(2, meu!.Posicao);
        Assert.Equal(1, doBruno!.Posicao);

        await ExecutarAsync(mundo, db => db.Database.ExecuteSqlAsync($"DELETE FROM posicao_no_ranking", Ct));
        Assert.Null((await ana.GetFromJsonAsync<PerfilResponse>(new Uri("/api/v1/eu/perfil", UriKind.Relative), Ct))!.Posicao);
    }

    [Fact]
    public async Task Leitura_da_pagina_usa_o_indice_da_visao()
    {
        await using var mundo = await MundoDeConquistas.CriarAsync(postgres);
        await using var scope = mundo.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();

        // Sem estatística, numa tabela vazia, o planejador prefere varrer; desligar a
        // varredura sequencial mostra se o índice atende a consulta.
        await db.Database.ExecuteSqlAsync($"SET enable_seqscan = off", Ct);
        var plano = await db.Database.SqlQuery<string>($"""
            EXPLAIN SELECT * FROM posicao_no_ranking
            WHERE escopo = 'geral' AND periodo = 'semestre' AND semestre_letivo_id = {mundo.Semestre1.Id} AND curso_id IS NULL AND posicao > 0
            ORDER BY posicao LIMIT 21
            """).ToListAsync(Ct);

        Assert.Contains(plano, linha => linha.Contains("ix_posicao_no_ranking_visao_posicao", StringComparison.Ordinal));
    }

    private static Uri Rota(string escopo, string periodo, string? cursor = null, bool seguidos = false) =>
        new($"/api/v1/rankings?escopo={escopo}&periodo={periodo}{(cursor is null ? string.Empty : $"&cursor={cursor}")}{(seguidos ? "&seguidos=true" : string.Empty)}", UriKind.Relative);

    private static async Task<RankingResponse> RankingAsync(HttpClient client, string escopo, string periodo, string? cursor = null, bool seguidos = false)
    {
        var resposta = await client.GetAsync(Rota(escopo, periodo, cursor, seguidos), Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<RankingResponse>(Ct))!;
    }

    private static (Guid, int, int, string, string, string?, string?, int?, bool) Resumo(LinhaDoRankingResponse linha) =>
        (linha.AlunoId, linha.Posicao, linha.Pontos, linha.NomeDeExibicao, linha.Arroba, linha.Curso, linha.NomeDoCurso, linha.Semestre, linha.ExAluno);

    private static VisaoDoRanking Geral(Guid semestre) => new(EscopoDoRanking.Geral, PeriodoDoRanking.Semestre, semestre, null);

    private static VisaoDoRanking Historico() => new(EscopoDoRanking.Geral, PeriodoDoRanking.Historico, null, null);

    private static VisaoDoRanking Curso(Guid? semestre, Guid curso) =>
        new(EscopoDoRanking.Curso, semestre is null ? PeriodoDoRanking.Historico : PeriodoDoRanking.Semestre, semestre, curso);

    // Grava a visão como o worker gravaria: na ordem dada, posições de 1 em diante.
    private static Task PosicoesAsync(MundoDeConquistas mundo, VisaoDoRanking visao, params (Usuario Aluno, int Pontos)[] linhas) =>
        SalvarAsync(
            mundo.Factory,
            [.. linhas.Select((linha, indice) => (object)PosicaoNoRankingRow.De(
                new LinhaDoRanking(visao, linha.Aluno.Id, indice + 1, linha.Pontos, mundo.Relogio.GetUtcNow())))]);

    // Alunos de CC com conta, sem senha: só aparecem no ranking.
    private static async Task<List<Usuario>> AlunosAsync(MundoDeConquistas mundo, int quantos)
    {
        List<object> entidades = [];
        List<Usuario> alunos = [];
        for (var i = 0; i < quantos; i++)
        {
            var cadastro = CadastroElegivel.Aluno($"2026{i:D4}9", $"aluno{i}@al.unieduk.com.br", $"Aluno {i}", mundo.Cc, 1);
            var usuario = Usuario.CadastrarAluno(cadastro, mundo.Cc, $"Aluno {i}", $"aluno{i}x", "hash", mundo.Relogio.GetUtcNow());
            entidades.Add(cadastro);
            entidades.Add(usuario);
            alunos.Add(usuario);
        }

        await SalvarAsync(mundo.Factory, [.. entidades]);
        return alunos;
    }

    private static async Task ProfessoraComSenhaAsync(MundoDeConquistas mundo)
    {
        var cadastro = CadastroElegivel.Professor(_professora, "Marta Lopes");
        var usuario = Usuario.CriarPeloSeed(cadastro, "martaL", mundo.Relogio.GetUtcNow());
        usuario.DefinirSenha(new Argon2idSenhaHasher().Hash(MundoDeConquistas.Senha));
        await SalvarAsync(mundo.Factory, cadastro, usuario);
    }

    private static async Task ExecutarAsync(MundoDeConquistas mundo, Func<MaxxDbContext, Task> acao)
    {
        await using var scope = mundo.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        await acao(db);
        await db.SaveChangesAsync(Ct);
    }

    private static async Task AssertRecusaAsync(HttpResponseMessage resposta, HttpStatusCode status, string codigo)
    {
        Assert.Equal(status, resposta.StatusCode);
        using var corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(Ct));
        Assert.Equal(codigo, corpo.RootElement.GetProperty("codigo").GetString());
    }
}
