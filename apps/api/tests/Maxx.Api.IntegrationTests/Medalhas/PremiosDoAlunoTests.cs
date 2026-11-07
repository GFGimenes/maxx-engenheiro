using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Maxx.Api.Autenticacao;
using Maxx.Api.Premios;
using Maxx.Application.Medalhas;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Infrastructure.Autenticacao;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.BancoDeTeste;

namespace Maxx.Api.IntegrationTests.Medalhas;

// A leitura da tela de Troféus pelo próprio aluno, com o catálogo de verdade do seed base
// e a raridade escrita pelo worker.
public sealed class PremiosDoAlunoTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string _senha = "senha-boa-123";
    private const string _ana = "ana.souza@al.unieduk.com.br";
    private const string _renata = "renata@prof.unieduk.com.br";

    // Dentro do bimestre 4 de 2026, no calendário do seed.
    private static readonly DateTimeOffset _concessao = new(2026, 11, 10, 13, 0, 0, TimeSpan.Zero);

    private static readonly Uri _premios = new("/api/v1/eu/premios", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Aluno_ve_o_catalogo_inteiro_com_criterio_familia_e_o_que_ja_obteve()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        await EsvaziarAsync(factory);
        using var client = await ComoAsync(factory, _ana);

        var premios = (await client.GetFromJsonAsync<PremiosDoAlunoResponse>(_premios, Ct))!;

        // Os 46 itens do catálogo; o restrito a ADM fica de fora para quem é de CC.
        Assert.Equal(46, premios.Itens.Count);
        Assert.DoesNotContain(premios.Itens, item => item.Codigo == "M90");
        Assert.Equal(8, premios.Familias.Count);
        Assert.Equal(30, premios.Pontos);

        var m01 = premios.Itens.Single(item => item.Codigo == "M01");
        Assert.Equal(("medalha", "Nota 10 em prova", "bronze", 30, "nota-prova", 1), (m01.Tipo, m01.Nome, m01.Tier, m01.Pontos, m01.Familia, m01.OrdemNaFamilia));
        Assert.StartsWith("Tirar 10 em uma prova da matéria.", m01.Criterio, StringComparison.Ordinal);
        var obtido = Assert.Single(m01.Obtidos);
        Assert.Equal((mundo.MedalhaDaAna, "bronze", 30, "Banco de Dados", 4, _concessao), (obtido.Id, obtido.Tier, obtido.Pontos, obtido.Materia, obtido.Bimestre, obtido.ObtidoEm));

        // A anulada não aparece; o que falta vem sem obtidos, com o critério.
        Assert.Empty(premios.Itens.Single(item => item.Codigo == "M03").Obtidos);
        var c01 = premios.Itens.Single(item => item.Codigo == "C01");
        Assert.Equal(("conquista", "volume-medalhas"), (c01.Tipo, c01.Familia));
        Assert.Empty(c01.Obtidos);
        Assert.Contains(premios.Itens, item => item.Tipo == "premioParticipacao");
    }

    [Fact]
    public async Task Raridade_vem_do_worker_e_o_seed_pede_o_primeiro_calculo()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var client = await ComoAsync(factory, _ana);

        // Antes de o worker rodar: ainda sem raridade.
        var antes = (await client.GetFromJsonAsync<PremiosDoAlunoResponse>(_premios, Ct))!;
        Assert.All(antes.Itens, item => Assert.Null(item.Raridade));

        await EsvaziarAsync(factory);
        var depois = (await client.GetFromJsonAsync<PremiosDoAlunoResponse>(_premios, Ct))!;

        // Ana e Bruno têm conta; só Ana tem M01 ativa. A M03 anulada não conta.
        Assert.Equal(new RaridadeResponse("comum", 50.0), depois.Itens.Single(item => item.Codigo == "M01").Raridade);
        Assert.Equal(new RaridadeResponse("lendario", 0.0), depois.Itens.Single(item => item.Codigo == "M03").Raridade);
        Assert.All(depois.Itens, item => Assert.NotNull(item.Raridade));
    }

    [Fact]
    public async Task O_seed_de_novo_nao_pede_outro_calculo()
    {
        await using var factory = await MigradoAsync(postgres);
        await factory.SeedBaseAsync(Ct);
        Assert.Equal(1, await PedidosDeRaridadeAsync(factory));

        await factory.SeedBaseAsync(Ct);
        Assert.Equal(1, await PedidosDeRaridadeAsync(factory));

        await EsvaziarAsync(factory);
        await factory.SeedBaseAsync(Ct);
        Assert.Equal(0, await PedidosDeRaridadeAsync(factory));
    }

    [Fact]
    public async Task Medalha_de_outro_aluno_nao_entra_nos_meus_obtidos()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var client = await ComoAsync(factory, "bruno.lima@al.unieduk.com.br");

        var premios = (await client.GetFromJsonAsync<PremiosDoAlunoResponse>(_premios, Ct))!;

        Assert.Equal(0, premios.Pontos);
        Assert.All(premios.Itens, item => Assert.Empty(item.Obtidos));
    }

    [Fact]
    public async Task So_aluno_le_os_proprios_premios()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var professor = await ComoAsync(factory, _renata);
        using var anonimo = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Forbidden, (await professor.GetAsync(_premios, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync(_premios, Ct)).StatusCode);
    }

    private async Task<Mundo> MundoAsync()
    {
        var factory = await MigradoAsync(postgres);
        await factory.SeedBaseAsync(Ct);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var semestre = await db.Set<SemestreLetivo>().Include(linha => linha.Bimestres)
            .SingleAsync(linha => linha.Ano == 2026 && linha.Numero == 2, Ct);
        var m01 = CatalogoRowMapper.ParaMedalha(await db.Set<ItemCatalogoRow>().SingleAsync(item => item.Codigo == "M01", Ct));
        var m03 = CatalogoRowMapper.ParaMedalha(await db.Set<ItemCatalogoRow>().SingleAsync(item => item.Codigo == "M03", Ct));

        var hash = new Argon2idSenhaHasher().Hash(_senha);
        var cc = new Curso("Ciência da Computação", "CC", 8);
        var adm = new Curso("Administração", "ADM", 8);
        var bd = new Materia(cc, "Banco de Dados");
        var turma = new Turma(bd, semestre);
        var renata = CadastroElegivel.Professor(_renata, "Renata Albuquerque");
        var ana = CadastroElegivel.Aluno("20260101", _ana, "Ana Souza", cc, 3);
        var bruno = CadastroElegivel.Aluno("20260102", "bruno.lima@al.unieduk.com.br", "Bruno Lima", cc, 3);
        var contaDaRenata = ComSenha(Usuario.CriarPeloSeed(renata, "renataA", _concessao), hash);
        var contaDaAna = Usuario.CadastrarAluno(ana, cc, "Ana", "anaSouza", hash, _concessao);
        var contaDoBruno = Usuario.CadastrarAluno(bruno, cc, "Bruno", "brunoLima", hash, _concessao);
        var medalha = Medalha.Conceder(m01, 1, contaDaAna, "CC", turma, semestre, contaDaRenata, [], _concessao).Medalha!;
        var anulada = Medalha.Conceder(m03, 1, contaDaAna, "CC", turma, semestre, contaDaRenata, [], _concessao).Medalha!;
        anulada.Anular(contaDaRenata.Id, _concessao.AddHours(1));
        var m90 = CatalogoDeMedalhas.M03();
        m90.Codigo = "M90";
        m90.RestricaoCurso = "ADM";

        await SalvarAsync(
            factory, cc, adm, bd, turma, renata, ana, bruno, contaDaRenata, contaDaAna, contaDoBruno, medalha, anulada, m90);
        return new Mundo(factory, medalha.Id);
    }

    private static Usuario ComSenha(Usuario usuario, string hash)
    {
        usuario.DefinirSenha(hash);
        return usuario;
    }

    private static async Task<HttpClient> ComoAsync(MaxxApiFactory factory, string email)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative), new { identificador = email, senha = _senha }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = (await response.Content.ReadFromJsonAsync<SessaoResponse>(Ct))!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task EsvaziarAsync(MaxxApiFactory factory)
    {
        var consumidor = factory.Services.GetRequiredService<ConsumidorDeTrabalhos>();
        while (await consumidor.ProcessarProximoAsync(Ct))
        {
        }
    }

    private static async Task<int> PedidosDeRaridadeAsync(MaxxApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<Trabalho>()
            .CountAsync(trabalho => trabalho.Tipo == RecalcularRaridade.Tipo, Ct);
    }

    private sealed record Mundo(MaxxApiFactory Factory, Guid MedalhaDaAna);
}
