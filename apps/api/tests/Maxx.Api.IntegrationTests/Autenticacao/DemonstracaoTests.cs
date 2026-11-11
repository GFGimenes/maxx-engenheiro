using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Maxx.Api.Autenticacao;
using Maxx.Api.Demonstracao;
using Maxx.Api.IntegrationTests.PremiosParticipacao;
using Maxx.Application;
using Maxx.Application.Medalhas;
using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Fotos;
using Maxx.Domain.Identidade;
using Maxx.Domain.Notificacoes;
using Maxx.Domain.Premios;
using Maxx.Domain.Social;
using Maxx.Infrastructure.Conquistas;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Maxx.Api.IntegrationTests.BancoDeTeste;

namespace Maxx.Api.IntegrationTests.Autenticacao;

// O modo de demonstração (produto.md 11), contra o Postgres real, desligado e ligado.
public sealed class DemonstracaoTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string _lote = "banca-teste";

    private static readonly Uri _demo = new("/api/v1/demo", UriKind.Relative);
    private static readonly Uri _alunos = new("/api/v1/demo/alunos", UriKind.Relative);
    private static readonly Uri _entrar = new("/api/v1/demo/entrar", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Hoje => FusoHorario.DataLocal(DateTimeOffset.UtcNow);

    [Fact]
    public async Task Registro_pede_o_recalculo_da_raridade_na_mesma_gravacao()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        await RegistrarAsync(client, "Ana Paula");

        await using var scope = factory.Services.CreateAsyncScope();
        var trabalho = await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<Trabalho>().SingleAsync(Ct);
        Assert.Equal(RecalcularRaridade.Tipo, trabalho.Tipo);
    }

    [Fact]
    public async Task Desligada_as_tres_rotas_respondem_igual_a_uma_rota_inexistente()
    {
        await using var factory = new MaxxApiFactory(MaxxApiFactory.UnreachableDatabase);
        using var client = factory.CreateClient();

        var inexistenteGet = await DescreverAsync(await client.GetAsync(new Uri("/api/v1/nada-aqui", UriKind.Relative), Ct));
        var inexistentePost = await DescreverAsync(
            await client.PostAsJsonAsync(new Uri("/api/v1/nada-aqui", UriKind.Relative), new { nome = "Ana", curso = "CC", versaoDoTermo = VersaoDoTermo }, Ct));

        Assert.StartsWith("404", inexistenteGet, StringComparison.Ordinal);
        Assert.Equal(inexistenteGet, await DescreverAsync(await client.GetAsync(_demo, Ct)));
        Assert.Equal(inexistentePost, await DescreverAsync(await client.PostAsJsonAsync(_alunos, new { nome = "Ana", curso = "CC", versaoDoTermo = VersaoDoTermo }, Ct)));
        Assert.Equal(inexistentePost, await DescreverAsync(await client.PostAsJsonAsync(_entrar, new { chaveDoDispositivo = "x" }, Ct)));
    }

    [Fact]
    public void A_flag_nao_existe_em_nenhum_appsettings_nem_no_compose_de_producao()
    {
        var arquivos = Directory.GetFiles(Path.Combine(Repositorio.Raiz, "apps", "api", "src"), "appsettings*.json", SearchOption.AllDirectories)
            .Where(arquivo => !arquivo.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !arquivo.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(arquivos);

        foreach (var arquivo in arquivos)
        {
            var chaves = Chaves(JsonNode.Parse(File.ReadAllText(arquivo))!).ToList();
            Assert.DoesNotContain(chaves, chave => chave.StartsWith(DemoOptions.Section, StringComparison.OrdinalIgnoreCase));
        }

        Assert.DoesNotContain("Demo__", File.ReadAllText(Path.Combine(Repositorio.Raiz, "docker-compose.yml")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ligada_sem_lote_a_api_nao_sobe()
    {
        await using var factory = new MaxxApiFactory(
            MaxxApiFactory.UnreachableDatabase, configuracao: new Dictionary<string, string> { ["Demo:Habilitada"] = "true" });

        var falha = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Demo__Lote", falha.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ligada_lista_so_os_cursos_com_turma_no_semestre_vigente()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        var demo = await client.GetFromJsonAsync<DemonstracaoResponse>(_demo, Ct);

        Assert.Equal([new CursoDeDemonstracao("CC", "Ciência da Computação")], demo!.Cursos);
    }

    [Fact]
    public async Task Registro_cria_o_aluno_marcado_nas_turmas_vigentes_do_curso_e_abre_sessao()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(_alunos, new { nome = "Ana Paula", curso = "CC", versaoDoTermo = VersaoDoTermo }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var registro = (await response.Content.ReadFromJsonAsync<RegistroDeDemonstracaoResponse>(Ct))!;
        Assert.Equal(43, registro.ChaveDoDispositivo.Length);
        Assert.False(string.IsNullOrEmpty(registro.Sessao.RefreshToken));
        var eu = await EuAsync(client, registro.Sessao.AccessToken);
        Assert.Equal(["aluno"], eu.Papeis);
        Assert.Equal("@anaPaula-CC", eu.Arroba);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var usuario = await db.Set<Usuario>().SingleAsync(Ct);
        var cadastro = await db.Set<CadastroElegivel>().SingleAsync(linha => linha.Id == usuario.CadastroElegivelId, Ct);
        Assert.True(cadastro.Demonstracao);
        Assert.Matches("^DEMO[A-Z2-9]{8}$", cadastro.Ra);
        Assert.EndsWith("@demonstracao.invalid", cadastro.Email, StringComparison.Ordinal);
        Assert.Equal("Ana Paula", cadastro.NomeCompleto);
        Assert.Equal(_lote, usuario.LoteDaDemonstracao);
        Assert.False(usuario.TemSenha);
        Assert.Equal(Sha256(registro.ChaveDoDispositivo), usuario.HashDaChaveDoDispositivo);

        // As duas turmas de CC do semestre vigente; a do ano passado, não.
        var turmas = await db.Set<Matricula>().Where(matricula => matricula.AlunoId == cadastro.Id)
            .Join(db.Set<Turma>(), matricula => matricula.TurmaId, turma => turma.Id, (_, turma) => turma.Materia.Nome)
            .OrderBy(nome => nome).ToListAsync(Ct);
        Assert.Equal(["Banco de Dados", "Redes"], turmas);
    }

    [Fact]
    public async Task A_chave_do_dispositivo_volta_para_o_mesmo_aluno_e_outra_chave_nao_entra()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();
        var registro = await RegistrarAsync(client, "Ana Paula");
        var id = (await EuAsync(client, registro.Sessao.AccessToken)).Id;

        var response = await client.PostAsJsonAsync(_entrar, new { chaveDoDispositivo = registro.ChaveDoDispositivo }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sessao = (await response.Content.ReadFromJsonAsync<SessaoResponse>(Ct))!;
        Assert.Equal(id, (await EuAsync(client, sessao.AccessToken)).Id);

        var errada = await client.PostAsJsonAsync(_entrar, new { chaveDoDispositivo = "chave-que-nao-existe" }, Ct);
        await AssertRecusaAsync(errada, HttpStatusCode.Unauthorized, "chave-invalida");
    }

    [Fact]
    public async Task Mesmo_nome_registrado_ao_mesmo_tempo_ganha_arrobas_diferentes()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        var respostas = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            client.PostAsJsonAsync(_alunos, new { nome = "Ana Paula", curso = "CC", versaoDoTermo = VersaoDoTermo }, Ct)));

        Assert.All(respostas, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
        await using var scope = factory.Services.CreateAsyncScope();
        var prefixos = await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<Usuario>()
            .Select(usuario => usuario.Prefixo).ToListAsync(Ct);
        Assert.Equal(8, prefixos.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task Esgotadas_as_tentativas_responde_409_sem_repetir_sugestao_nem_gravar()
    {
        var tentados = new List<string>();
        await using var factory = await MundoAsync(servicos => servicos.AddScoped<IUnitOfWork>(
            provedor => new GravacaoSempreRecusada(provedor.GetRequiredService<MaxxDbContext>(), tentados)));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(_alunos, new { nome = "Ana Paula", curso = "CC", versaoDoTermo = VersaoDoTermo }, Ct);

        await AssertRecusaAsync(response, HttpStatusCode.Conflict, "registro-simultaneo");
        Assert.Equal(
            ["anaPaula", "anaPaula2", "anaPaula3", "anaPaula4", "anaPaula5", "anaPaula6", "anaPaula7", "anaPaula8", "anaPaula9", "anaPaula10"],
            tentados);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<Usuario>().CountAsync(Ct));
    }

    [Theory]
    [InlineData("Ana", "XYZ", HttpStatusCode.NotFound, "curso-indisponivel")]
    [InlineData("Ana", "ADM", HttpStatusCode.NotFound, "curso-indisponivel")]
    [InlineData("   ", "CC", HttpStatusCode.BadRequest, "nome-de-exibicao-invalido")]
    public async Task Registro_recusa_curso_sem_turma_vigente_e_nome_em_branco(
        string nome, string curso, HttpStatusCode status, string codigo)
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        await AssertRecusaAsync(await client.PostAsJsonAsync(_alunos, new { nome, curso, versaoDoTermo = VersaoDoTermo }, Ct), status, codigo);
    }

    [Fact]
    public async Task Na_web_o_refresh_vai_em_cookie_e_a_chave_continua_no_corpo()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();
        using var pedido = new HttpRequestMessage(HttpMethod.Post, _alunos) { Content = JsonContent.Create(new { nome = "Ana", curso = "CC", versaoDoTermo = VersaoDoTermo }) };
        pedido.Headers.Add(SessaoHttp.CabecalhoDoCliente, SessaoHttp.ClienteWeb);

        var response = await client.SendAsync(pedido, Ct);

        var registro = (await response.Content.ReadFromJsonAsync<RegistroDeDemonstracaoResponse>(Ct))!;
        Assert.Null(registro.Sessao.RefreshToken);
        Assert.False(string.IsNullOrEmpty(registro.ChaveDoDispositivo));
        Assert.StartsWith("maxx_refresh=", Assert.Single(response.Headers.GetValues("Set-Cookie")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Aluno_de_demonstracao_nao_entra_por_senha_nem_recebe_recuperacao()
    {
        var caixa = new CaixaDeEmail();
        await using var factory = await MundoAsync(servicos => servicos.AddSingleton<IEmailSender>(caixa));
        using var client = factory.CreateClient();
        await RegistrarAsync(client, "Ana Paula");
        await using var scope = factory.Services.CreateAsyncScope();
        var cadastro = await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<CadastroElegivel>()
            .SingleAsync(linha => linha.Demonstracao, Ct);

        var login = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative), new { identificador = cadastro.Ra, senha = "qualquer-senha" }, Ct);
        var recuperacao = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/recuperacao", UriKind.Relative), new { email = cadastro.Email }, Ct);

        await AssertRecusaAsync(login, HttpStatusCode.Unauthorized, "credenciais-invalidas");
        Assert.Equal(HttpStatusCode.Accepted, recuperacao.StatusCode);
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<RecuperacaoDeSenha>().CountAsync(Ct));
        Assert.Equal(0, caixa.Recebidos);
    }

    [Fact]
    public async Task O_banco_recusa_aluno_de_demonstracao_com_senha_ou_sem_chave()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();
        await RegistrarAsync(client, "Ana Paula");

        AssertCheck(
            await FalhaNoSqlAsync(factory, "UPDATE usuario SET hash_da_senha = 'x' WHERE lote_da_demonstracao IS NOT NULL"),
            "ck_usuario_demonstracao");
        AssertCheck(
            await FalhaNoSqlAsync(factory, "UPDATE usuario SET hash_da_chave_do_dispositivo = NULL WHERE lote_da_demonstracao IS NOT NULL"),
            "ck_usuario_demonstracao");
    }

    // Passo 14: professor e coordenação de demonstração, e a conta encerrada pela purga.
    [Fact]
    public async Task O_banco_aceita_professor_e_coordenacao_de_demonstracao_e_so_eles_encerrados()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();
        await RegistrarAsync(client, "Ana Paula");
        var agora = DateTimeOffset.UtcNow;
        var cadastroDoProfessor = CadastroElegivel.ProfessorDeDemonstracao("DEMOPROF0001", "Rita Lima");
        var cadastroDaCoordenacao = CadastroElegivel.CoordenacaoDeDemonstracao("DEMOCOOR0001", "Caio Reis");
        var professor = Usuario.EntrarNaDemonstracao(cadastroDoProfessor, null, "ritaLima", "hash-p", _lote, agora);
        var coordenacao = Usuario.EntrarNaDemonstracao(cadastroDaCoordenacao, null, "caioReis", "hash-c", _lote, agora);
        await SalvarAsync(factory, cadastroDoProfessor, cadastroDaCoordenacao, professor, coordenacao);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
            (await db.Set<Usuario>().SingleAsync(usuario => usuario.Id == professor.Id, Ct)).Encerrar(agora);
            await db.SaveChangesAsync(Ct);
        }

        const string Aluno = "papeis = 1 AND lote_da_demonstracao IS NOT NULL";
        AssertCheck(
            await FalhaNoSqlAsync(factory, $"UPDATE usuario SET hash_da_chave_do_dispositivo = NULL, encerrada_em = now() WHERE {Aluno}"),
            "ck_usuario_demonstracao");
        AssertCheck(
            await FalhaNoSqlAsync(factory, "UPDATE usuario SET hash_da_senha = 'x' WHERE papeis = 4 AND lote_da_demonstracao IS NOT NULL"),
            "ck_usuario_demonstracao");
        AssertCheck(
            await FalhaNoSqlAsync(factory, "UPDATE usuario SET papeis = 6 WHERE papeis = 4 AND lote_da_demonstracao IS NOT NULL"),
            "ck_usuario_demonstracao");
        AssertCheck(
            await FalhaNoSqlAsync(factory, "UPDATE usuario SET hash_da_chave_do_dispositivo = 'y' WHERE encerrada_em IS NOT NULL"),
            "ck_usuario_demonstracao");
        AssertCheck(
            await FalhaNoSqlAsync(factory, "UPDATE usuario SET lote_da_demonstracao = NULL WHERE encerrada_em IS NOT NULL"),
            "ck_usuario_demonstracao");
        AssertCheck(
            await FalhaNoSqlAsync(
                factory,
                $"INSERT INTO purga_da_demonstracao (id, lote, alunos, professores, coordenacoes, fotos, purgado_por_id, purgado_em) " +
                $"VALUES (gen_random_uuid(), 'x', 0, 0, 0, 0, '{coordenacao.Id}', now())"),
            "ck_purga_da_demonstracao_contagens");
    }

    [Fact]
    public async Task Registro_grava_o_aceite_da_versao_vigente_e_a_volta_nao_pede_de_novo()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();

        var registro = await RegistrarAsync(client, "Ana Paula");
        await SalvarAsync(factory, new TermoDeUso(VersaoDoTermo + 1, "Texto novo.", DateTimeOffset.UtcNow));
        var volta = await client.PostAsJsonAsync(_entrar, new { chaveDoDispositivo = registro.ChaveDoDispositivo }, Ct);

        Assert.Equal(HttpStatusCode.OK, volta.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var usuario = await db.Set<Usuario>().Include(usuario => usuario.AceitesDeTermo).SingleAsync(Ct);
        Assert.Equal(VersaoDoTermo, Assert.Single(usuario.AceitesDeTermo).Versao);
    }

    [Fact]
    public async Task Registro_sem_a_versao_ou_com_versao_antiga_nao_cria_aluno()
    {
        await using var factory = await MundoAsync();
        await SalvarAsync(factory, new TermoDeUso(VersaoDoTermo + 1, "Texto novo.", DateTimeOffset.UtcNow));
        using var client = factory.CreateClient();

        await AssertRecusaAsync(
            await client.PostAsJsonAsync(_alunos, new { nome = "Ana", curso = "CC" }, Ct),
            HttpStatusCode.BadRequest,
            "dados-invalidos");
        await AssertRecusaAsync(
            await client.PostAsJsonAsync(_alunos, new { nome = "Ana", curso = "CC", versaoDoTermo = VersaoDoTermo }, Ct),
            HttpStatusCode.Conflict,
            "termo-desatualizado");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        Assert.Equal(0, await db.Set<Usuario>().CountAsync(Ct));
        Assert.Equal(0, await db.Set<CadastroElegivel>().CountAsync(Ct));
    }

    // O SQL é o do README das decisões, o que será rodado depois da apresentação.
    [Fact]
    public async Task A_purga_do_readme_apaga_o_lote_com_aceites_medalhas_uploads_posts_e_interacoes_e_mantem_o_termo_e_o_que_nao_e_do_lote()
    {
        await using var factory = await MundoAsync();
        using var client = factory.CreateClient();
        await RegistrarAsync(client, "Ana Paula");
        await RegistrarAsync(client, "Bruno Lima");
        await RegistrarAsync(client, "Carla Dias");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dados = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
            await using var mudarLote = dados.CreateCommand(
                "UPDATE usuario SET lote_da_demonstracao = 'outra-banca' WHERE nome_de_exibicao = 'Carla Dias'");
            await mudarLote.ExecuteNonQueryAsync(Ct);
        }

        // Uma medalha para um aluno do lote e outra para quem é de outro lote.
        var renata = CadastroElegivel.Professor("renata@prof.unieduk.com.br", "Renata Albuquerque");
        var contaDaRenata = Usuario.CriarPeloSeed(renata, "renataA", DateTimeOffset.UtcNow);
        Guid turma;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var banco = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
            var cadastroDaAna = (await banco.Set<Usuario>().SingleAsync(usuario => usuario.NomeDeExibicao == "Ana Paula", Ct)).CadastroElegivelId;
            turma = await banco.Set<Matricula>()
                .Where(matricula => matricula.AlunoId == cadastroDaAna)
                .Join(banco.Set<Turma>(), matricula => matricula.TurmaId, linha => linha.Id, (_, linha) => linha)
                .Where(linha => linha.Materia.Nome == "Banco de Dados")
                .Select(linha => linha.Id)
                .SingleAsync(Ct);
            await SalvarAsync(
                factory,
                renata,
                contaDaRenata,
                new VinculoProfessor(renata, await banco.Set<Turma>().Include(linha => linha.Materia).SingleAsync(linha => linha.Id == turma, Ct)),
                Medalhas.CatalogoDeMedalhas.M03());
        }

        foreach (var nome in new[] { "Ana Paula", "Carla Dias" })
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var aluno = await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<Usuario>()
                .SingleAsync(usuario => usuario.NomeDeExibicao == nome, Ct);
            await scope.ServiceProvider.GetRequiredService<ConcederMedalhaHandler>().ConcederAsync(contaDaRenata.Id, turma, aluno.Id, "M03", Ct);
        }

        // Um upload de foto pendente para cada um dos dois: a linha segura o usuário, sem
        // cascata. Os objetos do bucket não entram aqui: a purga é só do banco.
        foreach (var nome in new[] { "Ana Paula", "Carla Dias" })
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var aluno = await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<Usuario>()
                .SingleAsync(usuario => usuario.NomeDeExibicao == nome, Ct);
            await SalvarAsync(
                factory,
                UploadPendente.Pedir(aluno, FinalidadeDoUpload.FotoDePerfil, "image/jpeg", 1000, 1000, DateTimeOffset.UtcNow).Upload!);
        }

        // Um post com a medalha de cada um dos dois: o post segura o autor e a medalha.
        foreach (var nome in new[] { "Ana Paula", "Carla Dias" })
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var banco = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
            var aluno = await banco.Set<Usuario>().SingleAsync(usuario => usuario.NomeDeExibicao == nome, Ct);
            var medalha = await banco.Set<Medalha>().SingleAsync(linha => linha.AlunoId == aluno.Id, Ct);
            await SalvarAsync(factory, Post.Publicar(aluno, "Nota 10!", null, medalha, DateTimeOffset.UtcNow).Post!);
        }

        // Uma reivindicação de prêmio de participação de cada um, com evento digitado: o
        // evento segura quem digitou, e o prêmio, a comprovação e o post seguram o aluno.
        await SalvarAsync(factory, CatalogoRowMapper.ParaRow(CatalogoDeParticipacao.P01));
        foreach (var nome in new[] { "Ana Paula", "Carla Dias" })
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var banco = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
            var aluno = await banco.Set<Usuario>().SingleAsync(usuario => usuario.NomeDeExibicao == nome, Ct);
            var agora = DateTimeOffset.UtcNow;
            var evento = Evento.TextoLivre("Palestra de abertura", Hoje.AddDays(-1), Hoje.AddDays(-1), aluno);
            var premio = PremioParticipacao.Reivindicar(CatalogoDeParticipacao.P01, 1, aluno, null, evento, null, null, agora).Premio!;
            var upload = UploadPendente.Pedir(aluno, FinalidadeDoUpload.Post, "image/jpeg", 1000, 500, agora).Upload!;
            var post = Post.Reivindicar(aluno, null, upload, premio, agora).Post!;
            var comprovacao = Comprovacao.De(premio, post.Id);

            // Os dois chegam à averiguação: a contestação segura o prêmio, sem cascata.
            premio.Aprovar(aluno, contaDaRenata, agora);
            var contestacao = premio.Contestar(contaDaRenata, "Revi a foto.", agora).Contestacao!;
            await SalvarAsync(factory, evento, premio, upload, post, comprovacao, contestacao);
        }

        // Entre os dois: a Ana curte e comenta o post da Carla, a Carla curte o da Ana e
        // comenta o próprio, e cada uma segue a outra. Do lado da Carla fica só o comentário
        // dela no post dela; o resto toca a Ana e sai com ela.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var banco = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
            var ana = await banco.Set<Usuario>().SingleAsync(usuario => usuario.NomeDeExibicao == "Ana Paula", Ct);
            var carla = await banco.Set<Usuario>().SingleAsync(usuario => usuario.NomeDeExibicao == "Carla Dias", Ct);
            var doPost = await banco.Set<Post>().Where(post => post.MedalhaId != null).ToDictionaryAsync(post => post.AutorId, Ct);
            var agora = DateTimeOffset.UtcNow;
            await SalvarAsync(
                factory,
                Curtida.Curtir(ana, doPost[carla.Id], agora)!,
                Curtida.Curtir(carla, doPost[ana.Id], agora)!,
                Comentario.Escrever(ana, doPost[carla.Id], "Parabéns!", agora).Comentario!,
                Comentario.Escrever(carla, doPost[carla.Id], "Valeu!", agora).Comentario!,
                Seguimento.Seguir(ana, carla, professorDoSeguido: false, agora)!,
                Seguimento.Seguir(carla, ana, professorDoSeguido: false, agora)!);
        }

        // O worker grava as notificações de medalha recebida, que a purga também leva.
        var consumidor = factory.Services.GetRequiredService<ConsumidorDeTrabalhos>();
        while (await consumidor.ProcessarProximoAsync(Ct))
        {
        }

        // Uma conquista de cada um, com a medalha como evidência e o progresso gravado: a
        // conquista segura o aluno, e a evidência, a medalha.
        var c90 = new ItemConquista
        {
            Codigo = "C90",
            Nome = "Uma presença no curso",
            Descricao = "Receber uma medalha de presença integral.",
            Tier = Tier.Bronze,
            Pontos = 5,
            Periodo = Periodo.Curso,
            Gatilho = Gatilho.PorFato,
            Regra = new() { Forma = FormaDeContagem.Contagem, Fonte = FonteDeContagem.Medalha, FiltroItens = ["M03"], Alvo = AlvoDeConquista.De(1) },
        };
        await SalvarAsync(factory, CatalogoRowMapper.ParaRow(c90));
        foreach (var nome in new[] { "Ana Paula", "Carla Dias" })
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var banco = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
            var aluno = await banco.Set<Usuario>().SingleAsync(usuario => usuario.NomeDeExibicao == nome, Ct);
            var medalha = await banco.Set<Medalha>().SingleAsync(linha => linha.AlunoId == aluno.Id, Ct);
            var fato = new FatoContavel
            {
                Origem = OrigemDoFato.Medalha,
                Id = medalha.Id,
                ItemCodigo = medalha.ItemCodigo,
                MateriaId = medalha.MateriaId,
                BimestreId = medalha.BimestreId,
                SemestreLetivoId = medalha.SemestreLetivoId,
                AnoLetivoId = Guid.CreateVersion7(),
            };
            var instancia = new InstanciaDePeriodo(Periodo.Curso, aluno.Id);
            var conquista = Conquista.Conceder(c90, 1, aluno.Id, instancia, Avaliacao.De(1, 1, [fato]), DateTimeOffset.UtcNow);

            // A Ana posta a conquista: o post segura a conquista, e a purga o apaga antes dela.
            object[] doPost = nome == "Ana Paula"
                ? [Post.Publicar(aluno, null, null, null, conquista, DateTimeOffset.UtcNow).Post!]
                : [];
            await SalvarAsync(
                factory,
                [
                conquista,
                .. doPost,
                new ProgressoDeConquistaRow
                {
                    AlunoId = aluno.Id,
                    ItemCodigo = c90.Codigo,
                    TipoDePeriodo = "curso",
                    ReferenciaDoPeriodo = aluno.Id,
                    Contagem = 1,
                    Alvo = 1,
                    AtualizadoEm = DateTimeOffset.UtcNow,
                },
                ]);
        }

        // Um aviso da Renata para a turma, lido pelas duas (passo 10): a leitura segura o
        // usuário, sem cascata. O aviso não é do lote e fica.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var banco = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
            var agora = DateTimeOffset.UtcNow;
            var aviso = Maxx.Domain.Mural.Aviso.Publicar(
                contaDaRenata, Maxx.Domain.Mural.EscopoDoAviso.Turma, [turma], "Prova remarcada", "Passou para sexta.", null, agora).Aviso!;
            var leitoras = await banco.Set<Usuario>()
                .Where(usuario => usuario.NomeDeExibicao == "Ana Paula" || usuario.NomeDeExibicao == "Carla Dias")
                .Select(usuario => usuario.Id)
                .ToListAsync(Ct);
            await SalvarAsync(factory, [aviso, .. leitoras.Select(leitora => new Maxx.Domain.Mural.LeituraDeAviso(leitora, aviso.Id, agora))]);
        }

        var readme = await File.ReadAllTextAsync(Path.Combine(Repositorio.Raiz, "docs", "decisoes", "README.md"), Ct);
        var purga = Regex.Match(readme, @"```sql\s+(BEGIN;.*?COMMIT;)\s+```", RegexOptions.Singleline, TimeSpan.FromSeconds(5));
        Assert.True(purga.Success);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dados = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
            await using var comando = dados.CreateCommand(purga.Groups[1].Value.Replace(":'lote'", $"'{_lote}'", StringComparison.Ordinal));
            await comando.ExecuteNonQueryAsync(Ct);
        }

        await using var depois = factory.Services.CreateAsyncScope();
        var db = depois.ServiceProvider.GetRequiredService<MaxxDbContext>();
        var restante = await db.Set<Usuario>().SingleAsync(usuario => usuario.Papeis == Papeis.Aluno, Ct);
        Assert.Equal("Carla Dias", restante.NomeDeExibicao);
        Assert.Equal(restante.Id, (await db.Set<AceiteDeTermo>().SingleAsync(Ct)).UsuarioId);
        Assert.Equal(restante.Id, (await db.Set<Medalha>().SingleAsync(Ct)).AlunoId);
        var conquistaQueFica = await db.Set<Conquista>().Include(conquista => conquista.Evidencias).SingleAsync(Ct);
        Assert.Equal(restante.Id, conquistaQueFica.AlunoId);
        Assert.Equal(1, await db.Set<EvidenciaDaConquista>().CountAsync(Ct));
        Assert.Equal(restante.Id, (await db.Set<ProgressoDeConquistaRow>().SingleAsync(Ct)).AlunoId);
        Assert.Equal(restante.Id, (await db.Set<Notificacao>().SingleAsync(Ct)).UsuarioId);
        Assert.Equal(restante.Id, (await db.Set<Maxx.Domain.Mural.LeituraDeAviso>().SingleAsync(Ct)).UsuarioId);
        Assert.Equal(1, await db.Set<Maxx.Domain.Mural.Aviso>().CountAsync(Ct));
        Assert.All(await db.Set<UploadPendente>().ToListAsync(Ct), upload => Assert.Equal(restante.Id, upload.UsuarioId));
        Assert.Equal(2, await db.Set<UploadPendente>().CountAsync(Ct));
        Assert.All(await db.Set<Post>().ToListAsync(Ct), post => Assert.Equal(restante.Id, post.AutorId));
        Assert.Equal(2, await db.Set<Post>().CountAsync(Ct));
        var premioQueFica = await db.Set<PremioParticipacao>().SingleAsync(Ct);
        Assert.Equal(restante.Id, premioQueFica.AlunoId);
        Assert.Equal(premioQueFica.Id, (await db.Set<Contestacao>().SingleAsync(Ct)).PremioParticipacaoId);
        Assert.Equal(restante.Id, (await db.Set<Comprovacao>().SingleAsync(Ct)).AlunoId);
        Assert.Equal(restante.Id, (await db.Set<Evento>().SingleAsync(Ct)).CriadoPorId);
        Assert.Empty(await db.Set<Curtida>().ToListAsync(Ct));
        Assert.Empty(await db.Set<Seguimento>().ToListAsync(Ct));
        Assert.Equal("Valeu!", (await db.Set<Comentario>().SingleAsync(Ct)).Texto);
        Assert.Equal(1, await db.Set<CadastroElegivel>().CountAsync(cadastro => cadastro.Papeis == Papeis.Aluno, Ct));
        Assert.Equal(1, await db.Set<TermoDeUso>().CountAsync(Ct));
    }

    private async Task<MaxxApiFactory> MundoAsync(Action<IServiceCollection>? servicos = null)
    {
        var factory = new MaxxApiFactory(
            await postgres.CreateDatabaseAsync(),
            configuracao: new Dictionary<string, string> { ["Demo:Habilitada"] = "true", ["Demo:Lote"] = _lote },
            servicos: servicos);
        await factory.MigrateAsync(Ct);

        // Ano passado inteiro no passado; neste ano, o semestre 1 contém hoje.
        var anterior = Ano(Hoje.Year - 1, Hoje.AddDays(-400));
        var atual = Ano(Hoje.Year, Hoje.AddDays(-75));
        var cc = new Curso("Ciência da Computação", "CC", 8);
        var adm = new Curso("Administração", "ADM", 8);
        var bd = new Materia(cc, "Banco de Dados");
        var redes = new Materia(cc, "Redes");
        var contabilidade = new Materia(adm, "Contabilidade");
        await SalvarAsync(
            factory,
            TermoVigente(),
            anterior,
            atual,
            cc,
            adm,
            bd,
            redes,
            contabilidade,
            new Turma(bd, atual.Semestres[0]),
            new Turma(redes, atual.Semestres[0]),
            new Turma(bd, anterior.Semestres[1]),
            new Turma(contabilidade, anterior.Semestres[1]));
        return factory;
    }

    // Quatro bimestres seguidos de 50 dias a partir de `inicio`, com um dia de folga entre eles.
    private static AnoLetivo Ano(int ano, DateOnly inicio)
    {
        Bimestre Bimestre(int numero) =>
            new(numero, inicio.AddDays((numero - 1) * 51), inicio.AddDays(((numero - 1) * 51) + 49));

        return new AnoLetivo(
            ano,
            new SemestreLetivo(ano, 1, Bimestre(1), Bimestre(2)),
            new SemestreLetivo(ano, 2, Bimestre(3), Bimestre(4)));
    }

    private static async Task<RegistroDeDemonstracaoResponse> RegistrarAsync(HttpClient client, string nome)
    {
        var response = await client.PostAsJsonAsync(_alunos, new { nome, curso = "CC", versaoDoTermo = VersaoDoTermo }, Ct);
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

    // Status, tipo de conteúdo e corpo sem o traceId, que muda a cada pedido.
    private static async Task<string> DescreverAsync(HttpResponseMessage response)
    {
        var texto = await response.Content.ReadAsStringAsync(Ct);
        var corpo = string.IsNullOrEmpty(texto)
            ? string.Empty
            : string.Join(',', JsonDocument.Parse(texto).RootElement.EnumerateObject()
                .Where(campo => campo.Name != "traceId").Select(campo => $"{campo.Name}={campo.Value}"));
        return $"{(int)response.StatusCode} {response.Content.Headers.ContentType} {corpo}";
    }

    private static IEnumerable<string> Chaves(JsonNode no, string prefixo = "")
    {
        if (no is not JsonObject objeto)
        {
            yield break;
        }

        foreach (var (nome, filho) in objeto)
        {
            var chave = prefixo.Length == 0 ? nome : $"{prefixo}:{nome}";
            yield return chave;
            if (filho is not null)
            {
                foreach (var interna in Chaves(filho, chave))
                {
                    yield return interna;
                }
            }
        }
    }

    private static async Task AssertRecusaAsync(HttpResponseMessage response, HttpStatusCode status, string codigo)
    {
        Assert.Equal(status, response.StatusCode);
        using var corpo = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(codigo, corpo.RootElement.GetProperty("codigo").GetString());
    }

    private static string Sha256(string valor) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));

    // Toda gravação perde a corrida pelo prefixo; guarda qual prefixo cada tentativa pediu.
    private sealed class GravacaoSempreRecusada(MaxxDbContext db, List<string> tentados) : IUnitOfWork
    {
        public Task SalvarAsync(CancellationToken cancellationToken)
        {
            tentados.Add(db.ChangeTracker.Entries<Usuario>().Single().Entity.Prefixo);
            throw new ConflitoDeUnicidadeException(Unicidade.Prefixo, new InvalidOperationException("Prefixo já reservado."));
        }

        public void Descartar() => db.ChangeTracker.Clear();
    }

    private sealed class CaixaDeEmail : IEmailSender
    {
        private int _recebidos;

        public int Recebidos => _recebidos;

        public Task EnviarAsync(MensagemDeEmail mensagem, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _recebidos);
            return Task.CompletedTask;
        }
    }
}
