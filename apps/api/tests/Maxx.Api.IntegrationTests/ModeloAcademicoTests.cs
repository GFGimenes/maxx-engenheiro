using Maxx.Domain.Academico;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Maxx.Api.IntegrationTests.BancoDeTeste;

namespace Maxx.Api.IntegrationTests;

// O modelo acadêmico no Postgres real: a migration, a ida e volta pelo EF e as
// constraints que seguram o dado mesmo quando ele não passa pelo domínio.
public sealed class ModeloAcademicoTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migration_cria_as_nove_tabelas_academicas()
    {
        await using var factory = await BancoMigradoAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();

        await using var command = dataSource.CreateCommand(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name");
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var tabelas = new List<string>();
        while (await reader.ReadAsync(Ct))
        {
            tabelas.Add(reader.GetString(0));
        }

        Assert.Subset(
            tabelas.ToHashSet(),
            new HashSet<string>
            {
                "curso", "ano_letivo", "semestre_letivo", "bimestre", "materia", "turma",
                "cadastro_elegivel", "matricula", "vinculo_professor",
            });
    }

    [Fact]
    public async Task Modelo_academico_vai_e_volta_pelo_banco()
    {
        await using var factory = await BancoMigradoAsync();
        var mundo = new Mundo();
        await SalvarAsync(factory, mundo.Todos());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();

        var ano = await db.Set<AnoLetivo>()
            .Include(ano => ano.Semestres).ThenInclude(semestre => semestre.Bimestres)
            .SingleAsync(Ct);
        Assert.Equal(2026, ano.Ano);
        Assert.Equal(["2026/1", "2026/2"], ano.Semestres.Select(semestre => semestre.Codigo));
        Assert.Equal([1, 2, 3, 4], ano.Bimestres.Select(bimestre => bimestre.Numero));
        Assert.Equal(new DateOnly(2026, 7, 27), ano.Semestres[1].Inicio);
        Assert.Equal(new DateOnly(2026, 12, 5), ano.Fim);

        var turma = await db.Set<Turma>().Include(turma => turma.Materia).SingleAsync(Ct);
        Assert.Equal("Banco de Dados", turma.Materia.Nome);
        Assert.Equal(ano.Semestres[1].Id, turma.SemestreLetivoId);

        var aluno = await db.Set<CadastroElegivel>().SingleAsync(cadastro => cadastro.Ra == "123456", Ct);
        Assert.Equal(Papeis.Aluno, aluno.Papeis);
        Assert.Equal(mundo.Computacao.Id, aluno.CursoId);
        Assert.Equal(3, aluno.Semestre);

        var professor = await db.Set<CadastroElegivel>().SingleAsync(cadastro => cadastro.Id == mundo.Professor.Id, Ct);
        Assert.Equal(Papeis.Professor | Papeis.Instituicao, professor.Papeis);
        Assert.Null(professor.Ra);

        Assert.Equal(aluno.Id, (await db.Set<Matricula>().SingleAsync(Ct)).AlunoId);
        Assert.Equal(professor.Id, (await db.Set<VinculoProfessor>().SingleAsync(Ct)).ProfessorId);
    }

    [Fact]
    public async Task Ra_repetido_e_recusado()
    {
        await using var factory = await BancoMigradoAsync();
        var mundo = new Mundo();
        await SalvarAsync(factory, mundo.Todos());

        var outro = CadastroElegivel.Aluno("123456", "outra@al.unieduk.com.br", "Outra Pessoa", mundo.Computacao, 1);

        AssertUnicidade(await FalhaAoSalvarAsync(factory, outro), "ix_cadastro_elegivel_ra");
    }

    [Fact]
    public async Task Email_repetido_com_outra_caixa_e_recusado()
    {
        await using var factory = await BancoMigradoAsync();
        var mundo = new Mundo();
        await SalvarAsync(factory, mundo.Todos());

        var outro = CadastroElegivel.Aluno("999999", "ANA@al.unieduk.com.br", "Ana Souza", mundo.Computacao, 1);

        AssertUnicidade(await FalhaAoSalvarAsync(factory, outro), "ix_cadastro_elegivel_email");
    }

    [Fact]
    public async Task Email_guarda_a_caixa_como_veio()
    {
        await using var factory = await BancoMigradoAsync();
        var mundo = new Mundo();
        var aluno = CadastroElegivel.Aluno("777777", "Bia.Rocha@al.unieduk.com.br", "Bia Rocha", mundo.Computacao, 1);
        await SalvarAsync(factory, mundo.Computacao, aluno);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var lido = await db.Set<CadastroElegivel>().SingleAsync(cadastro => cadastro.Email == "bia.rocha@AL.unieduk.com.br", Ct);

        Assert.Equal("Bia.Rocha@al.unieduk.com.br", lido.Email);
    }

    [Fact]
    public async Task Varios_cadastros_sem_ra_convivem()
    {
        await using var factory = await BancoMigradoAsync();
        var mundo = new Mundo();
        await SalvarAsync(factory, mundo.Todos());

        await AdicionarAsync(factory, CadastroElegivel.Instituicao("secretaria@unieduk.com.br", "Secretaria UniMax"));
    }

    [Fact]
    public async Task Sigla_de_curso_repetida_e_recusada()
    {
        await using var factory = await BancoMigradoAsync();
        await SalvarAsync(factory, new Curso("Ciência da Computação", "CC", 8));

        AssertUnicidade(await FalhaAoSalvarAsync(factory, new Curso("Ciências Contábeis", "CC", 8)), "ix_curso_sigla");
    }

    [Fact]
    public async Task Segunda_turma_da_mesma_materia_no_mesmo_semestre_e_recusada()
    {
        await using var factory = await BancoMigradoAsync();
        var mundo = new Mundo();
        await SalvarAsync(factory, mundo.Todos());

        var repetida = new Turma(mundo.BancoDeDados, mundo.Ano.Semestres[1]);

        AssertUnicidade(await FalhaAoSalvarAsync(factory, repetida), "ix_turma_materia_id_semestre_letivo_id");
    }

    [Fact]
    public async Task Mesma_materia_em_outro_semestre_e_outra_turma()
    {
        await using var factory = await BancoMigradoAsync();
        var mundo = new Mundo();
        await SalvarAsync(factory, mundo.Todos());

        await AdicionarAsync(factory, new Turma(mundo.BancoDeDados, mundo.Ano.Semestres[0]));
    }

    [Fact]
    public async Task Matricula_repetida_e_recusada()
    {
        await using var factory = await BancoMigradoAsync();
        var mundo = new Mundo();
        await SalvarAsync(factory, mundo.Todos());

        AssertUnicidade(
            await FalhaAoSalvarAsync(factory, new Matricula(mundo.Aluno, mundo.Turma)), "ix_matricula_aluno_id_turma_id");
    }

    [Fact]
    public async Task Vinculo_repetido_e_recusado()
    {
        await using var factory = await BancoMigradoAsync();
        var mundo = new Mundo();
        await SalvarAsync(factory, mundo.Todos());

        AssertUnicidade(
            await FalhaAoSalvarAsync(factory, new VinculoProfessor(mundo.Professor, mundo.Turma)),
            "ix_vinculo_professor_professor_id_turma_id");
    }

    // Os casos abaixo o domínio já impede; o banco recusa sozinho, por SQL direto.

    [Fact]
    public async Task Bimestre_que_termina_antes_de_comecar_e_recusado_pelo_banco()
    {
        await using var factory = await BancoMigradoAsync();
        var mundo = new Mundo();
        await SalvarAsync(factory, mundo.Todos());

        var falha = await FalhaNoSqlAsync(factory, "UPDATE bimestre SET fim = inicio - 1 WHERE numero = 1");

        AssertCheck(falha, "ck_bimestre_fim_depois_do_inicio");
    }

    [Fact]
    public async Task Aluno_sem_curso_e_recusado_pelo_banco()
    {
        await using var factory = await BancoMigradoAsync();
        var mundo = new Mundo();
        await SalvarAsync(factory, mundo.Todos());

        var falha = await FalhaNoSqlAsync(
            factory,
            "UPDATE cadastro_elegivel SET curso_id = NULL, semestre = NULL WHERE ra = '123456'");

        AssertCheck(falha, "ck_cadastro_elegivel_aluno");
    }

    [Fact]
    public async Task Acumulo_com_aluno_e_recusado_pelo_banco()
    {
        await using var factory = await BancoMigradoAsync();
        var mundo = new Mundo();
        await SalvarAsync(factory, mundo.Todos());

        // Professor (sem curso, sem semestre) virando também aluno: só a regra de papéis falha.
        var falha = await FalhaNoSqlAsync(
            factory, $"UPDATE cadastro_elegivel SET papeis = 3 WHERE id = '{mundo.Professor.Id}'");

        AssertCheck(falha, "ck_cadastro_elegivel_papeis");
    }

    [Fact]
    public async Task Professor_com_curso_e_recusado_pelo_banco()
    {
        await using var factory = await BancoMigradoAsync();
        var mundo = new Mundo();
        await SalvarAsync(factory, mundo.Todos());

        var falha = await FalhaNoSqlAsync(
            factory,
            $"UPDATE cadastro_elegivel SET curso_id = '{mundo.Computacao.Id}' WHERE id = '{mundo.Professor.Id}'");

        AssertCheck(falha, "ck_cadastro_elegivel_aluno");
    }

    private Task<MaxxApiFactory> BancoMigradoAsync() => BancoDeTeste.MigradoAsync(postgres);

    // Um mundo mínimo: um curso, o ano de 2026, uma turma, um aluno matriculado e um
    // professor (que também é instituição) vinculado.
    private sealed class Mundo
    {
        public Mundo()
        {
            Ano = new AnoLetivo(
                2026,
                new SemestreLetivo(
                    2026, 1, new Bimestre(1, new(2026, 1, 26), new(2026, 4, 3)), new Bimestre(2, new(2026, 4, 6), new(2026, 6, 27))),
                new SemestreLetivo(
                    2026, 2, new Bimestre(3, new(2026, 7, 27), new(2026, 9, 26)), new Bimestre(4, new(2026, 9, 28), new(2026, 12, 5))));
            BancoDeDados = new Materia(Computacao, "Banco de Dados");
            Turma = new Turma(BancoDeDados, Ano.Semestres[1]);
            Aluno = CadastroElegivel.Aluno("123456", "ana@al.unieduk.com.br", "Ana Souza", Computacao, 3);
            Professor = CadastroElegivel.Professor("bruno@unieduk.com.br", "Bruno Lima", tambemInstituicao: true);
        }

        public Curso Computacao { get; } = new("Ciência da Computação", "CC", 8);

        public AnoLetivo Ano { get; }

        public Materia BancoDeDados { get; }

        public Turma Turma { get; }

        public CadastroElegivel Aluno { get; }

        public CadastroElegivel Professor { get; }

        public object[] Todos() =>
        [
            Computacao, Ano, BancoDeDados, Turma, Aluno, Professor,
            new Matricula(Aluno, Turma), new VinculoProfessor(Professor, Turma),
        ];
    }
}
