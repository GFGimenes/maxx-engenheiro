using System.Text.Json.Nodes;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.BancoDeTeste;

namespace Maxx.Api.IntegrationTests;

// `seed demo` contra o Postgres real, sempre depois do base.
public sealed class SeedDemoTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>, IDisposable
{
    private readonly CopiaDeDados _copia = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _copia.Dispose();

    [Fact]
    public async Task Demo_cria_quatro_cursos_dezesseis_turmas_e_vinte_e_cinco_cadastros()
    {
        await using var factory = await ComBaseAsync();

        var resultado = await factory.SeedDemoAsync(Ct);

        // 4 cursos × 4 matérias, uma turma cada em 2026/2; 20 alunos, 4 professores, 1 instituição;
        // cada aluno nas 4 turmas do curso, cada professor nas 4 turmas do curso; conta só
        // para os 4 professores e a instituição.
        Assert.Equal(new ResultadoDoSeedDemo(4, 16, 16, 25, 80, 16, 5), resultado);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var cursos = await db.Set<Curso>().OrderBy(curso => curso.Sigla)
            .Select(curso => new ValueTuple<string, int>(curso.Sigla, curso.DuracaoEmSemestres)).ToListAsync(Ct);
        Assert.Equal([("ADM", 8), ("CC", 8), ("IA", 8), ("MED", 12)], cursos);
        Assert.Equal(1, await db.Set<CadastroElegivel>().CountAsync(cadastro => cadastro.Papeis == Papeis.Instituicao, Ct));
    }

    [Fact]
    public async Task Toda_turma_tem_professor_e_todo_aluno_tem_matricula_em_2026_2()
    {
        await using var factory = await ComBaseAsync();
        await factory.SeedDemoAsync(Ct);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var semestre = await db.Set<SemestreLetivo>().SingleAsync(semestre => semestre.Ano == 2026 && semestre.Numero == 2, Ct);

        var turmasSemProfessor = await db.Set<Turma>()
            .Where(turma => !db.Set<VinculoProfessor>().Any(vinculo => vinculo.TurmaId == turma.Id))
            .CountAsync(Ct);
        var alunosSemMatricula = await db.Set<CadastroElegivel>()
            .Where(cadastro => cadastro.Papeis == Papeis.Aluno)
            .Where(aluno => !db.Set<Matricula>().Any(matricula =>
                matricula.AlunoId == aluno.Id
                && db.Set<Turma>().Any(turma => turma.Id == matricula.TurmaId && turma.SemestreLetivoId == semestre.Id)))
            .CountAsync(Ct);

        Assert.Equal(0, turmasSemProfessor);
        Assert.Equal(0, alunosSemMatricula);
        Assert.Equal(16, await db.Set<Turma>().CountAsync(turma => turma.SemestreLetivoId == semestre.Id, Ct));
    }

    [Fact]
    public async Task Aluno_so_tem_turmas_do_proprio_curso()
    {
        await using var factory = await ComBaseAsync();
        await factory.SeedDemoAsync(Ct);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var foraDoCurso = await (
                from matricula in db.Set<Matricula>()
                join aluno in db.Set<CadastroElegivel>() on matricula.AlunoId equals aluno.Id
                join turma in db.Set<Turma>() on matricula.TurmaId equals turma.Id
                where turma.Materia.CursoId != aluno.CursoId
                select matricula.Id)
            .CountAsync(Ct);

        Assert.Equal(0, foraDoCurso);
    }

    [Fact]
    public async Task Rodar_de_novo_nao_muda_nada()
    {
        await using var factory = await ComBaseAsync();
        await factory.SeedDemoAsync(Ct);
        var antes = await ContagensAsync(factory);

        var resultado = await factory.SeedDemoAsync(Ct);

        Assert.Equal(new ResultadoDoSeedDemo(0, 0, 0, 0, 0, 0, 0), resultado);
        Assert.Equal(antes, await ContagensAsync(factory));
    }

    [Fact]
    public async Task Demo_sem_base_falha_e_explica()
    {
        await using var factory = await MigradoAsync(postgres);

        var falha = await Assert.ThrowsAsync<SeedException>(() => factory.SeedDemoAsync(Ct));

        Assert.Contains("seed base", falha.Message, StringComparison.Ordinal);
        Assert.Equal((0, 0, 0, 0, 0), await ContagensAsync(factory));
    }

    [Fact]
    public async Task Base_depois_do_demo_continua_idempotente()
    {
        await using var factory = await ComBaseAsync();
        await factory.SeedDemoAsync(Ct);

        var resultado = await factory.SeedBaseAsync(Ct);

        Assert.Equal(new ResultadoDoSeedBase(0, 0, 46, 0, 0, 0, 0, 0), resultado);
    }

    [Fact]
    public async Task Linha_que_nao_esta_no_arquivo_fica()
    {
        await using var factory = await ComBaseAsync();
        await factory.SeedDemoAsync(Ct);

        // Um cadastro de fora do arquivo, como o aluno que a banca criar no passo 5.
        await AdicionarAsync(factory, CadastroElegivel.Instituicao("outra@unieduk.com.br", "Outra Conta"));
        await factory.SeedDemoAsync(Ct);

        Assert.Equal(26, (await ContagensAsync(factory)).Cadastros);
    }

    [Fact]
    public async Task Professores_e_instituicao_ganham_conta_sem_senha_e_aluno_nenhum()
    {
        await using var factory = await ComBaseAsync();
        await factory.SeedDemoAsync(Ct);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var contas = await db.Set<Usuario>().ToListAsync(Ct);

        // O primeiro acesso é pela recuperação de senha (produto.md 2.5).
        Assert.Equal(5, contas.Count);
        Assert.All(contas, conta => Assert.False(conta.TemSenha));
        Assert.Equal(4, contas.Count(conta => conta.Papeis == Papeis.Professor && conta.Sufixo == Sufixo.Professor));
        Assert.Single(contas, conta => conta.Papeis == Papeis.Instituicao && conta.Sufixo == Sufixo.Instituicao);
        Assert.Equal(5, contas.Select(conta => conta.Prefixo).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(5, await db.Set<PrefixoReservado>().CountAsync(Ct));

        // De novo, nada muda: nem conta nova, nem o arroba das que já existem.
        var arrobas = contas.Select(conta => conta.Arroba.ToString()).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(0, (await factory.SeedDemoAsync(Ct)).ContasNovas);
        db.ChangeTracker.Clear();
        Assert.Equal(arrobas, (await db.Set<Usuario>().ToListAsync(Ct)).Select(conta => conta.Arroba.ToString()).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Aluno_novo_no_arquivo_entra_sem_mexer_no_resto()
    {
        await using var factory = await ComBaseAsync();
        await factory.SeedDemoAsync(Ct);

        _copia.Alterar("demo/mundo.json", mundo => Alunos(mundo, "CC").Add(new JsonObject
        {
            ["ra"] = "20260106",
            ["email"] = "nova.aluna@al.unieduk.com.br",
            ["nomeCompleto"] = "Nova Aluna",
            ["semestre"] = 2,
            ["materias"] = new JsonArray("Banco de Dados"),
        }));
        var resultado = await factory.SeedDemoAsync(Ct);

        Assert.Equal(new ResultadoDoSeedDemo(0, 0, 0, 1, 1, 0, 0), resultado);
    }

    [Fact]
    public async Task Cadastro_que_diverge_do_arquivo_falha_sem_gravar_nada()
    {
        await using var factory = await ComBaseAsync();
        await factory.SeedDemoAsync(Ct);
        var antes = await ContagensAsync(factory);

        _copia.Alterar("demo/mundo.json", mundo =>
        {
            Alunos(mundo, "CC")[0]!["semestre"] = 2;
            Alunos(mundo, "ADM").Add(new JsonObject
            {
                ["ra"] = "20260206",
                ["email"] = "outro.aluno@al.unieduk.com.br",
                ["nomeCompleto"] = "Outro Aluno",
                ["semestre"] = 1,
                ["materias"] = new JsonArray("Marketing"),
            });
        });
        var falha = await Assert.ThrowsAsync<SeedException>(() => factory.SeedDemoAsync(Ct));

        Assert.Contains("ana.souza@al.unieduk.com.br", falha.Message, StringComparison.Ordinal);
        Assert.Equal(antes, await ContagensAsync(factory));
    }

    [Fact]
    public async Task Semestre_do_aluno_alem_da_duracao_do_curso_falha()
    {
        await using var factory = await ComBaseAsync();

        _copia.Alterar("demo/mundo.json", mundo => Alunos(mundo, "CC")[0]!["semestre"] = 9);
        var falha = await Assert.ThrowsAsync<SeedException>(() => factory.SeedDemoAsync(Ct));

        Assert.Contains("ana.souza@al.unieduk.com.br", falha.Message, StringComparison.Ordinal);
        Assert.Equal((0, 0, 0, 0, 0), await ContagensAsync(factory));
    }

    [Fact]
    public async Task Materia_citada_que_nao_existe_no_curso_falha()
    {
        await using var factory = await ComBaseAsync();

        // Aluno de Administração em Algoritmos: a matéria é de CC.
        _copia.Alterar("demo/mundo.json", mundo => Alunos(mundo, "ADM")[0]!["materias"] = new JsonArray("Algoritmos e Programação"));
        var falha = await Assert.ThrowsAsync<SeedException>(() => factory.SeedDemoAsync(Ct));

        Assert.Contains("Algoritmos e Programação", falha.Message, StringComparison.Ordinal);
        Assert.Equal((0, 0, 0, 0, 0), await ContagensAsync(factory));
    }

    [Fact]
    public async Task Email_repetido_no_arquivo_falha()
    {
        await using var factory = await ComBaseAsync();

        _copia.Alterar("demo/mundo.json", mundo => Alunos(mundo, "MED")[0]!["email"] = "ANA.SOUZA@al.unieduk.com.br");
        var falha = await Assert.ThrowsAsync<SeedException>(() => factory.SeedDemoAsync(Ct));

        Assert.Contains("e-mail repetido", falha.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Turma_em_semestre_fora_do_calendario_falha()
    {
        await using var factory = await ComBaseAsync();

        _copia.Alterar("demo/mundo.json", mundo => Curso(mundo, "MED")["materias"]![0]!["turmas"] = new JsonArray("2027/1"));
        var falha = await Assert.ThrowsAsync<SeedException>(() => factory.SeedDemoAsync(Ct));

        Assert.Contains("2027/1", falha.Message, StringComparison.Ordinal);
        Assert.Equal((0, 0, 0, 0, 0), await ContagensAsync(factory));
    }

    [Fact]
    public async Task Restricao_de_curso_para_sigla_existente_passa()
    {
        _copia.AlterarItem("premios-participacao.json", "P05", item => item["restricaoCurso"] = "MED");
        await using var factory = await ComBaseAsync();

        await factory.SeedDemoAsync(Ct);
    }

    [Fact]
    public async Task Restricao_de_curso_para_sigla_inexistente_falha_sem_gravar_nada()
    {
        _copia.AlterarItem("premios-participacao.json", "P05", item => item["restricaoCurso"] = "ENG");
        await using var factory = await ComBaseAsync();

        var falha = await Assert.ThrowsAsync<SeedException>(() => factory.SeedDemoAsync(Ct));

        Assert.Contains("P05 (ENG)", falha.Message, StringComparison.Ordinal);
        Assert.Equal((0, 0, 0, 0, 0), await ContagensAsync(factory));
    }

    private async Task<MaxxApiFactory> ComBaseAsync()
    {
        var factory = await MigradoAsync(postgres, _copia.Diretorio);
        await factory.SeedBaseAsync(Ct);
        return factory;
    }

    private static JsonObject Curso(JsonNode mundo, string sigla) =>
        mundo["cursos"]!.AsArray().Single(curso => (string?)curso!["sigla"] == sigla)!.AsObject();

    private static JsonArray Alunos(JsonNode mundo, string sigla) => Curso(mundo, sigla)["alunos"]!.AsArray();

    private static async Task<(int Cursos, int Turmas, int Cadastros, int Matriculas, int Vinculos)> ContagensAsync(
        MaxxApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        return (
            await db.Set<Curso>().CountAsync(Ct),
            await db.Set<Turma>().CountAsync(Ct),
            await db.Set<CadastroElegivel>().CountAsync(Ct),
            await db.Set<Matricula>().CountAsync(Ct),
            await db.Set<VinculoProfessor>().CountAsync(Ct));
    }
}
