using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Maxx.Api.Autenticacao;
using Maxx.Api.Medalhas;
using Maxx.Application.Conquistas;
using Maxx.Application.Medalhas;
using Maxx.Application.Ranking;
using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Infrastructure.Autenticacao;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.BancoDeTeste;

namespace Maxx.Api.IntegrationTests.Medalhas;

// Turmas, alunos e concessão de medalha pelo professor, contra o Postgres real
// (fluxos/03-concessao-de-medalha.md). O calendário é montado em volta de hoje, para a
// turma estar sempre no semestre vigente e hoje cair num bimestre.
public sealed class ConcessaoDeMedalhaTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string _senha = "senha-boa-123";
    private const string _renata = "renata@prof.unieduk.com.br";
    private const string _paulo = "paulo@prof.unieduk.com.br";
    private const string _ana = "ana.souza@al.unieduk.com.br";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Professor_ve_so_as_turmas_vigentes_dele_com_os_alunos_que_tem_conta()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var client = await ComoAsync(factory, _renata);

        var turmas = await client.GetFromJsonAsync<List<TurmaDoProfessorResponse>>("/api/v1/professor/turmas", Ct);

        var turma = Assert.Single(turmas!);
        Assert.Equal(new TurmaDoProfessorResponse(mundo.BancoDeDados, "Banco de Dados", "CC", $"{mundo.Ano}/1", 1), turma);
    }

    [Fact]
    public async Task A_lista_da_turma_traz_so_aluno_com_conta_e_que_nao_e_ex_aluno()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var client = await ComoAsync(factory, _renata);

        var alunos = await AlunosAsync(client, mundo.BancoDeDados);

        // Bruno está matriculado e não tem conta; Carla tem conta e é ex-aluna.
        var ana = Assert.Single(alunos);
        Assert.Equal(mundo.Ana, ana.Id);
        Assert.Equal("Ana", ana.NomeDeExibicao);
        Assert.Equal("@anaSouza-CC", ana.Arroba);
        Assert.Empty(ana.Medalhas);
    }

    [Fact]
    public async Task O_catalogo_do_modal_traz_as_medalhas_com_tier_e_limite()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var client = await ComoAsync(factory, _renata);

        var medalhas = await client.GetFromJsonAsync<List<ItemDeMedalhaResponse>>("/api/v1/professor/medalhas", Ct);

        Assert.Equal(["M01", "M03"], medalhas!.Select(item => item.Codigo));
        Assert.Equal(
            new ItemDeMedalhaResponse("M01", "Nota 10 em prova", "Tirar 10 em uma prova da matéria.", "bronze", 30, "nota", "bimestre", 2),
            medalhas![0]);
    }

    [Fact]
    public async Task Concessao_grava_turma_materia_bimestre_snapshot_e_auditoria_e_enfileira_os_trabalhos()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var client = await ComoAsync(factory, _renata);
        var antes = DateTimeOffset.UtcNow;

        var response = await ConcederAsync(client, mundo.BancoDeDados, mundo.Ana, "M01");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var concedida = (await response.Content.ReadFromJsonAsync<MedalhaConcedidaResponse>(Ct))!;
        Assert.Equal(("M01", "bronze", 30, 1), (concedida.Item, concedida.Tier, concedida.Pontos, concedida.Ocorrencia));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var medalha = await db.Set<Medalha>().SingleAsync(Ct);
        Assert.Equal(concedida.Id, medalha.Id);
        Assert.Equal(mundo.Ana, medalha.AlunoId);
        Assert.Equal(mundo.BancoDeDados, medalha.TurmaId);
        Assert.Equal(mundo.MateriaBancoDeDados, medalha.MateriaId);
        Assert.Equal(mundo.BimestreDeHoje, medalha.BimestreId);
        Assert.Equal((Tier.Bronze, 30, 1), (medalha.Tier, medalha.Pontos, medalha.VersaoDoItem));
        Assert.Equal(mundo.Renata, medalha.ConcedidaPorId);
        Assert.InRange(medalha.ConcedidaEm, antes, DateTimeOffset.UtcNow);
        Assert.Null(medalha.AnuladaEm);

        var trabalhos = await db.Set<Trabalho>().OrderBy(trabalho => trabalho.Id).ToListAsync(Ct);
        Assert.Equal(
            [NotificarMedalhaRecebida.Tipo, ReavaliarConquistas.Tipo, RecalcularRanking.Tipo, RecalcularRaridade.Tipo], trabalhos.Select(trabalho => trabalho.Tipo));
        using var carga = JsonDocument.Parse(trabalhos[0].Carga);
        Assert.Equal(medalha.Id, carga.RootElement.GetProperty("medalhaId").GetGuid());
        using var reavaliacao = JsonDocument.Parse(trabalhos[1].Carga);
        Assert.Equal(mundo.Ana, reavaliacao.RootElement.GetProperty("alunoId").GetGuid());
        Assert.Equal(medalha.Id, reavaliacao.RootElement.GetProperty("medalhaId").GetGuid());

        var naLista = Assert.Single(Assert.Single(await AlunosAsync(client, mundo.BancoDeDados)).Medalhas);
        Assert.Equal((medalha.Id, "M01", 2), (naLista.Id, naLista.Item, naLista.Bimestre));
    }

    [Fact]
    public async Task Limite_por_periodo_recusa_a_concessao_que_passa_dele()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var client = await ComoAsync(factory, _renata);

        Assert.Equal(HttpStatusCode.Created, (await ConcederAsync(client, mundo.BancoDeDados, mundo.Ana, "M01")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await ConcederAsync(client, mundo.BancoDeDados, mundo.Ana, "M01")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await ConcederAsync(client, mundo.BancoDeDados, mundo.Ana, "M03")).StatusCode);

        await AssertRecusaAsync(await ConcederAsync(client, mundo.BancoDeDados, mundo.Ana, "M01"), HttpStatusCode.Conflict, "limite-atingido");
        await AssertRecusaAsync(await ConcederAsync(client, mundo.BancoDeDados, mundo.Ana, "M03"), HttpStatusCode.Conflict, "limite-atingido");
    }

    [Fact]
    public async Task Concessoes_simultaneas_respeitam_o_limite_sem_erro_interno()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var client = await ComoAsync(factory, _renata);

        // M01 tem duas vagas no bimestre; M03, uma.
        var m01 = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => ConcederAsync(client, mundo.BancoDeDados, mundo.Ana, "M01")));
        var m03 = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => ConcederAsync(client, mundo.BancoDeDados, mundo.Ana, "M03")));

        Assert.Equal(2, m01.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, m03.Count(response => response.StatusCode == HttpStatusCode.Created));
        foreach (var recusada in m01.Concat(m03).Where(response => response.StatusCode != HttpStatusCode.Created))
        {
            await AssertRecusaAsync(recusada, HttpStatusCode.Conflict, "limite-atingido");
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var ocorrencias = await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<Medalha>()
            .Where(medalha => medalha.ItemCodigo == "M01").Select(medalha => medalha.Ocorrencia).OrderBy(n => n).ToListAsync(Ct);
        Assert.Equal([1, 2], ocorrencias);
    }

    [Fact]
    public async Task Turma_de_outro_professor_esta_fora_do_escopo()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var client = await ComoAsync(factory, _renata);

        await AssertRecusaAsync(
            await client.GetAsync(new Uri($"/api/v1/professor/turmas/{mundo.Redes}/alunos", UriKind.Relative), Ct),
            HttpStatusCode.Forbidden,
            "fora-do-escopo");
        await AssertRecusaAsync(
            await ConcederAsync(client, mundo.Redes, mundo.Diego, "M01"), HttpStatusCode.Forbidden, "fora-do-escopo");

        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<Medalha>().ToListAsync(Ct));
    }

    [Fact]
    public async Task Aluno_nao_usa_as_rotas_do_professor()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var client = await ComoAsync(factory, _ana);

        await AssertRecusaAsync(
            await client.GetAsync(new Uri("/api/v1/professor/turmas", UriKind.Relative), Ct), HttpStatusCode.Forbidden, "sem-permissao");
        await AssertRecusaAsync(
            await ConcederAsync(client, mundo.BancoDeDados, mundo.Ana, "M01"), HttpStatusCode.Forbidden, "sem-permissao");
    }

    [Fact]
    public async Task Aluno_sem_conta_ou_de_outra_turma_nao_recebe()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var client = await ComoAsync(factory, _renata);

        // O id da linha do cadastro do Bruno não é conta; Diego tem conta, mas é de Redes.
        await AssertRecusaAsync(
            await ConcederAsync(client, mundo.BancoDeDados, mundo.CadastroDoBruno, "M01"), HttpStatusCode.NotFound, "aluno-fora-da-turma");
        await AssertRecusaAsync(
            await ConcederAsync(client, mundo.BancoDeDados, mundo.Diego, "M01"), HttpStatusCode.NotFound, "aluno-fora-da-turma");
        await AssertRecusaAsync(
            await ConcederAsync(client, mundo.BancoDeDados, mundo.Renata, "M01"), HttpStatusCode.NotFound, "aluno-fora-da-turma");
    }

    [Fact]
    public async Task Ex_aluno_nao_recebe()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var client = await ComoAsync(factory, _renata);

        await AssertRecusaAsync(await ConcederAsync(client, mundo.BancoDeDados, mundo.Carla, "M01"), HttpStatusCode.Conflict, "ex-aluno");
    }

    [Fact]
    public async Task Item_que_nao_e_medalha_do_catalogo_e_recusado()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var client = await ComoAsync(factory, _renata);

        await AssertRecusaAsync(await ConcederAsync(client, mundo.BancoDeDados, mundo.Ana, "M99"), HttpStatusCode.NotFound, "item-nao-encontrado");
        await AssertRecusaAsync(await ConcederAsync(client, mundo.BancoDeDados, mundo.Ana, ""), HttpStatusCode.BadRequest, "dados-invalidos");
    }

    private async Task<Mundo> MundoAsync()
    {
        var factory = await MigradoAsync(postgres);

        // Hoje cai no bimestre 2 do semestre 1 de um ano letivo montado em volta dele.
        var hoje = FusoHorario.DataLocal(DateTimeOffset.UtcNow);
        var inicio = hoje.AddDays(-75);
        Bimestre Bimestre(int numero) => new(numero, inicio.AddDays((numero - 1) * 51), inicio.AddDays(((numero - 1) * 51) + 49));
        var ano = new AnoLetivo(
            hoje.Year, new SemestreLetivo(hoje.Year, 1, Bimestre(1), Bimestre(2)), new SemestreLetivo(hoje.Year, 2, Bimestre(3), Bimestre(4)));
        var vigente = ano.Semestres[0];

        var cc = new Curso("Ciência da Computação", "CC", 8);
        var bd = new Materia(cc, "Banco de Dados");
        var redes = new Materia(cc, "Redes");
        var bancoDeDados = new Turma(bd, vigente);
        var bancoDeDadosFuturo = new Turma(bd, ano.Semestres[1]);
        var turmaDeRedes = new Turma(redes, vigente);

        var hash = new Argon2idSenhaHasher().Hash(_senha);
        var agora = DateTimeOffset.UtcNow;
        var renata = CadastroElegivel.Professor(_renata, "Renata Albuquerque");
        var paulo = CadastroElegivel.Professor(_paulo, "Paulo Mendes");
        var ana = CadastroElegivel.Aluno("20260101", _ana, "Ana Souza", cc, 3);
        var bruno = CadastroElegivel.Aluno("20260102", "bruno.lima@al.unieduk.com.br", "Bruno Lima", cc, 3);
        var carla = CadastroElegivel.Aluno("20260103", "carla.dias@al.unieduk.com.br", "Carla Dias", cc, 8);
        var diego = CadastroElegivel.Aluno("20260104", "diego.reis@al.unieduk.com.br", "Diego Reis", cc, 3);
        var contaDaRenata = ComSenha(Usuario.CriarPeloSeed(renata, "renataA", agora), hash);
        var contaDaAna = Usuario.CadastrarAluno(ana, cc, "Ana", "anaSouza", hash, agora);
        var contaDaCarla = Usuario.CadastrarAluno(carla, cc, "Carla", "carlaDias", hash, agora);
        contaDaCarla.MarcarExAluno(agora);
        var contaDoDiego = Usuario.CadastrarAluno(diego, cc, "Diego", "diegoReis", hash, agora);

        await SalvarAsync(
            factory,
            ano,
            cc,
            bd,
            redes,
            bancoDeDados,
            bancoDeDadosFuturo,
            turmaDeRedes,
            renata,
            paulo,
            ana,
            bruno,
            carla,
            diego,
            new VinculoProfessor(renata, bancoDeDados),
            new VinculoProfessor(renata, bancoDeDadosFuturo),
            new VinculoProfessor(paulo, turmaDeRedes),
            new Matricula(ana, bancoDeDados),
            new Matricula(bruno, bancoDeDados),
            new Matricula(carla, bancoDeDados),
            new Matricula(diego, turmaDeRedes),
            contaDaRenata,
            ComSenha(Usuario.CriarPeloSeed(paulo, "pauloM", agora), hash),
            contaDaAna,
            contaDaCarla,
            contaDoDiego,
            CatalogoDeMedalhas.M01(),
            CatalogoDeMedalhas.M03());

        return new Mundo(
            factory,
            hoje.Year,
            bancoDeDados.Id,
            bd.Id,
            turmaDeRedes.Id,
            vigente.Bimestres[1].Id,
            contaDaRenata.Id,
            contaDaAna.Id,
            contaDaCarla.Id,
            contaDoDiego.Id,
            bruno.Id);
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

    private static async Task<List<AlunoDaTurmaResponse>> AlunosAsync(HttpClient client, Guid turmaId) =>
        (await client.GetFromJsonAsync<List<AlunoDaTurmaResponse>>($"/api/v1/professor/turmas/{turmaId}/alunos", Ct))!;

    private static Task<HttpResponseMessage> ConcederAsync(HttpClient client, Guid turmaId, Guid alunoId, string item) =>
        client.PostAsJsonAsync(new Uri($"/api/v1/professor/turmas/{turmaId}/medalhas", UriKind.Relative), new { alunoId, item }, Ct);

    private static async Task AssertRecusaAsync(HttpResponseMessage response, HttpStatusCode status, string codigo)
    {
        Assert.Equal(status, response.StatusCode);
        using var corpo = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(codigo, corpo.RootElement.GetProperty("codigo").GetString());
    }

    private sealed record Mundo(
        MaxxApiFactory Factory,
        int Ano,
        Guid BancoDeDados,
        Guid MateriaBancoDeDados,
        Guid Redes,
        Guid BimestreDeHoje,
        Guid Renata,
        Guid Ana,
        Guid Carla,
        Guid Diego,
        Guid CadastroDoBruno);
}
