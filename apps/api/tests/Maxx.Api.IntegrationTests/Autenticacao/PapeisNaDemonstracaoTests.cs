using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Maxx.Api.Autenticacao;
using Maxx.Api.Demonstracao;
using Maxx.Api.IntegrationTests.Fotos;
using Maxx.Api.IntegrationTests.PremiosParticipacao;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Infrastructure.Autenticacao;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.BancoDeTeste;
using static Maxx.Api.IntegrationTests.PremiosParticipacao.MundoDePremios;
using static Maxx.Api.IntegrationTests.Social.MundoSocial;

namespace Maxx.Api.IntegrationTests.Autenticacao;

// O professor e a coordenação de demonstração (passo 14), contra o Postgres e o bucket reais.
// O mundo é o dos prêmios de participação: a Renata leciona Banco de Dados (CC) para Ana,
// Bruno e Diego; a Elisa é de CC sem matrícula na turma. Aqui entra também uma turma vigente
// de Contabilidade (ADM), com o Fábio.
public sealed class PapeisNaDemonstracaoTests(PostgresFixture postgres, ArmazenamentoFixture armazenamento)
    : IClassFixture<PostgresFixture>, IClassFixture<ArmazenamentoFixture>
{
    private const string _lote = "banca-teste";
    private const string _fabio = "fabio.melo@al.unieduk.com.br";

    private static readonly Uri _professores = new("/api/v1/demo/professores", UriKind.Relative);
    private static readonly Uri _coordenacoes = new("/api/v1/demo/coordenacoes", UriKind.Relative);
    private static readonly Uri _alunos = new("/api/v1/demo/alunos", UriKind.Relative);
    private static readonly Uri _entrar = new("/api/v1/demo/entrar", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Professor_de_demonstracao_nasce_vinculado_as_turmas_vigentes_do_curso_e_volta_pela_chave()
    {
        await using var mundo = await MundoAsync();
        using var client = mundo.Social.Factory.CreateClient();

        var registro = await RegistrarAsync(client, _professores, new { nome = " Rita Lima ", curso = "CC", versaoDoTermo = VersaoDoTermo });

        var eu = await EuAsync(client, registro.Sessao.AccessToken);
        Assert.Equal(["professor"], eu.Papeis);
        Assert.Equal(("@ritaLima-PROF", "Rita Lima"), (eu.Arroba, eu.NomeDeExibicao));
        await using (var scope = mundo.Social.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
            var conta = await db.Set<Usuario>().Include(usuario => usuario.AceitesDeTermo).SingleAsync(usuario => usuario.Id == eu.Id, Ct);
            var cadastro = await db.Set<CadastroElegivel>().SingleAsync(linha => linha.Id == conta.CadastroElegivelId, Ct);
            Assert.Equal((_lote, false, VersaoDoTermo), (conta.LoteDaDemonstracao, conta.TemSenha, Assert.Single(conta.AceitesDeTermo).Versao));
            Assert.Equal((Papeis.Professor, true, (string?)null), (cadastro.Papeis, cadastro.Demonstracao, cadastro.Ra));
            Assert.EndsWith($"@{CadastroElegivel.DominioDaDemonstracao}", cadastro.Email, StringComparison.Ordinal);
            Assert.Equal(
                [mundo.Social.Turma],
                await db.Set<VinculoProfessor>().Where(vinculo => vinculo.ProfessorId == cadastro.Id).Select(vinculo => vinculo.TurmaId).ToListAsync(Ct));
        }

        var volta = await client.PostAsJsonAsync(_entrar, new { chaveDoDispositivo = registro.ChaveDoDispositivo }, Ct);
        Assert.Equal(HttpStatusCode.OK, volta.StatusCode);
        Assert.Equal(eu.Id, (await EuAsync(client, (await volta.Content.ReadFromJsonAsync<SessaoResponse>(Ct))!.AccessToken)).Id);
    }

    [Fact]
    public async Task Coordenacao_de_demonstracao_nasce_com_o_papel_de_instituicao_sem_turma()
    {
        await using var mundo = await MundoAsync();
        using var client = mundo.Social.Factory.CreateClient();

        var registro = await RegistrarAsync(client, _coordenacoes, new { nome = "Caio Reis", versaoDoTermo = VersaoDoTermo });

        var eu = await EuAsync(client, registro.Sessao.AccessToken);
        Assert.Equal(["instituicao"], eu.Papeis);
        Assert.Equal("@caioReis-UNIMAX", eu.Arroba);
        await using var scope = mundo.Social.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var conta = await db.Set<Usuario>().SingleAsync(usuario => usuario.Id == eu.Id, Ct);
        Assert.Equal(_lote, conta.LoteDaDemonstracao);
        Assert.False(await db.Set<VinculoProfessor>().AnyAsync(vinculo => vinculo.ProfessorId == conta.CadastroElegivelId, Ct));
    }

    [Fact]
    public async Task Registros_recusam_curso_sem_turma_vigente_nome_invalido_e_termo_errado()
    {
        await using var mundo = await MundoAsync();
        using var client = mundo.Social.Factory.CreateClient();

        await AssertRecusaAsync(
            await client.PostAsJsonAsync(_professores, new { nome = "Rita", curso = "MED", versaoDoTermo = VersaoDoTermo }, Ct),
            HttpStatusCode.NotFound,
            "curso-indisponivel");
        await AssertRecusaAsync(
            await client.PostAsJsonAsync(_professores, new { nome = " ", curso = "CC", versaoDoTermo = VersaoDoTermo }, Ct),
            HttpStatusCode.BadRequest,
            "nome-de-exibicao-invalido");
        await AssertRecusaAsync(
            await client.PostAsJsonAsync(_coordenacoes, new { nome = new string('a', 61), versaoDoTermo = VersaoDoTermo }, Ct),
            HttpStatusCode.BadRequest,
            "nome-de-exibicao-invalido");
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync(_coordenacoes, new { nome = "Caio", versaoDoTermo = VersaoDoTermo + 1 }, Ct)).StatusCode);

        await using var scope = mundo.Social.Factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<CadastroElegivel>().AnyAsync(linha => linha.Demonstracao, Ct));
    }

    // O escopo do professor de demonstração é o de qualquer professor (produto.md 11): aluno
    // do seed e de demonstração das turmas dele, e ninguém de outro curso.
    [Fact]
    public async Task Professor_de_demonstracao_concede_aos_alunos_das_turmas_dele_e_nao_enxerga_outro_curso()
    {
        await using var mundo = await MundoAsync();
        var (contabilidade, fabio) = await ContabilidadeAsync(mundo);
        using var anonimo = mundo.Social.Factory.CreateClient();
        var alunoDeTeste = await RegistrarAsync(anonimo, _alunos, new { nome = "Dani Teste", curso = "CC", versaoDoTermo = VersaoDoTermo });
        var idDoAlunoDeTeste = (await EuAsync(anonimo, alunoDeTeste.Sessao.AccessToken)).Id;
        using var professor = ComToken(mundo, (await RegistrarAsync(anonimo, _professores, new { nome = "Rita Lima", curso = "CC", versaoDoTermo = VersaoDoTermo })).Sessao);

        using var turmas = JsonDocument.Parse(await professor.GetStringAsync(new Uri("/api/v1/professor/turmas", UriKind.Relative), Ct));
        Assert.Equal(
            [mundo.Social.Turma],
            turmas.RootElement.EnumerateArray().Select(turma => turma.GetProperty("id").GetGuid()));
        using var alunos = JsonDocument.Parse(
            await professor.GetStringAsync(new Uri($"/api/v1/professor/turmas/{mundo.Social.Turma}/alunos", UriKind.Relative), Ct));
        var ids = alunos.RootElement.EnumerateArray().Select(aluno => aluno.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(mundo.Social.Id(Ana), ids);
        Assert.Contains(idDoAlunoDeTeste, ids);
        Assert.DoesNotContain(fabio, ids);

        Assert.Equal(HttpStatusCode.Created, (await ConcederAsync(professor, mundo.Social.Turma, mundo.Social.Id(Ana))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await ConcederAsync(professor, mundo.Social.Turma, idDoAlunoDeTeste)).StatusCode);
        await AssertRecusaAsync(await ConcederAsync(professor, contabilidade, fabio), HttpStatusCode.Forbidden, "fora-do-escopo");
        await AssertRecusaAsync(await ConcederAsync(professor, mundo.Social.Turma, fabio), HttpStatusCode.NotFound, "aluno-fora-da-turma");
        await AssertRecusaAsync(
            await professor.GetAsync(new Uri($"/api/v1/professor/turmas/{contabilidade}/alunos", UriKind.Relative), Ct),
            HttpStatusCode.Forbidden,
            "fora-do-escopo");
    }

    [Fact]
    public async Task Professor_de_demonstracao_decide_a_fila_das_turmas_dele_e_publica_aviso_para_elas()
    {
        await using var mundo = await MundoAsync();
        var (contabilidade, _) = await ContabilidadeAsync(mundo);
        using var ana = await mundo.Social.ComoAsync(Ana);
        using var elisa = await mundo.Social.ComoAsync(Elisa);
        await NaSemanaAsync(elisa, mundo.Semana);
        var daAna = await NaSemanaAsync(ana, mundo.Semana);
        using var anonimo = mundo.Social.Factory.CreateClient();
        using var professor = ComToken(mundo, (await RegistrarAsync(anonimo, _professores, new { nome = "Rita Lima", curso = "CC", versaoDoTermo = VersaoDoTermo })).Sessao);

        using var fila = JsonDocument.Parse(await professor.GetStringAsync(new Uri("/api/v1/comprovacoes", UriKind.Relative), Ct));
        var unica = Assert.Single(fila.RootElement.GetProperty("itens").EnumerateArray());
        Assert.Equal(daAna.Premio!.Id, unica.GetProperty("premioId").GetGuid());
        var aprovacao = await professor.PostAsync(
            new Uri($"/api/v1/comprovacoes/{unica.GetProperty("id").GetGuid()}/aprovacao", UriKind.Relative), null, Ct);
        Assert.True(aprovacao.IsSuccessStatusCode, $"{aprovacao.StatusCode}");

        var paraAsDele = await professor.PostAsJsonAsync(
            new Uri("/api/v1/avisos", UriKind.Relative),
            new { escopo = "turma", titulo = "Prova de quinta", texto = "Revisem o capítulo 3.", turmaIds = new[] { mundo.Social.Turma } },
            Ct);
        Assert.Equal(HttpStatusCode.Created, paraAsDele.StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await professor.PostAsJsonAsync(
                new Uri("/api/v1/avisos", UriKind.Relative),
                new { escopo = "turma", titulo = "Prova de quinta", texto = "Revisem.", turmaIds = new[] { contabilidade } },
                Ct)).StatusCode);
    }

    // A exceção única aos poderes da coordenação de demonstração (passo 14, P5).
    [Fact]
    public async Task Coordenacao_de_demonstracao_corrige_e_mail_so_para_dominio_reservado()
    {
        await using var mundo = await MundoAsync();
        using var anonimo = mundo.Social.Factory.CreateClient();
        using var coordenacao = ComToken(mundo, (await RegistrarAsync(anonimo, _coordenacoes, new { nome = "Caio Reis", versaoDoTermo = VersaoDoTermo })).Sessao);
        using var doSeed = await mundo.Social.ComoAsync(Coordenacao);
        Guid cadastroDaElisa;
        await using (var scope = mundo.Social.Factory.Services.CreateAsyncScope())
        {
            cadastroDaElisa = await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<CadastroElegivel>()
                .Where(linha => linha.Email == Elisa).Select(linha => linha.Id).SingleAsync(Ct);
        }

        var ficha = await coordenacao.GetAsync(Rota($"/{cadastroDaElisa}"), Ct);
        Assert.Equal(HttpStatusCode.OK, ficha.StatusCode);
        await AssertRecusaAsync(
            await coordenacao.PutAsJsonAsync(Rota($"/{cadastroDaElisa}/email"), new { email = "elisa.p@al.unieduk.com.br" }, Ct),
            HttpStatusCode.BadRequest,
            "dominio-nao-reservado");
        await AssertRecusaAsync(
            await coordenacao.PutAsJsonAsync(Rota($"/{cadastroDaElisa}/email"), new { email = "elisa.p@unieduk.com.br" }, Ct),
            HttpStatusCode.BadRequest,
            "dominio-nao-reservado");
        Assert.True((await coordenacao.PutAsJsonAsync(Rota($"/{cadastroDaElisa}/email"), new { email = "elisa.p@al.unieduk.invalid" }, Ct)).IsSuccessStatusCode);

        // A instituição do seed segue sem a exceção.
        Assert.True((await doSeed.PutAsJsonAsync(Rota($"/{cadastroDaElisa}/email"), new { email = "elisa.p@al.unieduk.com.br" }, Ct)).IsSuccessStatusCode);
    }

    private async Task<MundoDePremios> MundoAsync()
    {
        var mundo = await CriarComConfiguracaoAsync(
            postgres,
            armazenamento,
            new Dictionary<string, string> { ["Demo:Habilitada"] = "true", ["Demo:Lote"] = _lote });
        await SalvarAsync(mundo.Social.Factory, TermoVigente());
        return mundo;
    }

    // Contabilidade (ADM) no mesmo semestre vigente, com o Fábio matriculado e com conta.
    private static async Task<(Guid Turma, Guid Fabio)> ContabilidadeAsync(MundoDePremios mundo)
    {
        await using var scope = mundo.Social.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var adm = await db.Set<Curso>().SingleAsync(curso => curso.Sigla == "ADM", Ct);
        var semestreId = await db.Set<Turma>().Where(turma => turma.Id == mundo.Social.Turma).Select(turma => turma.SemestreLetivoId).SingleAsync(Ct);
        var semestre = await db.Set<SemestreLetivo>().SingleAsync(semestre => semestre.Id == semestreId, Ct);
        var contabilidade = new Materia(adm, "Contabilidade");
        var turma = new Turma(contabilidade, semestre);
        var cadastro = CadastroElegivel.Aluno("20260106", _fabio, "Fábio Melo", adm, 2);
        var conta = Usuario.CadastrarAluno(cadastro, adm, "Fábio Melo", "fabioMelo", new Argon2idSenhaHasher().Hash(Senha), mundo.Social.Relogio.GetUtcNow());
        db.AddRange(contabilidade, turma, cadastro, conta, new Matricula(cadastro, turma));
        await db.SaveChangesAsync(Ct);
        return (turma.Id, conta.Id);
    }

    private static HttpClient ComToken(MundoDePremios mundo, SessaoResponse sessao)
    {
        var client = mundo.Social.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        return client;
    }

    private static Task<HttpResponseMessage> ConcederAsync(HttpClient client, Guid turma, Guid aluno) =>
        client.PostAsJsonAsync(new Uri($"/api/v1/professor/turmas/{turma}/medalhas", UriKind.Relative), new { alunoId = aluno, item = "M01" }, Ct);

    private static async Task<RegistroDeDemonstracaoResponse> RegistrarAsync(HttpClient client, Uri rota, object pedido)
    {
        var response = await client.PostAsJsonAsync(rota, pedido, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegistroDeDemonstracaoResponse>(Ct))!;
    }

    private static async Task<EuResponse> EuAsync(HttpClient client, string accessToken)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/eu", UriKind.Relative));
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(pedido, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EuResponse>(Ct))!;
    }

    private static Uri Rota(string resto) => new($"/api/v1/instituicao/alunos{resto}", UriKind.Relative);

    private static async Task AssertRecusaAsync(HttpResponseMessage response, HttpStatusCode status, string codigo)
    {
        Assert.Equal(status, response.StatusCode);
        using var corpo = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(codigo, corpo.RootElement.GetProperty("codigo").GetString());
    }
}
