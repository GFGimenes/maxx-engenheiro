using Maxx.Api.Medalhas;
using Maxx.Application.Medalhas;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.BancoDeTeste;

namespace Maxx.Api.IntegrationTests.Medalhas;

// A anulação da fase 1, pelo comando `anular-medalha`, contra o Postgres real: o mesmo
// caminho que o Program roda, com a saída capturada.
public sealed class AnulacaoDeMedalhaTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string _secretaria = "secretaria@unieduk.com.br";
    private const string _coordenacao = "coordenacao@prof.unieduk.com.br";
    private const string _renata = "renata@prof.unieduk.com.br";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Instituicao_anula_grava_quem_e_quando_e_enfileira_o_recalculo()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        var medalha = await ConcederAsync(mundo, "M03");
        var antes = DateTimeOffset.UtcNow;

        var (codigo, saida, _) = await AnularAsync(factory, medalha.ToString(), _secretaria);

        Assert.Equal(0, codigo);
        Assert.Equal($"Medalha {medalha} anulada.", saida.Trim());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var anulada = await db.Set<Medalha>().SingleAsync(Ct);
        Assert.False(anulada.EstaAtiva);
        Assert.Equal(mundo.Secretaria, anulada.AnuladaPorId);
        Assert.InRange(anulada.AnuladaEm!.Value, antes, DateTimeOffset.UtcNow);
        Assert.Equal(mundo.Renata, anulada.ConcedidaPorId);
        Assert.Contains(await db.Set<Trabalho>().Select(trabalho => trabalho.Tipo).ToListAsync(Ct), tipo => tipo == RecalcularRaridade.Tipo);
    }

    [Fact]
    public async Task Medalha_anulada_libera_a_vaga_para_nova_concessao()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        var primeira = await ConcederAsync(mundo, "M03");
        await AnularAsync(factory, primeira.ToString(), _secretaria);

        var segunda = await ConcederAsync(mundo, "M03");

        await using var scope = factory.Services.CreateAsyncScope();
        var medalhas = await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<Medalha>().ToListAsync(Ct);
        Assert.Equal(2, medalhas.Count);
        Assert.True(medalhas.Single(medalha => medalha.Id == segunda).EstaAtiva);
    }

    [Fact]
    public async Task Anular_de_novo_nao_muda_nada()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        var medalha = await ConcederAsync(mundo, "M03");
        await AnularAsync(factory, medalha.ToString(), _secretaria);
        var primeira = await AnuladaEmAsync(factory);

        var (codigo, saida, _) = await AnularAsync(factory, medalha.ToString(), _coordenacao);

        Assert.Equal(0, codigo);
        Assert.Equal($"Medalha {medalha} já estava anulada.", saida.Trim());
        Assert.Equal(primeira, await AnuladaEmAsync(factory));
    }

    [Fact]
    public async Task Duas_anulacoes_simultaneas_gravam_uma_so()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        var medalha = await ConcederAsync(mundo, "M03");

        var resultados = await Task.WhenAll(
            AnularAsync(factory, medalha.ToString(), _secretaria), AnularAsync(factory, medalha.ToString(), _coordenacao));

        Assert.All(resultados, resultado => Assert.Equal(0, resultado.Codigo));
        Assert.Single(resultados, resultado => resultado.Saida.Contains("já estava anulada", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Conta_de_professor_com_instituicao_tambem_anula()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        var medalha = await ConcederAsync(mundo, "M03");

        Assert.Equal(0, (await AnularAsync(factory, medalha.ToString(), _coordenacao)).Codigo);
    }

    [Fact]
    public async Task Quem_nao_e_instituicao_nao_anula()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        var medalha = await ConcederAsync(mundo, "M03");

        var (codigo, _, erro) = await AnularAsync(factory, medalha.ToString(), _renata);
        var desconhecido = await AnularAsync(factory, medalha.ToString(), "ninguem@unieduk.com.br");

        Assert.Equal(1, codigo);
        Assert.Equal($"{_renata} não é uma conta da instituição.", erro.Trim());
        Assert.Equal(1, desconhecido.Codigo);
        Assert.Null(await AnuladaEmAsync(factory));
    }

    [Fact]
    public async Task Medalha_inexistente_e_uso_errado()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        var id = Guid.CreateVersion7();

        var inexistente = await AnularAsync(factory, id.ToString(), _secretaria);
        var semInstituicao = await ExecutarAsync(factory, [ComandoDeAnulacao.Nome, id.ToString()]);
        var idInvalido = await AnularAsync(factory, "nao-e-um-id", _secretaria);

        Assert.Equal((1, $"Nenhuma medalha com o id {id}."), (inexistente.Codigo, inexistente.Erro.Trim()));
        Assert.Equal((2, ComandoDeAnulacao.Uso), (semInstituicao.Codigo, semInstituicao.Erro.Trim()));
        Assert.Equal(2, idInvalido.Codigo);
    }

    private async Task<Mundo> MundoAsync()
    {
        var factory = await MigradoAsync(postgres);

        var hoje = FusoHorario.DataLocal(DateTimeOffset.UtcNow);
        var inicio = hoje.AddDays(-75);
        Bimestre Bimestre(int numero) => new(numero, inicio.AddDays((numero - 1) * 51), inicio.AddDays(((numero - 1) * 51) + 49));
        var ano = new AnoLetivo(
            hoje.Year, new SemestreLetivo(hoje.Year, 1, Bimestre(1), Bimestre(2)), new SemestreLetivo(hoje.Year, 2, Bimestre(3), Bimestre(4)));
        var cc = new Curso("Ciência da Computação", "CC", 8);
        var bd = new Materia(cc, "Banco de Dados");
        var turma = new Turma(bd, ano.Semestres[0]);
        var agora = DateTimeOffset.UtcNow;
        var renata = CadastroElegivel.Professor(_renata, "Renata Albuquerque");
        var secretaria = CadastroElegivel.Instituicao(_secretaria, "Secretaria UniMax");
        var coordenacao = CadastroElegivel.Professor(_coordenacao, "Coordenação", tambemInstituicao: true);
        var ana = CadastroElegivel.Aluno("20260101", "ana.souza@al.unieduk.com.br", "Ana Souza", cc, 3);
        var contaDaRenata = Usuario.CriarPeloSeed(renata, "renataA", agora);
        var contaDaSecretaria = Usuario.CriarPeloSeed(secretaria, "secretaria", agora);
        var contaDaAna = Usuario.CadastrarAluno(ana, cc, "Ana", "anaSouza", "hash", agora);

        await SalvarAsync(
            factory,
            ano,
            cc,
            bd,
            turma,
            renata,
            secretaria,
            coordenacao,
            ana,
            new VinculoProfessor(renata, turma),
            new Matricula(ana, turma),
            contaDaRenata,
            contaDaSecretaria,
            Usuario.CriarPeloSeed(coordenacao, "coordenacao", agora),
            contaDaAna,
            CatalogoDeMedalhas.M03());

        return new Mundo(factory, turma.Id, contaDaRenata.Id, contaDaSecretaria.Id, contaDaAna.Id);
    }

    private static async Task<Guid> ConcederAsync(Mundo mundo, string item)
    {
        await using var scope = mundo.Factory.Services.CreateAsyncScope();
        var medalha = await scope.ServiceProvider.GetRequiredService<ConcederMedalhaHandler>()
            .ConcederAsync(mundo.Renata, mundo.Turma, mundo.Ana, item, Ct);
        return medalha.Id;
    }

    private static Task<Resultado> AnularAsync(MaxxApiFactory factory, string medalha, string email) =>
        ExecutarAsync(factory, [ComandoDeAnulacao.Nome, medalha, "--instituicao", email]);

    private static async Task<Resultado> ExecutarAsync(MaxxApiFactory factory, string[] args)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        using var saida = new StringWriter();
        using var erro = new StringWriter();
        var codigo = await ComandoDeAnulacao.ExecutarAsync(scope.ServiceProvider, args, saida, erro, Ct);
        return new Resultado(codigo, saida.ToString(), erro.ToString());
    }

    private static async Task<DateTimeOffset?> AnuladaEmAsync(MaxxApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<Medalha>().SingleAsync(Ct)).AnuladaEm;
    }

    private sealed record Resultado(int Codigo, string Saida, string Erro);

    private sealed record Mundo(MaxxApiFactory Factory, Guid Turma, Guid Renata, Guid Secretaria, Guid Ana);
}
