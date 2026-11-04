using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Maxx.Api.IntegrationTests.BancoDeTeste;

namespace Maxx.Api.IntegrationTests;

// Conta, sessão e recuperação no Postgres real: a migration, a ida e volta pelo EF, as
// unicidades que o domínio não consegue garantir sozinho e a concorrência de quem usa o
// mesmo refresh ou o mesmo link ao mesmo tempo.
public sealed class IdentidadePersistenciaTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset _agora = new(2026, 11, 3, 15, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migration_cria_as_cinco_tabelas_de_identidade()
    {
        await using var factory = await MigradoAsync(postgres);
        await using var scope = factory.Services.CreateAsyncScope();
        var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();

        await using var command = dataSource.CreateCommand(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'");
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var tabelas = new HashSet<string>();
        while (await reader.ReadAsync(Ct))
        {
            tabelas.Add(reader.GetString(0));
        }

        Assert.Subset(
            tabelas,
            new HashSet<string> { "usuario", "prefixo_reservado", "familia_sessao", "refresh_token", "recuperacao_senha" });
    }

    [Fact]
    public async Task Usuario_vai_e_volta_com_os_prefixos_reservados()
    {
        await using var factory = await MigradoAsync(postgres);
        var mundo = new Mundo();
        await SalvarAsync(factory, mundo.Todos());
        var ana = mundo.Ana();
        ana.EditarPrefixo("aninha", _agora.AddDays(1));
        await SalvarAsync(factory, ana);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var lida = await db.Set<Usuario>().Include(usuario => usuario.PrefixosReservados).SingleAsync(Ct);

        Assert.Equal("@aninha-CC", lida.Arroba.ToString());
        Assert.Equal("Ana", lida.NomeDeExibicao);
        Assert.Equal(Papeis.Aluno, lida.Papeis);
        Assert.Equal("hash", lida.HashDaSenha);
        Assert.Equal(_agora, lida.CriadoEm);
        Assert.Equal(["anaSouza", "aninha"], lida.PrefixosReservados.Select(reservado => reservado.Prefixo).Order());
    }

    [Fact]
    public async Task Conta_do_seed_vai_e_volta_sem_senha()
    {
        await using var factory = await MigradoAsync(postgres);
        var mundo = new Mundo();
        await SalvarAsync(factory, mundo.Todos());
        await SalvarAsync(factory, Usuario.CriarPeloSeed(mundo.Renata, "renataAlbuquerque", _agora));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var renata = await db.Set<Usuario>().SingleAsync(Ct);

        Assert.False(renata.TemSenha);
        Assert.Equal("@renataAlbuquerque-PROF", renata.Arroba.ToString());
    }

    [Fact]
    public async Task Edicao_de_prefixo_grava_a_reserva_nova_e_mantem_a_antiga()
    {
        await using var factory = await MigradoAsync(postgres);
        var mundo = new Mundo();
        await SalvarAsync(factory, mundo.Todos());
        await SalvarAsync(factory, mundo.Ana());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
            var ana = await db.Set<Usuario>().Include(usuario => usuario.PrefixosReservados).SingleAsync(Ct);
            ana.EditarPrefixo("aninha", _agora.AddDays(1));
            await db.SaveChangesAsync(Ct);
        }

        // Bruno não toma o prefixo que a Ana deixou de usar.
        var falha = await FalhaAoSalvarUsuarioAsync(factory, mundo.Bruno("anaSouza"));
        AssertUnicidade(falha, "pk_prefixo_reservado");
    }

    [Fact]
    public async Task Um_ra_aceita_uma_conta_so()
    {
        await using var factory = await MigradoAsync(postgres);
        var mundo = new Mundo();
        await SalvarAsync(factory, mundo.Todos());
        await SalvarAsync(factory, mundo.Ana());

        var segunda = Usuario.CadastrarAluno(mundo.CadastroDaAna, mundo.Computacao, "Outra", "outraConta", "hash", _agora);

        AssertUnicidade(await FalhaAoSalvarUsuarioAsync(factory, segunda), "ix_usuario_cadastro_elegivel_id");
    }

    [Fact]
    public async Task Prefixo_de_outro_e_recusado_sem_diferenciar_maiusculas()
    {
        await using var factory = await MigradoAsync(postgres);
        var mundo = new Mundo();
        await SalvarAsync(factory, mundo.Todos());
        await SalvarAsync(factory, mundo.Ana());

        AssertUnicidade(await FalhaAoSalvarUsuarioAsync(factory, mundo.Bruno("ANASOUZA")), "pk_prefixo_reservado");
    }

    [Fact]
    public async Task Dois_cadastros_simultaneos_pelo_mesmo_prefixo_deixam_um_so_vencedor()
    {
        await using var factory = await MigradoAsync(postgres);
        var mundo = new Mundo();
        await SalvarAsync(factory, mundo.Todos());

        var tentativas = await Task.WhenAll(
            TentarSalvarAsync(factory, mundo.Ana()),
            TentarSalvarAsync(factory, mundo.Bruno("anaSouza")));

        Assert.Single(tentativas, sucesso => sucesso);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        Assert.Equal(1, await db.Set<Usuario>().CountAsync(Ct));
        Assert.Equal(1, await db.Set<PrefixoReservado>().CountAsync(Ct));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("1ana")]
    [InlineData("ana-souza")]
    [InlineData("joão")]
    public async Task Banco_recusa_prefixo_fora_do_formato_mesmo_sem_passar_pelo_dominio(string prefixo)
    {
        await using var factory = await MigradoAsync(postgres);

        var falha = await FalhaNoSqlAsync(
            factory,
            $"INSERT INTO prefixo_reservado (prefixo, usuario_id, reservado_em) VALUES ('{prefixo}', gen_random_uuid(), now())");

        AssertCheck(falha, "ck_prefixo_reservado_prefixo");
    }

    [Fact]
    public async Task Ex_aluno_vai_e_volta_com_o_sufixo_ex()
    {
        await using var factory = await MigradoAsync(postgres);
        var mundo = new Mundo();
        await SalvarAsync(factory, mundo.Todos());
        var ana = mundo.Ana();
        ana.MarcarExAluno(_agora.AddYears(4));
        await SalvarAsync(factory, ana);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var lida = await db.Set<Usuario>().SingleAsync(Ct);

        Assert.True(lida.EhExAluno);
        Assert.Equal("@anaSouza-EX", lida.Arroba.ToString());
    }

    [Fact]
    public async Task Familia_de_sessao_vai_e_volta_com_a_rotacao()
    {
        await using var factory = await MigradoAsync(postgres);
        var (_, ana) = await AnaGravadaAsync(factory);
        await SalvarAsync(factory, FamiliaDeSessao.Abrir(ana.Id, "R1", _agora));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
            var familia = await FamiliaPeloHashAsync(db, "R1");
            Assert.Equal(ResultadoDoRefresh.Rotacionado, familia.Rotacionar("R1", "R2", _agora.AddMinutes(15)));
            await db.SaveChangesAsync(Ct);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
            var familia = await FamiliaPeloHashAsync(db, "R2");
            Assert.Equal(2, familia.Tokens.Count);
            Assert.Equal(_agora.AddMinutes(15), familia.Tokens.Single(token => token.Hash == "R1").RotacionadoEm);

            Assert.Equal(ResultadoDoRefresh.ReusoDetectado, familia.Rotacionar("R1", "R3", _agora.AddMinutes(16)));
            await db.SaveChangesAsync(Ct);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
            var familia = await FamiliaPeloHashAsync(db, "R2");
            Assert.Equal(MotivoDaRevogacao.Reuso, familia.MotivoDaRevogacao);
            Assert.Equal(_agora.AddMinutes(16), familia.RevogadaEm);
        }
    }

    [Fact]
    public async Task Duas_renovacoes_simultaneas_com_o_mesmo_refresh_so_uma_grava()
    {
        await using var factory = await MigradoAsync(postgres);
        var (_, ana) = await AnaGravadaAsync(factory);
        await SalvarAsync(factory, FamiliaDeSessao.Abrir(ana.Id, "R1", _agora));

        await using var scopeA = factory.Services.CreateAsyncScope();
        await using var scopeB = factory.Services.CreateAsyncScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<MaxxDbContext>();

        // As duas leem R1 ainda não rotacionado, e as duas rotacionam.
        var familiaA = await FamiliaPeloHashAsync(dbA, "R1");
        var familiaB = await FamiliaPeloHashAsync(dbB, "R1");
        Assert.Equal(ResultadoDoRefresh.Rotacionado, familiaA.Rotacionar("R1", "R2a", _agora.AddMinutes(15)));
        Assert.Equal(ResultadoDoRefresh.Rotacionado, familiaB.Rotacionar("R1", "R2b", _agora.AddMinutes(15)));

        await dbA.SaveChangesAsync(Ct);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync(Ct));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        Assert.Equal(["R1", "R2a"], await db.Set<RefreshToken>().Select(token => token.Hash).Order().ToListAsync(Ct));
    }

    [Fact]
    public async Task Hash_de_refresh_e_unico_entre_familias()
    {
        await using var factory = await MigradoAsync(postgres);
        var (_, ana) = await AnaGravadaAsync(factory);
        await SalvarAsync(factory, FamiliaDeSessao.Abrir(ana.Id, "R1", _agora));

        var falha = await Assert.ThrowsAsync<DbUpdateException>(
            () => SalvarAsync(factory, FamiliaDeSessao.Abrir(ana.Id, "R1", _agora)));

        AssertUnicidade(Assert.IsType<PostgresException>(falha.InnerException), "ix_refresh_token_hash");
    }

    [Fact]
    public async Task Recuperacao_concluida_grava_senha_revogacoes_e_invalidacoes()
    {
        await using var factory = await MigradoAsync(postgres);
        var (_, ana) = await AnaGravadaAsync(factory);
        await SalvarAsync(
            factory,
            FamiliaDeSessao.Abrir(ana.Id, "C1", _agora),
            RecuperacaoDeSenha.Solicitar(ana.Id, "T1", _agora),
            RecuperacaoDeSenha.Solicitar(ana.Id, "T2", _agora.AddMinutes(1)));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
            var usuario = await db.Set<Usuario>().SingleAsync(Ct);
            var pedido = await db.Set<RecuperacaoDeSenha>().SingleAsync(recuperacao => recuperacao.Hash == "T2", Ct);
            var familias = await db.Set<FamiliaDeSessao>()
                .Where(familia => familia.UsuarioId == usuario.Id && familia.RevogadaEm == null).ToListAsync(Ct);
            var pendentes = await db.Set<RecuperacaoDeSenha>()
                .Where(recuperacao => recuperacao.UsuarioId == usuario.Id && recuperacao.UsadaEm == null && recuperacao.InvalidadaEm == null)
                .ToListAsync(Ct);

            Assert.Equal(
                ResultadoDaRecuperacao.Concluida,
                pedido.Concluir(usuario, "hash-novo", familias, pendentes, _agora.AddMinutes(2)));
            await db.SaveChangesAsync(Ct);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
            Assert.Equal("hash-novo", (await db.Set<Usuario>().SingleAsync(Ct)).HashDaSenha);
            Assert.Equal(MotivoDaRevogacao.SenhaRedefinida, (await db.Set<FamiliaDeSessao>().SingleAsync(Ct)).MotivoDaRevogacao);
            var pedidos = await db.Set<RecuperacaoDeSenha>().OrderBy(recuperacao => recuperacao.Hash).ToListAsync(Ct);
            Assert.NotNull(pedidos[0].InvalidadaEm);
            Assert.NotNull(pedidos[1].UsadaEm);
        }
    }

    [Fact]
    public async Task O_mesmo_link_usado_duas_vezes_ao_mesmo_tempo_so_grava_uma()
    {
        await using var factory = await MigradoAsync(postgres);
        var (_, ana) = await AnaGravadaAsync(factory);
        await SalvarAsync(factory, RecuperacaoDeSenha.Solicitar(ana.Id, "T1", _agora));

        await using var scopeA = factory.Services.CreateAsyncScope();
        await using var scopeB = factory.Services.CreateAsyncScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<MaxxDbContext>();

        foreach (var (db, hash) in new[] { (dbA, "hash-a"), (dbB, "hash-b") })
        {
            var usuario = await db.Set<Usuario>().SingleAsync(Ct);
            var pedido = await db.Set<RecuperacaoDeSenha>().SingleAsync(Ct);
            Assert.Equal(ResultadoDaRecuperacao.Concluida, pedido.Concluir(usuario, hash, [], [], _agora.AddMinutes(1)));
        }

        await dbA.SaveChangesAsync(Ct);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync(Ct));

        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(
            "hash-a",
            (await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<Usuario>().SingleAsync(Ct)).HashDaSenha);
    }

    private static async Task<(Mundo Mundo, Usuario Ana)> AnaGravadaAsync(MaxxApiFactory factory)
    {
        var mundo = new Mundo();
        await SalvarAsync(factory, mundo.Todos());
        var ana = mundo.Ana();
        await SalvarAsync(factory, ana);
        return (mundo, ana);
    }

    private static Task<FamiliaDeSessao> FamiliaPeloHashAsync(MaxxDbContext db, string hash) =>
        db.Set<FamiliaDeSessao>()
            .Include(familia => familia.Tokens)
            .SingleAsync(familia => familia.Tokens.Any(token => token.Hash == hash), Ct);

    private static async Task<PostgresException> FalhaAoSalvarUsuarioAsync(MaxxApiFactory factory, Usuario usuario)
    {
        var falha = await Assert.ThrowsAsync<DbUpdateException>(() => SalvarAsync(factory, usuario));
        return Assert.IsType<PostgresException>(falha.InnerException);
    }

    private static async Task<bool> TentarSalvarAsync(MaxxApiFactory factory, Usuario usuario)
    {
        try
        {
            await SalvarAsync(factory, usuario);
            return true;
        }
        catch (DbUpdateException falha) when (falha.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return false;
        }
    }

    // Dois alunos de CC e uma professora, ainda sem conta.
    private sealed class Mundo
    {
        public Curso Computacao { get; } = new("Ciência da Computação", "CC", 8);

        public CadastroElegivel CadastroDaAna { get; }

        public CadastroElegivel CadastroDoBruno { get; }

        public CadastroElegivel Renata { get; } = CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque");

        public Mundo()
        {
            CadastroDaAna = CadastroElegivel.Aluno("20260101", "ana.souza@al.unieduk.com.br", "Ana Souza", Computacao, 3);
            CadastroDoBruno = CadastroElegivel.Aluno("20260102", "bruno.lima@al.unieduk.com.br", "Bruno Lima", Computacao, 3);
        }

        public object[] Todos() => [Computacao, CadastroDaAna, CadastroDoBruno, Renata];

        public Usuario Ana() => Usuario.CadastrarAluno(CadastroDaAna, Computacao, "Ana", "anaSouza", "hash", _agora);

        public Usuario Bruno(string prefixo) => Usuario.CadastrarAluno(CadastroDoBruno, Computacao, "Bruno", prefixo, "hash", _agora);
    }
}
