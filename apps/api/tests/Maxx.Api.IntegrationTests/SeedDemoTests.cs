using System.Linq.Expressions;
using System.Text.Json.Nodes;
using Maxx.Api.IntegrationTests.Fotos;
using Maxx.Application.Medalhas;
using Maxx.Application.Ranking;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.BancoDeTeste;

namespace Maxx.Api.IntegrationTests;

// `seed demo` contra o Postgres real, sempre depois do base.
// O bucket é o emulador: o seed demo envia as fotos dos posts do mundo (passo 12).
public sealed class SeedDemoTests(PostgresFixture postgres, ArmazenamentoFixture armazenamento)
    : IClassFixture<PostgresFixture>, IClassFixture<ArmazenamentoFixture>, IDisposable
{
    private readonly CopiaDeDados _copia = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _copia.Dispose();

    [Fact]
    public async Task Demo_cria_quatro_cursos_quarenta_e_oito_turmas_e_cento_e_cinquenta_e_nove_cadastros()
    {
        await using var factory = await ComBaseAsync();

        var resultado = await factory.SeedDemoAsync(Ct);

        // 4 cursos × 6 matérias, uma turma em 2026/1 e outra em 2026/2; 150 alunos, 8 professores
        // e 1 instituição; cada aluno nas turmas das matérias dele nos semestres que cursa, cada
        // professor nas turmas das matérias dele; conta para os 9 de professor e instituição e
        // para os 150 alunos, 30 deles ex-alunos; os 22 eventos de data/demo/eventos.json.
        // Os fatos da atividade têm o teste próprio (SeedAtividadeTests).
        Assert.Equal(
            new ResultadoDoSeedDemo(4, 24, 48, 159, 1512, 48, 9, 22, 150, 30, 0, 0, 0, 0),
            resultado with { MedalhasNovas = 0, PremiosNovos = 0, AvisosNovos = 0, PostsNovos = 0 });
        Assert.Equal(9, resultado.AvisosNovos);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var cursos = await db.Set<Curso>().OrderBy(curso => curso.Sigla)
            .Select(curso => new ValueTuple<string, int>(curso.Sigla, curso.DuracaoEmSemestres)).ToListAsync(Ct);
        Assert.Equal([("ADM", 8), ("CC", 8), ("IA", 8), ("MED", 12)], cursos);
        Assert.Equal(1, await db.Set<CadastroElegivel>().CountAsync(cadastro => cadastro.Papeis == Papeis.Instituicao, Ct));
    }

    [Fact]
    public async Task Toda_turma_tem_professor_e_todo_aluno_ativo_tem_matricula_em_2026_2_e_o_ex_aluno_nao()
    {
        await using var factory = await ComBaseAsync();
        await factory.SeedDemoAsync(Ct);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var semestre = await db.Set<SemestreLetivo>().SingleAsync(semestre => semestre.Ano == 2026 && semestre.Numero == 2, Ct);

        var turmasSemProfessor = await db.Set<Turma>()
            .Where(turma => !db.Set<VinculoProfessor>().Any(vinculo => vinculo.TurmaId == turma.Id))
            .CountAsync(Ct);
        var exAlunos = await db.Set<Usuario>().Where(usuario => usuario.ExAlunoDesde != null).Select(usuario => usuario.CadastroElegivelId).ToListAsync(Ct);
        var semMatricula = await db.Set<CadastroElegivel>()
            .Where(cadastro => cadastro.Papeis == Papeis.Aluno)
            .Where(aluno => !db.Set<Matricula>().Any(matricula =>
                matricula.AlunoId == aluno.Id
                && db.Set<Turma>().Any(turma => turma.Id == matricula.TurmaId && turma.SemestreLetivoId == semestre.Id)))
            .Select(aluno => aluno.Id)
            .ToListAsync(Ct);

        Assert.Equal(0, turmasSemProfessor);
        Assert.Equal(30, exAlunos.Count);
        Assert.Equal(exAlunos.Order(), semMatricula.Order());
        Assert.Equal(24, await db.Set<Turma>().CountAsync(turma => turma.SemestreLetivoId == semestre.Id, Ct));
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

        Assert.Equal(new ResultadoDoSeedDemo(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), resultado);
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

        Assert.Equal(160, (await ContagensAsync(factory)).Cadastros);
    }

    [Fact]
    public async Task Professores_instituicao_e_alunos_do_arquivo_ganham_conta_sem_senha()
    {
        await using var factory = await ComBaseAsync();
        await factory.SeedDemoAsync(Ct);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var contas = await db.Set<Usuario>().ToListAsync(Ct);

        // Professor e instituição: primeiro acesso pela recuperação de senha (produto.md 2.5).
        // Aluno do mundo: sem senha e com e-mail num domínio reservado, ninguém entra (P8).
        Assert.Equal(159, contas.Count);
        Assert.All(contas, conta => Assert.False(conta.TemSenha));
        Assert.Equal(8, contas.Count(conta => conta.Papeis == Papeis.Professor && conta.Sufixo == Sufixo.Professor));
        Assert.Single(contas, conta => conta.Papeis == Papeis.Instituicao && conta.Sufixo == Sufixo.Instituicao);
        Assert.Equal(150, contas.Count(conta => conta.Papeis == Papeis.Aluno));
        Assert.Equal(159, contas.Select(conta => conta.Prefixo).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(159, await db.Set<PrefixoReservado>().CountAsync(Ct));
        Assert.Empty(await db.Set<AceiteDeTermo>().ToListAsync(Ct));

        // De novo, nada muda: nem conta nova, nem o arroba das que já existem.
        var arrobas = contas.Select(conta => conta.Arroba.ToString()).Order(StringComparer.Ordinal).ToList();
        var resultado = await factory.SeedDemoAsync(Ct);
        Assert.Equal((0, 0), (resultado.ContasNovas, resultado.ContasDeAlunoNovas));
        db.ChangeTracker.Clear();
        Assert.Equal(arrobas, (await db.Set<Usuario>().ToListAsync(Ct)).Select(conta => conta.Arroba.ToString()).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Conta_nasce_no_inicio_do_primeiro_semestre_cursado_e_ex_aluno_e_marcado_pela_instituicao_no_fim_do_ultimo()
    {
        await using var factory = await ComBaseAsync();
        await factory.SeedDemoAsync(Ct);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var contas = await (
                from usuario in db.Set<Usuario>()
                join cadastro in db.Set<CadastroElegivel>() on usuario.CadastroElegivelId equals cadastro.Id
                select new { usuario, cadastro.Email })
            .ToDictionaryAsync(linha => linha.Email, linha => linha.usuario, StringComparer.OrdinalIgnoreCase, Ct);
        var instituicao = contas["coordenacao@unieduk.com.br"];

        // 2026/1 começa em 26/01 e termina em 27/06; 2026/2 começa em 27/07. Meia-noite em São Paulo.
        var inicioDe2026_1 = new DateTimeOffset(2026, 1, 26, 3, 0, 0, TimeSpan.Zero);
        Assert.Equal(inicioDe2026_1, contas["lucas.ferreira@al.unieduk.invalid"].CriadoEm);
        Assert.Equal(new DateTimeOffset(2026, 7, 27, 3, 0, 0, TimeSpan.Zero), contas["ana.souza@al.unieduk.invalid"].CriadoEm);
        Assert.Equal(inicioDe2026_1, instituicao.CriadoEm);
        Assert.Equal(inicioDe2026_1, contas["renata.albuquerque@prof.unieduk.com.br"].CriadoEm);

        var exAlunos = contas.Values.Where(conta => conta.EhExAluno).ToList();
        Assert.Equal(30, exAlunos.Count);
        Assert.All(exAlunos, conta =>
        {
            Assert.Equal(Sufixo.ExAluno, conta.Sufixo);
            Assert.Equal(instituicao.Id, conta.ExAlunoMarcadoPorId);
            Assert.Equal(new DateTimeOffset(2026, 6, 28, 3, 0, 0, TimeSpan.Zero), conta.ExAlunoDesde);
        });

        // Os pedidos ao worker: raridade, pelo divisor, e ranking, pelos ex-alunos. Um de cada.
        var trabalhos = await db.Set<Trabalho>().ToListAsync(Ct);
        Assert.Single(trabalhos, trabalho => trabalho.Tipo == RecalcularRaridade.Tipo);
        Assert.Single(trabalhos, trabalho => trabalho.Tipo == RecalcularRanking.Tipo);
    }

    [Fact]
    public async Task Aluno_da_base_fora_do_arquivo_nao_ganha_conta()
    {
        await using var factory = await ComBaseAsync();
        await factory.SeedDemoAsync(Ct);
        var cc = await LinhaAsync<Curso>(factory, curso => curso.Sigla == "CC");
        var deFora = CadastroElegivel.Aluno("20269999", "de.fora@al.unieduk.com.br", "Aluna de Fora", cc, 1);
        await AdicionarAsync(factory, deFora);

        await factory.SeedDemoAsync(Ct);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        Assert.False(await db.Set<Usuario>().AnyAsync(usuario => usuario.CadastroElegivelId == deFora.Id, Ct));
    }

    [Fact]
    public async Task Email_e_curso_corrigidos_pela_instituicao_vencem_o_arquivo_e_o_resto_que_diverge_falha()
    {
        await using var factory = await ComBaseAsync();
        await factory.SeedDemoAsync(Ct);
        var instituicao = await LinhaAsync<Usuario>(factory, usuario => usuario.Sufixo == Sufixo.Instituicao);
        var adm = await LinhaAsync<Curso>(factory, curso => curso.Sigla == "ADM");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
            var lucas = await db.Set<CadastroElegivel>().SingleAsync(cadastro => cadastro.Ra == "20260102", Ct);
            var mariana = await db.Set<CadastroElegivel>().SingleAsync(cadastro => cadastro.Ra == "20260103", Ct);
            db.Add(lucas.CorrigirEmail("lucas.f@al.unieduk.invalid", instituicao.Id, DateTimeOffset.UtcNow).Correcao!);
            db.Add(mariana.TrocarDeCurso(adm, 2, instituicao.Id, DateTimeOffset.UtcNow).Troca!);
            await db.SaveChangesAsync(Ct);
        }

        Assert.Equal(new ResultadoDoSeedDemo(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), await factory.SeedDemoAsync(Ct));

        // Sem o registro do ato, a mesma divergência é do arquivo, e falha.
        _copia.Alterar("demo/mundo.json", mundo => Alunos(mundo, "CC")[3]!["email"] = "pedro.h.lima@al.unieduk.invalid");
        var falha = await Assert.ThrowsAsync<SeedException>(() => factory.SeedDemoAsync(Ct));
        Assert.Contains("20260104", falha.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Aluno_novo_no_arquivo_entra_sem_mexer_no_resto()
    {
        await using var factory = await ComBaseAsync();
        await factory.SeedDemoAsync(Ct);

        _copia.Alterar("demo/mundo.json", mundo => Alunos(mundo, "CC").Add(new JsonObject
        {
            ["ra"] = "20260199",
            ["email"] = "nova.aluna@al.unieduk.invalid",
            ["nomeCompleto"] = "Nova Aluna",
            ["semestre"] = 2,
            ["materias"] = new JsonArray("Banco de Dados"),
        }));
        var resultado = await factory.SeedDemoAsync(Ct);

        Assert.Equal(new ResultadoDoSeedDemo(0, 0, 0, 1, 2, 0, 0, 0, 1, 0, 0, 0, 0, 0), resultado);
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
                ["ra"] = "20260299",
                ["email"] = "outro.aluno@al.unieduk.invalid",
                ["nomeCompleto"] = "Outro Aluno",
                ["semestre"] = 1,
                ["materias"] = new JsonArray("Marketing"),
            });
        });
        var falha = await Assert.ThrowsAsync<SeedException>(() => factory.SeedDemoAsync(Ct));

        Assert.Contains("ana.souza@al.unieduk.invalid", falha.Message, StringComparison.Ordinal);
        Assert.Equal(antes, await ContagensAsync(factory));
    }

    [Fact]
    public async Task Semestre_do_aluno_alem_da_duracao_do_curso_falha()
    {
        await using var factory = await ComBaseAsync();

        _copia.Alterar("demo/mundo.json", mundo => Alunos(mundo, "CC")[0]!["semestre"] = 9);
        var falha = await Assert.ThrowsAsync<SeedException>(() => factory.SeedDemoAsync(Ct));

        Assert.Contains("ana.souza@al.unieduk.invalid", falha.Message, StringComparison.Ordinal);
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

        _copia.Alterar("demo/mundo.json", mundo => Alunos(mundo, "MED")[0]!["email"] = "ANA.SOUZA@al.unieduk.invalid");
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

        // A visita técnica semeada é de Administração: restrito a Medicina, o P05 sai da atividade.
        _copia.Alterar("demo/atividade.json", atividade => atividade["premiosParticipacao"]!.AsObject().Remove("P05"));
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

    [Fact]
    public async Task Eventos_entram_com_datas_itens_e_curso()
    {
        await using var factory = await ComBaseAsync();
        await factory.SeedDemoAsync(Ct);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var cc = await db.Set<Curso>().SingleAsync(curso => curso.Sigla == "CC", Ct);
        var eventos = await db.Set<Evento>().ToDictionaryAsync(evento => evento.Codigo!, Ct);

        Assert.Equal(22, eventos.Count);
        Assert.All(eventos.Values, evento => Assert.Equal(OrigemDoEvento.Semeado, evento.Origem));
        var semana = eventos["semana-academica-cc-2026-2"];
        Assert.Equal(
            ("Semana Acadêmica de Ciência da Computação", new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 18), cc.Id),
            (semana.Nome, semana.Inicio, semana.Fim, semana.CursoId));
        Assert.Equal(["P06", "P08", "P09"], semana.ItensAceitos);
        Assert.Null(eventos["maratona-de-programacao-2026"].CursoId);
    }

    [Fact]
    public async Task Evento_novo_no_arquivo_entra_sem_mexer_no_resto()
    {
        await using var factory = await ComBaseAsync();
        await factory.SeedDemoAsync(Ct);

        _copia.Alterar("demo/eventos.json", raiz => Eventos(raiz).Add(Evento("palestra-extra-2026-2", "P01")));
        var resultado = await factory.SeedDemoAsync(Ct);

        Assert.Equal(new ResultadoDoSeedDemo(0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0), resultado);
    }

    [Fact]
    public async Task Evento_que_diverge_do_arquivo_falha_sem_gravar_nada()
    {
        await using var factory = await ComBaseAsync();
        await factory.SeedDemoAsync(Ct);

        _copia.Alterar("demo/eventos.json", raiz =>
        {
            EventoDe(raiz, "palestra-abertura-2026-2")["fim"] = "2026-08-04";
            Eventos(raiz).Add(Evento("palestra-extra-2026-2", "P01"));
        });
        var falha = await Assert.ThrowsAsync<SeedException>(() => factory.SeedDemoAsync(Ct));

        Assert.Contains("palestra-abertura-2026-2", falha.Message, StringComparison.Ordinal);
        Assert.Equal(22, await ContarEventosAsync(factory));
    }

    [Theory]
    [InlineData("P99", "P99 não é prêmio de participação")]
    [InlineData("M01", "M01 não é prêmio de participação")]
    [InlineData("P17", "deduplica por matéria")]
    public async Task Evento_com_item_que_nao_pode_aceitar_falha_sem_gravar_nada(string item, string trecho)
    {
        await using var factory = await ComBaseAsync();

        _copia.Alterar("demo/eventos.json", raiz => Eventos(raiz).Add(Evento("evento-ruim", item)));
        var falha = await Assert.ThrowsAsync<SeedException>(() => factory.SeedDemoAsync(Ct));

        Assert.Contains("evento-ruim", falha.Message, StringComparison.Ordinal);
        Assert.Contains(trecho, falha.Message, StringComparison.Ordinal);
        Assert.Equal((0, 0, 0, 0, 0), await ContagensAsync(factory));
    }

    [Fact]
    public async Task Evento_de_curso_inexistente_falha_sem_gravar_nada()
    {
        await using var factory = await ComBaseAsync();

        _copia.Alterar("demo/eventos.json", raiz =>
        {
            var evento = Evento("semana-eng", "P06");
            evento["curso"] = "ENG";
            Eventos(raiz).Add(evento);
        });
        var falha = await Assert.ThrowsAsync<SeedException>(() => factory.SeedDemoAsync(Ct));

        Assert.Contains("ENG", falha.Message, StringComparison.Ordinal);
        Assert.Equal((0, 0, 0, 0, 0), await ContagensAsync(factory));
    }

    [Fact]
    public async Task Evento_que_termina_antes_de_comecar_falha()
    {
        await using var factory = await ComBaseAsync();

        _copia.Alterar("demo/eventos.json", raiz => EventoDe(raiz, "doacao-de-sangue-2026-2")["fim"] = "2026-08-01");
        var falha = await Assert.ThrowsAsync<SeedException>(() => factory.SeedDemoAsync(Ct));

        Assert.Contains("doacao-de-sangue-2026-2", falha.Message, StringComparison.Ordinal);
        Assert.Equal(0, await ContarEventosAsync(factory));
    }

    [Fact]
    public async Task Codigo_de_evento_repetido_no_arquivo_falha()
    {
        await using var factory = await ComBaseAsync();

        _copia.Alterar("demo/eventos.json", raiz => Eventos(raiz).Add(Evento("mostra-ia-2026-2", "P08")));
        var falha = await Assert.ThrowsAsync<SeedException>(() => factory.SeedDemoAsync(Ct));

        Assert.Contains("código de evento repetido: mostra-ia-2026-2", falha.Message, StringComparison.Ordinal);
    }

    private static JsonArray Eventos(JsonNode raiz) => raiz["eventos"]!.AsArray();

    private static JsonObject EventoDe(JsonNode raiz, string codigo) =>
        Eventos(raiz).Single(evento => (string?)evento!["codigo"] == codigo)!.AsObject();

    private static JsonObject Evento(string codigo, string item) => new()
    {
        ["codigo"] = codigo,
        ["nome"] = "Palestra extra",
        ["inicio"] = "2026-10-01",
        ["fim"] = "2026-10-01",
        ["itens"] = new JsonArray(item),
    };

    private static async Task<int> ContarEventosAsync(MaxxApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<Evento>().CountAsync(Ct);
    }

    private static async Task<T> LinhaAsync<T>(MaxxApiFactory factory, Expression<Func<T, bool>> filtro)
        where T : class
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<T>().AsNoTracking().SingleAsync(filtro, Ct);
    }

    private async Task<MaxxApiFactory> ComBaseAsync()
    {
        var factory = await MigradoAsync(postgres, _copia.Diretorio, armazenamento.Configuracao);
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
