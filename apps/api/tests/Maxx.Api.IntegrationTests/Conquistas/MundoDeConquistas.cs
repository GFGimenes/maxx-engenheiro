using Maxx.Application.Conquistas;
using Maxx.Application.Fila;
using Maxx.Application.Medalhas;
using Maxx.Application.PremiosParticipacao;
using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Infrastructure.Conquistas;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Maxx.Api.IntegrationTests.BancoDeTeste;

namespace Maxx.Api.IntegrationTests.Conquistas;

// Um banco migrado com o catálogo real (as 18 conquistas, as medalhas e os prêmios de
// data/catalogo/) e um calendário em volta de hoje, no relógio de teste: hoje cai no
// bimestre 2 do semestre 1. Ana e Bruno estudam Banco de Dados e Redes, as duas turmas da
// Renata; a Secretaria é a instituição.
internal sealed class MundoDeConquistas : IAsyncDisposable
{
    private static readonly Catalogo _catalogo = CatalogoReader.Ler(Repositorio.DiretorioDoCatalogo).Catalogo!;

    private MundoDeConquistas(MaxxApiFactory factory, RelogioDeTeste relogio)
    {
        Factory = factory;
        Relogio = relogio;
    }

    public MaxxApiFactory Factory { get; }

    public RelogioDeTeste Relogio { get; }

    public AnoLetivo Ano { get; private set; } = null!;

    public Turma BancoDeDados { get; private set; } = null!;

    public Turma Redes { get; private set; } = null!;

    public Usuario Renata { get; private set; } = null!;

    public Usuario Secretaria { get; private set; } = null!;

    public Usuario Ana { get; private set; } = null!;

    public Usuario Bruno { get; private set; } = null!;

    public Curso Cc { get; private set; } = null!;

    public SemestreLetivo Semestre1 => Ano.Semestres[0];

    public Bimestre BimestreDeHoje => Ano.Bimestres.Single(bimestre => bimestre.Numero == 2);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static ItemPremioParticipacao ItemDeParticipacao(string codigo) =>
        _catalogo.Itens.OfType<ItemPremioParticipacao>().Single(item => item.Codigo == codigo);

    public static async Task<MundoDeConquistas> CriarAsync(PostgresFixture postgres)
    {
        var relogio = new RelogioDeTeste();
        var factory = new MaxxApiFactory(
            await postgres.CreateDatabaseAsync(),
            servicos: servicos => servicos.Replace(ServiceDescriptor.Singleton<TimeProvider>(relogio)));
        await factory.MigrateAsync(Ct);
        var mundo = new MundoDeConquistas(factory, relogio);

        var hoje = FusoHorario.DataLocal(relogio.GetUtcNow());
        var inicio = hoje.AddDays(-75);
        Bimestre Bimestre(int numero) => new(numero, inicio.AddDays((numero - 1) * 51), inicio.AddDays(((numero - 1) * 51) + 49));
        mundo.Ano = new AnoLetivo(
            hoje.Year, new SemestreLetivo(hoje.Year, 1, Bimestre(1), Bimestre(2)), new SemestreLetivo(hoje.Year, 2, Bimestre(3), Bimestre(4)));

        mundo.Cc = new Curso("Ciência da Computação", "CC", 8);
        var bd = new Materia(mundo.Cc, "Banco de Dados");
        var redes = new Materia(mundo.Cc, "Redes");
        mundo.BancoDeDados = new Turma(bd, mundo.Semestre1);
        mundo.Redes = new Turma(redes, mundo.Semestre1);
        var agora = relogio.GetUtcNow();
        var renata = CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque");
        var secretaria = CadastroElegivel.Instituicao("secretaria@unieduk.com.br", "Secretaria UniMax");
        var ana = CadastroElegivel.Aluno("20260101", "ana.souza@al.unieduk.com.br", "Ana Souza", mundo.Cc, 3);
        var bruno = CadastroElegivel.Aluno("20260102", "bruno.lima@al.unieduk.com.br", "Bruno Lima", mundo.Cc, 3);
        mundo.Renata = Usuario.CriarPeloSeed(renata, "renataA", agora);
        mundo.Secretaria = Usuario.CriarPeloSeed(secretaria, "secretaria", agora);
        mundo.Ana = Usuario.CadastrarAluno(ana, mundo.Cc, "Ana", "anaSouza", "hash", agora);
        mundo.Bruno = Usuario.CadastrarAluno(bruno, mundo.Cc, "Bruno", "brunoLima", "hash", agora);

        await SalvarAsync(
            factory,
            [
                .. _catalogo.Familias.Select(CatalogoRowMapper.ParaRow),
                .. _catalogo.Itens.Select(CatalogoRowMapper.ParaRow),
                mundo.Ano,
                mundo.Cc,
                bd,
                redes,
                mundo.BancoDeDados,
                mundo.Redes,
                renata,
                secretaria,
                ana,
                bruno,
                new VinculoProfessor(renata, mundo.BancoDeDados),
                new VinculoProfessor(renata, mundo.Redes),
                new Matricula(ana, mundo.BancoDeDados),
                new Matricula(ana, mundo.Redes),
                new Matricula(bruno, mundo.BancoDeDados),
                mundo.Renata,
                mundo.Secretaria,
                mundo.Ana,
                mundo.Bruno,
            ]);
        return mundo;
    }

    public async Task<Guid> ConcederAsync(Usuario aluno, string item, Turma? turma = null)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<ConcederMedalhaHandler>()
            .ConcederAsync(Renata.Id, (turma ?? BancoDeDados).Id, aluno.Id, item, Ct)).Id;
    }

    // As cinco medalhas que a C01 pede, todas em Banco de Dados no bimestre de hoje: as
    // vagas de M01 e M02 são duas por bimestre, a de M03, uma.
    public async Task<List<Guid>> CincoNoBimestreAsync(Usuario aluno)
    {
        List<Guid> medalhas = [];
        foreach (var item in new[] { "M01", "M01", "M02", "M02", "M03" })
        {
            medalhas.Add(await ConcederAsync(aluno, item));
        }

        return medalhas;
    }

    public async Task AnularAsync(Guid medalha)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<AnularMedalhaHandler>()
            .AnularAsync(medalha, "secretaria@unieduk.com.br", Ct));
    }

    // Um prêmio de participação aprovado agora, num evento digitado que começou ontem, com o
    // trabalho do fim da janela agendado como a aprovação agenda.
    public async Task<PremioParticipacao> AprovadoAsync(Usuario aluno, string item, string nomeDoEvento)
    {
        var agora = Relogio.GetUtcNow();
        var ontem = FusoHorario.DataLocal(agora).AddDays(-1);
        var evento = Evento.TextoLivre(nomeDoEvento, ontem, ontem, aluno);
        var premio = PremioParticipacao.Reivindicar(ItemDeParticipacao(item), 1, aluno, Cc, evento, null, null, agora).Premio!;
        Assert.Null(premio.Aprovar(aluno, Secretaria, agora));

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        db.AddRange(evento, premio);
        scope.ServiceProvider.GetRequiredService<IFilaDeTrabalhos>()
            .Enfileirar(new TornarPremioDefinitivo(premio.Id), premio.FimDaJanela);
        await db.SaveChangesAsync(Ct);
        return premio;
    }

    // Processa até não haver trabalho disponível no relógio de agora.
    public async Task EsvaziarAsync()
    {
        var consumidor = Factory.Services.GetRequiredService<ConsumidorDeTrabalhos>();
        while (await consumidor.ProcessarProximoAsync(Ct))
        {
        }
    }

    // Enfileira um trabalho e grava, como faria a escrita que o origina.
    public async Task EnfileirarAsync<TCarga>(TCarga carga)
        where TCarga : ICargaDeTrabalho
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IFilaDeTrabalhos>().Enfileirar(carga);
        await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().SaveChangesAsync(Ct);
    }

    public async Task<List<T>> LinhasAsync<T>()
        where T : class
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<T>().AsNoTracking().ToListAsync(Ct);
    }

    // Grava o fechamento da instância como a verificação grava, sem enfileirar nada: isola a
    // reavaliação por fato do fechamento em si.
    public async Task RegistrarFechamentoAsync(InstanciaDePeriodo instancia)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        db.Add(new FechamentoDePeriodoRow
        {
            TipoDePeriodo = instancia.Tipo.ToString().ToLowerInvariant(),
            ReferenciaDoPeriodo = instancia.Referencia,
            FechadoEm = Relogio.GetUtcNow(),
        });
        await db.SaveChangesAsync(Ct);
    }

    // Uma verificação de fechamentos agora, e o que ela abrir, até a fila esvaziar.
    public async Task VerificarFechamentosAsync()
    {
        await EnfileirarAsync(new VerificarFechamentos());
        await EsvaziarAsync();
    }

    public ValueTask DisposeAsync() => Factory.DisposeAsync();
}

internal sealed class RelogioDeTeste : TimeProvider
{
    // Em segundo cheio: o Postgres guarda microssegundos, e a comparação é exata.
    private DateTimeOffset _agora = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    public override DateTimeOffset GetUtcNow() => _agora;

    public void Avancar(TimeSpan quanto) => _agora += quanto;
}
