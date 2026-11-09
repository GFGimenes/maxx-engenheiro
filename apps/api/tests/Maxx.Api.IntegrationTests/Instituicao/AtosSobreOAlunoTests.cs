using System.Net;
using System.Net.Http.Json;
using Maxx.Api.Instituicao;
using Maxx.Api.IntegrationTests.Fotos;
using Maxx.Api.IntegrationTests.Social;
using Maxx.Application.Ranking;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Domain.Ranking;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Ranking;
using static Maxx.Api.IntegrationTests.Social.MundoSocial;

namespace Maxx.Api.IntegrationTests.Instituicao;

// A área da instituição sobre o aluno (passo 12, etapa 1): busca e ficha, correção de
// e-mail, ex-aluno e troca de curso, cada ato com quem fez e quando. Ex-aluno e troca de
// curso pedem o recálculo do ranking na mesma transação do ato. Postgres e bucket reais.
public sealed class AtosSobreOAlunoTests(PostgresFixture postgres, ArmazenamentoFixture armazenamento)
    : IClassFixture<PostgresFixture>, IClassFixture<ArmazenamentoFixture>
{
    private const string _elisa = "elisa.prado@al.unieduk.com.br";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Busca_acha_pelo_nome_sem_acento_pelo_arroba_pelo_email_e_pelo_ra_inclusive_quem_nao_tem_conta()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        await ElisaSemContaAsync(mundo);
        using var coordenacao = await mundo.ComoAsync(Coordenacao);

        Assert.Equal(["Ana Souza"], (await BuscarAsync(coordenacao, "ANA so")).Select(aluno => aluno.NomeCompleto));
        Assert.Equal(["Bruno Lima"], (await BuscarAsync(coordenacao, "@brunoli")).Select(aluno => aluno.NomeCompleto));
        Assert.Equal(["Diego Reis"], (await BuscarAsync(coordenacao, "20260104")).Select(aluno => aluno.NomeCompleto));

        var elisa = Assert.Single(await BuscarAsync(coordenacao, "elisa.prado@"));
        Assert.Equal(("Élisa Prado", "20260105", "CC", 2), (elisa.NomeCompleto, elisa.Ra, elisa.Curso, elisa.Semestre));
        Assert.Null(elisa.Conta);
        Assert.Single(await BuscarAsync(coordenacao, "elisa"));

        // Professor e instituição não são alunos da base; a ex-aluna é, com a marca.
        Assert.Empty(await BuscarAsync(coordenacao, "Renata"));
        var carla = Assert.Single(await BuscarAsync(coordenacao, "carla"));
        Assert.NotNull(carla.Conta!.ExAlunoDesde);
        Assert.Equal("@carlaDias-EX", carla.Conta.Arroba);
    }

    [Fact]
    public async Task Busca_sem_termo_e_recusada_e_so_a_instituicao_entra()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var coordenacao = await mundo.ComoAsync(Coordenacao);
        using var renata = await mundo.ComoAsync(Renata);
        using var ana = await mundo.ComoAsync(Ana);

        await AssertRecusaAsync(await coordenacao.GetAsync(Rota("?busca=a"), Ct), HttpStatusCode.BadRequest, "busca-sem-criterio");
        Assert.Equal(HttpStatusCode.Forbidden, (await renata.GetAsync(Rota("?busca=ana"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ana.GetAsync(Rota("?busca=ana"), Ct)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await renata.PostAsync(Rota($"/{await CadastroAsync(mundo, Ana)}/ex-aluno"), null, Ct)).StatusCode);
    }

    [Fact]
    public async Task Ficha_traz_a_base_a_conta_e_so_as_medalhas_ativas()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        var anulada = await mundo.MedalhaAsync(Ana);
        var ativa = await mundo.MedalhaAsync(Ana, "M03");
        await mundo.AnularAsync(anulada);
        using var coordenacao = await mundo.ComoAsync(Coordenacao);

        var ficha = await FichaAsync(coordenacao, await CadastroAsync(mundo, Ana));

        Assert.Equal(("Ana Souza", "20260101", Ana, "CC", 3), (ficha.Aluno.NomeCompleto, ficha.Aluno.Ra, ficha.Aluno.Email, ficha.Aluno.Curso, ficha.Aluno.Semestre));
        Assert.Equal(("@anaSouza-CC", mundo.Id(Ana)), (ficha.Aluno.Conta!.Arroba, ficha.Aluno.Conta.Id));
        var medalha = Assert.Single(ficha.Medalhas);
        Assert.Equal((ativa, "M03", "Banco de Dados", "Renata Albuquerque"), (medalha.Id, medalha.Item, medalha.Materia, medalha.ConcedidaPor));

        await AssertRecusaAsync(
            await coordenacao.GetAsync(Rota($"/{await CadastroAsync(mundo, Renata)}"), Ct), HttpStatusCode.NotFound, "aluno-nao-encontrado");
    }

    [Fact]
    public async Task Correcao_de_email_vale_para_o_login_registra_o_ato_e_invalida_so_a_recuperacao_aberta()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var ana = await mundo.ComoAsync(Ana);
        var aberta = RecuperacaoDeSenha.Solicitar(mundo.Id(Ana), mundo.Relogio.GetUtcNow());
        await BancoDeTeste.SalvarAsync(mundo.Factory, aberta);
        using var coordenacao = await mundo.ComoAsync(Coordenacao);
        var cadastro = await CadastroAsync(mundo, Ana);

        var resposta = await coordenacao.PutAsJsonAsync(Rota($"/{cadastro}/email"), new { email = " ana.b.souza@al.unieduk.com.br " }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        var correcao = Assert.Single(await mundo.LinhasAsync<CorrecaoDeEmail>());
        Assert.Equal(
            (cadastro, Ana, "ana.b.souza@al.unieduk.com.br", mundo.Id(Coordenacao)),
            (correcao.CadastroElegivelId, correcao.EmailAnterior, correcao.EmailNovo, correcao.CorrigidoPorId));
        Assert.NotNull(Assert.Single(await mundo.LinhasAsync<RecuperacaoDeSenha>()).InvalidadaEm);

        // A sessão aberta continua; o login passa a ser pelo e-mail novo.
        Assert.Equal(HttpStatusCode.OK, (await ana.GetAsync(new Uri("/api/v1/eu", UriKind.Relative), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(mundo, "ana.b.souza@al.unieduk.com.br")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(mundo, Ana)).StatusCode);

        // O mesmo e-mail de novo não muda nada nem registra.
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await coordenacao.PutAsJsonAsync(Rota($"/{cadastro}/email"), new { email = "ana.b.souza@al.unieduk.com.br" }, Ct)).StatusCode);
        Assert.Single(await mundo.LinhasAsync<CorrecaoDeEmail>());
    }

    [Fact]
    public async Task Correcao_de_email_vale_para_quem_esta_so_na_base_e_recusa_forma_errada_e_email_de_outra_linha()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        var elisa = await ElisaSemContaAsync(mundo);
        using var coordenacao = await mundo.ComoAsync(Coordenacao);

        await AssertRecusaAsync(
            await coordenacao.PutAsJsonAsync(Rota($"/{elisa}/email"), new { email = "elisa sem arroba" }, Ct), HttpStatusCode.BadRequest, "email-invalido");
        await AssertRecusaAsync(
            await coordenacao.PutAsJsonAsync(Rota($"/{elisa}/email"), new { email = Renata.ToUpperInvariant() }, Ct), HttpStatusCode.Conflict, "email-em-uso");
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await coordenacao.PutAsJsonAsync(Rota($"/{elisa}/email"), new { email = "elisa.p@al.unieduk.com.br" }, Ct)).StatusCode);

        Assert.Equal("elisa.p@al.unieduk.com.br", Assert.Single(await mundo.LinhasAsync<CadastroElegivel>(), linha => linha.Id == elisa).Email);
        await AssertRecusaAsync(
            await coordenacao.PutAsJsonAsync(Rota($"/{Guid.CreateVersion7()}/email"), new { email = "x@al.unieduk.com.br" }, Ct),
            HttpStatusCode.NotFound,
            "aluno-nao-encontrado");
    }

    // A lista do appsettings.json (produto.md 2.4). Prova reversa: sem a conferência no
    // domínio, a correção para o gmail grava e o teste falha.
    [Theory]
    [InlineData("elisa@gmail.com")]
    [InlineData("elisa@x.al.unieduk.com.br")]
    public async Task Correcao_para_dominio_fora_da_lista_e_recusada_e_nada_muda(string email)
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        var elisa = await ElisaSemContaAsync(mundo);
        var antes = Assert.Single(await mundo.LinhasAsync<CadastroElegivel>(), linha => linha.Id == elisa).Email;
        using var coordenacao = await mundo.ComoAsync(Coordenacao);

        await AssertRecusaAsync(
            await coordenacao.PutAsJsonAsync(Rota($"/{elisa}/email"), new { email }, Ct), HttpStatusCode.BadRequest, "dominio-nao-aceito");

        Assert.Equal(antes, Assert.Single(await mundo.LinhasAsync<CadastroElegivel>(), linha => linha.Id == elisa).Email);
        Assert.Empty(await mundo.LinhasAsync<CorrecaoDeEmail>());
    }

    [Fact]
    public async Task Marcar_ex_aluno_troca_o_sufixo_registra_quem_e_pede_o_recalculo_uma_vez_so()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        using var coordenacao = await mundo.ComoAsync(Coordenacao);
        var cadastro = await CadastroAsync(mundo, Ana);

        Assert.Equal(HttpStatusCode.NoContent, (await coordenacao.PostAsync(Rota($"/{cadastro}/ex-aluno"), null, Ct)).StatusCode);

        var conta = Assert.Single(await mundo.LinhasAsync<Usuario>(), usuario => usuario.Id == mundo.Id(Ana));
        Assert.Equal(("EX", mundo.Id(Coordenacao), mundo.Relogio.GetUtcNow()), (conta.Sufixo, conta.ExAlunoMarcadoPorId, conta.ExAlunoDesde));
        Assert.Equal(1, await PedidosAsync(mundo));

        await AssertRecusaAsync(await coordenacao.PostAsync(Rota($"/{cadastro}/ex-aluno"), null, Ct), HttpStatusCode.Conflict, "ex-aluno");
        await AssertRecusaAsync(
            await coordenacao.PostAsync(Rota($"/{await ElisaSemContaAsync(mundo)}/ex-aluno"), null, Ct), HttpStatusCode.Conflict, "aluno-sem-conta");
        Assert.Equal(1, await PedidosAsync(mundo));
    }

    [Fact]
    public async Task Troca_de_curso_muda_curso_semestre_e_sufixo_registra_o_ato_e_pede_o_recalculo()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        var adm = new Curso("Administração", "ADM", 8);
        await BancoDeTeste.SalvarAsync(mundo.Factory, adm);
        using var coordenacao = await mundo.ComoAsync(Coordenacao);
        var cadastro = await CadastroAsync(mundo, Ana);

        var resposta = await coordenacao.PutAsJsonAsync(Rota($"/{cadastro}/curso"), new { cursoId = adm.Id, semestre = 2 }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        var linha = Assert.Single(await mundo.LinhasAsync<CadastroElegivel>(), linha => linha.Id == cadastro);
        Assert.Equal((adm.Id, 2), (linha.CursoId, linha.Semestre));
        Assert.Equal("ADM", Assert.Single(await mundo.LinhasAsync<Usuario>(), usuario => usuario.Id == mundo.Id(Ana)).Sufixo);
        var troca = Assert.Single(await mundo.LinhasAsync<TrocaDeCurso>());
        Assert.Equal((adm.Id, 3, 2, mundo.Id(Coordenacao)), (troca.CursoNovoId, troca.SemestreAnterior, troca.SemestreNovo, troca.TrocadoPorId));
        Assert.Equal(1, await PedidosAsync(mundo));

        // As matrículas antigas ficam.
        Assert.Contains(await mundo.LinhasAsync<Matricula>(), matricula => matricula.AlunoId == cadastro);
    }

    [Fact]
    public async Task Troca_de_curso_recusa_o_mesmo_curso_semestre_fora_curso_inexistente_e_ex_aluno_sem_pedir_recalculo()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        var adm = new Curso("Administração", "ADM", 8);
        await BancoDeTeste.SalvarAsync(mundo.Factory, adm);
        var cc = Assert.Single(await mundo.LinhasAsync<Curso>(), curso => curso.Sigla == "CC");
        using var coordenacao = await mundo.ComoAsync(Coordenacao);
        var ana = await CadastroAsync(mundo, Ana);

        await AssertRecusaAsync(await TrocarAsync(coordenacao, ana, cc.Id, 4), HttpStatusCode.Conflict, "mesmo-curso");
        await AssertRecusaAsync(await TrocarAsync(coordenacao, ana, adm.Id, 9), HttpStatusCode.BadRequest, "semestre-invalido");
        await AssertRecusaAsync(await TrocarAsync(coordenacao, ana, Guid.CreateVersion7(), 1), HttpStatusCode.NotFound, "curso-nao-encontrado");
        await AssertRecusaAsync(await TrocarAsync(coordenacao, await CadastroAsync(mundo, Carla), adm.Id, 1), HttpStatusCode.Conflict, "ex-aluno");

        Assert.Equal(0, await PedidosAsync(mundo));
        Assert.Empty(await mundo.LinhasAsync<TrocaDeCurso>());
    }

    [Fact]
    public async Task Ex_aluno_marcado_pela_area_sai_do_ranking_do_semestre_e_fica_no_historico()
    {
        await using var mundo = await CriarAsync(postgres, armazenamento);
        await mundo.MedalhaAsync(Ana);
        await mundo.EsvaziarFilaAsync();
        using var coordenacao = await mundo.ComoAsync(Coordenacao);

        Assert.Equal(HttpStatusCode.NoContent, (await coordenacao.PostAsync(Rota($"/{await CadastroAsync(mundo, Ana)}/ex-aluno"), null, Ct)).StatusCode);

        // O pedido do ato cai na janela de coalescência de 10 s da concessão: fica adiado.
        mundo.Relogio.Avancar(TimeSpan.FromSeconds(11));
        await mundo.EsvaziarFilaAsync();

        var linhas = (await mundo.LinhasAsync<PosicaoNoRankingRow>()).Where(linha => linha.AlunoId == mundo.Id(Ana)).ToList();
        Assert.All(linhas, linha => Assert.Equal(PeriodoDoRanking.Historico, linha.Periodo));
        Assert.Contains(linhas, linha => linha.Escopo == EscopoDoRanking.Geral);
    }

    private static Uri Rota(string resto) => new($"/api/v1/instituicao/alunos{resto}", UriKind.Relative);

    private static async Task<List<AlunoDaBaseResponse>> BuscarAsync(HttpClient client, string termo)
    {
        var resposta = await client.GetAsync(Rota($"?busca={Uri.EscapeDataString(termo)}"), Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<List<AlunoDaBaseResponse>>(Ct))!;
    }

    private static async Task<FichaDoAlunoResponse> FichaAsync(HttpClient client, Guid cadastro)
    {
        var resposta = await client.GetAsync(Rota($"/{cadastro}"), Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return (await resposta.Content.ReadFromJsonAsync<FichaDoAlunoResponse>(Ct))!;
    }

    private static Task<HttpResponseMessage> TrocarAsync(HttpClient client, Guid cadastro, Guid curso, int semestre) =>
        client.PutAsJsonAsync(Rota($"/{cadastro}/curso"), new { cursoId = curso, semestre }, Ct);

    private static Task<HttpResponseMessage> LoginAsync(MundoSocial mundo, string email) =>
        mundo.Factory.CreateClient().PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative), new { identificador = email, senha = Senha }, Ct);

    private static async Task<Guid> CadastroAsync(MundoSocial mundo, string email) =>
        Assert.Single(await mundo.LinhasAsync<CadastroElegivel>(), linha => string.Equals(linha.Email, email, StringComparison.OrdinalIgnoreCase)).Id;

    // Aluna de CC na base, sem conta, com acento no nome para a busca sem acento.
    private static async Task<Guid> ElisaSemContaAsync(MundoSocial mundo)
    {
        if ((await mundo.LinhasAsync<CadastroElegivel>()).FirstOrDefault(linha => linha.Email == _elisa) is { } existente)
        {
            return existente.Id;
        }

        var cc = Assert.Single(await mundo.LinhasAsync<Curso>(), curso => curso.Sigla == "CC");
        var elisa = CadastroElegivel.Aluno("20260105", _elisa, "Élisa Prado", cc, 2);
        await BancoDeTeste.SalvarAsync(mundo.Factory, elisa);
        return elisa.Id;
    }

    private static async Task<int> PedidosAsync(MundoSocial mundo) =>
        (await mundo.LinhasAsync<Trabalho>()).Count(trabalho => trabalho.Tipo == RecalcularRanking.Tipo);
}
