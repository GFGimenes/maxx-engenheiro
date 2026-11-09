using Maxx.Application.Fila;
using Maxx.Application.Medalhas;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Domain.Notificacoes;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.RaridadeDosItens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Maxx.Api.IntegrationTests.BancoDeTeste;

namespace Maxx.Api.IntegrationTests.Medalhas;

// O que o worker faz depois da concessão e da anulação, contra o Postgres real: a
// notificação de medalha recebida e a raridade por escassez, com coalescência (ADR 0005).
public sealed class TrabalhosDaMedalhaTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string _recalculada = "Raridade recalculada";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Medalha_recebida_vira_uma_notificacao_para_o_aluno_mesmo_que_o_trabalho_rode_de_novo()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        var medalha = await ConcederAsync(mundo, mundo.Ana, "M01");

        await EsvaziarAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ITrabalhoHandler<NotificarMedalhaRecebida>>()
                .ExecutarAsync(new NotificarMedalhaRecebida(medalha), Ct);
        }

        var notificacao = Assert.Single(await LinhasAsync<Notificacao>(factory));
        Assert.Equal((mundo.Ana, TipoDeNotificacao.MedalhaRecebida, medalha), (notificacao.UsuarioId, notificacao.Tipo, notificacao.ReferenciaId));
        Assert.Null(notificacao.LidaEm);
    }

    [Fact]
    public async Task Medalha_anulada_antes_do_trabalho_nao_notifica()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        var medalha = await ConcederAsync(mundo, mundo.Ana, "M01");
        await AnularAsync(factory, medalha);

        await EsvaziarAsync(factory);

        Assert.Empty(await LinhasAsync<Notificacao>(factory));
    }

    [Fact]
    public async Task Raridade_conta_alunos_distintos_com_medalha_ativa_sobre_os_alunos_com_conta()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;

        // M01: Ana duas vezes e Carla, que vira ex-aluna depois. M03: Bruno, anulada.
        // M90, restrita a CC: Ana. Diego é de ADM; Eva está na base e não tem conta.
        await ConcederAsync(mundo, mundo.Ana, "M01");
        await ConcederAsync(mundo, mundo.Ana, "M01");
        await ConcederAsync(mundo, mundo.Carla, "M01");
        await AnularAsync(factory, await ConcederAsync(mundo, mundo.Bruno, "M03"));
        await ConcederAsync(mundo, mundo.Ana, "M90");
        await MarcarExAlunaAsync(factory, mundo.Carla);

        await EsvaziarAsync(factory);

        var raridades = (await LinhasAsync<RaridadeDoItemRow>(factory)).ToDictionary(linha => linha.ItemCodigo);
        Assert.Equal(["M01", "M03", "M90"], raridades.Keys.Order(StringComparer.Ordinal));
        Assert.Equal((2, 4, "comum"), Contagem(raridades["M01"]));
        Assert.Equal((0, 4, "lendario"), Contagem(raridades["M03"]));
        Assert.Equal((1, 3, "comum"), Contagem(raridades["M90"]));
    }

    [Fact]
    public async Task Rajada_de_pedidos_vira_um_recalculo_so()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        await ConcederAsync(mundo, mundo.Ana, "M01");
        await ConcederAsync(mundo, mundo.Ana, "M01");
        await ConcederAsync(mundo, mundo.Bruno, "M01");
        await ConcederAsync(mundo, mundo.Carla, "M01");
        await ConcederAsync(mundo, mundo.Bruno, "M03");
        Assert.Equal(5, (await LinhasAsync<Trabalho>(factory)).Count(trabalho => trabalho.Tipo == RecalcularRaridade.Tipo));

        await EsvaziarAsync(factory);

        Assert.Single(factory.Log.Linhas, linha => linha.Contains(_recalculada, StringComparison.Ordinal));
        var raridades = (await LinhasAsync<RaridadeDoItemRow>(factory)).ToDictionary(linha => linha.ItemCodigo);
        Assert.Equal(3, raridades["M01"].AlunosComItem);
        Assert.Equal(1, raridades["M03"].AlunosComItem);
        Assert.Equal(5, (await LinhasAsync<Notificacao>(factory)).Count);
        Assert.Empty(await LinhasAsync<Trabalho>(factory));
    }

    [Fact]
    public async Task Pedido_antes_de_dez_segundos_espera_a_janela_fechar()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        await ConcederAsync(mundo, mundo.Ana, "M01");
        await EsvaziarAsync(factory);
        var primeira = mundo.Relogio.GetUtcNow();

        mundo.Relogio.Avancar(TimeSpan.FromSeconds(3));
        await ConcederAsync(mundo, mundo.Bruno, "M01");
        await EsvaziarAsync(factory);

        // Não recalculou: deixou um pedido só, para quando os dez segundos fecharem.
        Assert.Single(factory.Log.Linhas, linha => linha.Contains(_recalculada, StringComparison.Ordinal));
        var adiado = Assert.Single(await LinhasAsync<Trabalho>(factory), trabalho => trabalho.Tipo == RecalcularRaridade.Tipo);
        Assert.Equal((RecalcularRaridade.Tipo, primeira + TimeSpan.FromSeconds(10)), (adiado.Tipo, adiado.DisponivelEm));
        Assert.Equal(1, (await LinhasAsync<RaridadeDoItemRow>(factory)).Single(linha => linha.ItemCodigo == "M01").AlunosComItem);

        mundo.Relogio.Avancar(TimeSpan.FromSeconds(7));
        await EsvaziarAsync(factory);

        Assert.Equal(2, factory.Log.Linhas.Count(linha => linha.Contains(_recalculada, StringComparison.Ordinal)));
        Assert.Equal(2, (await LinhasAsync<RaridadeDoItemRow>(factory)).Single(linha => linha.ItemCodigo == "M01").AlunosComItem);
        Assert.Empty(await LinhasAsync<Trabalho>(factory));
    }

    private static (int, int, string) Contagem(RaridadeDoItemRow linha) => (linha.AlunosComItem, linha.AlunosElegiveis, linha.Raridade);

    private async Task<Mundo> MundoAsync()
    {
        var relogio = new RelogioDeTeste();
        var factory = new MaxxApiFactory(
            await postgres.CreateDatabaseAsync(),
            servicos: servicos => servicos.Replace(ServiceDescriptor.Singleton<TimeProvider>(relogio)));
        await factory.MigrateAsync(Ct);

        var hoje = FusoHorario.DataLocal(relogio.GetUtcNow());
        var inicio = hoje.AddDays(-75);
        Bimestre Bimestre(int numero) => new(numero, inicio.AddDays((numero - 1) * 51), inicio.AddDays(((numero - 1) * 51) + 49));
        var ano = new AnoLetivo(
            hoje.Year, new SemestreLetivo(hoje.Year, 1, Bimestre(1), Bimestre(2)), new SemestreLetivo(hoje.Year, 2, Bimestre(3), Bimestre(4)));
        var cc = new Curso("Ciência da Computação", "CC", 8);
        var adm = new Curso("Administração", "ADM", 8);
        var bd = new Materia(cc, "Banco de Dados");
        var turma = new Turma(bd, ano.Semestres[0]);
        var agora = relogio.GetUtcNow();
        var renata = CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque");
        var secretaria = CadastroElegivel.Instituicao("secretaria@unieduk.com.br", "Secretaria UniMax");
        var ana = CadastroElegivel.Aluno("20260101", "ana.souza@al.unieduk.com.br", "Ana Souza", cc, 3);
        var bruno = CadastroElegivel.Aluno("20260102", "bruno.lima@al.unieduk.com.br", "Bruno Lima", cc, 3);
        var carla = CadastroElegivel.Aluno("20260103", "carla.dias@al.unieduk.com.br", "Carla Dias", cc, 8);
        var diego = CadastroElegivel.Aluno("20260104", "diego.reis@al.unieduk.com.br", "Diego Reis", adm, 3);
        var eva = CadastroElegivel.Aluno("20260105", "eva.melo@al.unieduk.com.br", "Eva Melo", cc, 1);
        var contaDaRenata = Usuario.CriarPeloSeed(renata, "renataA", agora);
        var contaDaAna = Usuario.CadastrarAluno(ana, cc, "Ana", "anaSouza", "hash", agora);
        var contaDoBruno = Usuario.CadastrarAluno(bruno, cc, "Bruno", "brunoLima", "hash", agora);
        var contaDaCarla = Usuario.CadastrarAluno(carla, cc, "Carla", "carlaDias", "hash", agora);
        var m90 = CatalogoDeMedalhas.M03();
        m90.Codigo = "M90";
        m90.RestricaoCurso = "CC";

        await SalvarAsync(
            factory,
            ano,
            cc,
            adm,
            bd,
            turma,
            renata,
            secretaria,
            ana,
            bruno,
            carla,
            diego,
            eva,
            new VinculoProfessor(renata, turma),
            new Matricula(ana, turma),
            new Matricula(bruno, turma),
            new Matricula(carla, turma),
            new Matricula(eva, turma),
            contaDaRenata,
            Usuario.CriarPeloSeed(secretaria, "secretaria", agora),
            contaDaAna,
            contaDoBruno,
            contaDaCarla,
            Usuario.CadastrarAluno(diego, adm, "Diego", "diegoReis", "hash", agora),
            CatalogoDeMedalhas.M01(),
            CatalogoDeMedalhas.M03(),
            m90);

        return new Mundo(factory, relogio, turma.Id, contaDaRenata.Id, contaDaAna.Id, contaDoBruno.Id, contaDaCarla.Id);
    }

    private static async Task<Guid> ConcederAsync(Mundo mundo, Guid aluno, string item)
    {
        await using var scope = mundo.Factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<ConcederMedalhaHandler>()
            .ConcederAsync(mundo.Renata, mundo.Turma, aluno, item, Ct)).Id;
    }

    private static async Task AnularAsync(MaxxApiFactory factory, Guid medalha)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<AnularMedalhaHandler>()
            .AnularAsync(medalha, "secretaria@unieduk.com.br", Ct));
    }

    private static async Task MarcarExAlunaAsync(MaxxApiFactory factory, Guid aluna)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var usuario = await db.Set<Usuario>().SingleAsync(linha => linha.Id == aluna, Ct);
        usuario.MarcarExAluno(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(Ct);
    }

    // Processa até não haver trabalho disponível no relógio de agora.
    private static async Task EsvaziarAsync(MaxxApiFactory factory)
    {
        var consumidor = factory.Services.GetRequiredService<ConsumidorDeTrabalhos>();
        while (await consumidor.ProcessarProximoAsync(Ct))
        {
        }
    }

    private static async Task<List<T>> LinhasAsync<T>(MaxxApiFactory factory)
        where T : class
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<T>().AsNoTracking().ToListAsync(Ct);
    }

    private sealed record Mundo(
        MaxxApiFactory Factory, RelogioDeTeste Relogio, Guid Turma, Guid Renata, Guid Ana, Guid Bruno, Guid Carla);

    private sealed class RelogioDeTeste : TimeProvider
    {
        // Em segundo cheio: o Postgres guarda microssegundos, e a comparação é exata.
        private DateTimeOffset _agora = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan quanto) => _agora += quanto;
    }
}
