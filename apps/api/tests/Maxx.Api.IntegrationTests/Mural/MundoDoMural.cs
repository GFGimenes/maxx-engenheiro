using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Maxx.Api.Autenticacao;
using Maxx.Api.Fotos;
using Maxx.Api.IntegrationTests.Fotos;
using Maxx.Api.IntegrationTests.Social;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Infrastructure.Autenticacao;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Maxx.Api.IntegrationTests.Mural;

// O mundo dos testes do mural (passo 10). Dois cursos, CC e ADS. No semestre vigente, a
// Renata leciona Banco de Dados e Algoritmos (CC) e o Paulo, Web (ADS); no semestre
// seguinte, a Renata tem outra turma de Banco de Dados, ainda fora do escopo dela.
//
// Ana (CC) está em Banco de Dados e Algoritmos; Bruno (CC), só em Algoritmos; Carla (CC) é
// ex-aluna, matriculada em Banco de Dados; Diego (ADS), em Web. Eva (CC) está matriculada
// em Banco de Dados e não tem conta. A Coordenação é só instituição; a Marta, professora e
// instituição. Postgres e bucket reais.
internal sealed class MundoDoMural : IAsyncDisposable
{
    public const string Senha = "senha-boa-123";
    public const string Ana = "ana.souza@al.unieduk.com.br";
    public const string Bruno = "bruno.lima@al.unieduk.com.br";
    public const string Carla = "carla.dias@al.unieduk.com.br";
    public const string Diego = "diego.reis@al.unieduk.com.br";
    public const string Renata = "renata@prof.unieduk.com.br";
    public const string Paulo = "paulo@prof.unieduk.com.br";
    public const string Marta = "marta@prof.unieduk.com.br";
    public const string Coordenacao = "coordenacao@unieduk.com.br";
    public const long TamanhoDoFeed = MundoSocial.TamanhoDoFeed;
    public const long TamanhoDaGrade = MundoSocial.TamanhoDaGrade;

    private readonly Dictionary<string, Guid> _ids;

    private MundoDoMural(MaxxApiFactory factory, RelogioDeTeste relogio, Dictionary<string, Guid> ids)
    {
        Factory = factory;
        Relogio = relogio;
        _ids = ids;
    }

    public MaxxApiFactory Factory { get; }

    public RelogioDeTeste Relogio { get; }

    public Guid BancoDeDados { get; private init; }

    public Guid Algoritmos { get; private init; }

    public Guid Web { get; private init; }

    public Guid BancoDeDadosDoProximoSemestre { get; private init; }

    public Guid CC { get; private init; }

    public Guid ADS { get; private init; }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public Guid Id(string email) => _ids[email];

    public static async Task<MundoDoMural> CriarAsync(PostgresFixture postgres, ArmazenamentoFixture armazenamento)
    {
        var relogio = new RelogioDeTeste();
        var factory = new MaxxApiFactory(
            await postgres.CreateDatabaseAsync(),
            configuracao: armazenamento.Configuracao,
            servicos: colecao => colecao.Replace(ServiceDescriptor.Singleton<TimeProvider>(relogio)));
        await factory.MigrateAsync(Ct);

        // Hoje cai no bimestre 2 do semestre 1 de um ano letivo montado em volta dele.
        var hoje = FusoHorario.DataLocal(DateTimeOffset.UtcNow);
        var inicio = hoje.AddDays(-75);
        Bimestre Bimestre(int numero) => new(numero, inicio.AddDays((numero - 1) * 51), inicio.AddDays(((numero - 1) * 51) + 49));
        var ano = new AnoLetivo(
            hoje.Year, new SemestreLetivo(hoje.Year, 1, Bimestre(1), Bimestre(2)), new SemestreLetivo(hoje.Year, 2, Bimestre(3), Bimestre(4)));
        var cc = new Curso("Ciência da Computação", "CC", 8);
        var ads = new Curso("Análise e Desenvolvimento de Sistemas", "ADS", 5);
        var bd = new Materia(cc, "Banco de Dados");
        var alg = new Materia(cc, "Algoritmos");
        var web = new Materia(ads, "Web");
        var turmaBd = new Turma(bd, ano.Semestres[0]);
        var turmaAlg = new Turma(alg, ano.Semestres[0]);
        var turmaWeb = new Turma(web, ano.Semestres[0]);
        var turmaBdSeguinte = new Turma(bd, ano.Semestres[1]);

        // As contas nascem um minuto antes do relógio do teste: são de antes de qualquer aviso.
        var hash = new Argon2idSenhaHasher().Hash(Senha);
        var agora = relogio.GetUtcNow().AddMinutes(-1);
        var ana = CadastroElegivel.Aluno("20260101", Ana, "Ana Souza", cc, 3);
        var bruno = CadastroElegivel.Aluno("20260102", Bruno, "Bruno Lima", cc, 3);
        var carla = CadastroElegivel.Aluno("20260103", Carla, "Carla Dias", cc, 8);
        var diego = CadastroElegivel.Aluno("20260104", Diego, "Diego Reis", ads, 2);
        var eva = CadastroElegivel.Aluno("20260105", "eva.melo@al.unieduk.com.br", "Eva Melo", cc, 3);
        var renata = CadastroElegivel.Professor(Renata, "Renata Albuquerque");
        var paulo = CadastroElegivel.Professor(Paulo, "Paulo Santos");
        var marta = CadastroElegivel.Professor(Marta, "Marta Lima", tambemInstituicao: true);
        var coordenacao = CadastroElegivel.Instituicao(Coordenacao, "Coordenação UniMax");
        var contas = new Dictionary<string, Usuario>
        {
            [Ana] = Usuario.CadastrarAluno(ana, cc, "Ana Souza", "anaSouza", hash, agora),
            [Bruno] = Usuario.CadastrarAluno(bruno, cc, "Bruno Lima", "brunoLima", hash, agora),
            [Carla] = Usuario.CadastrarAluno(carla, cc, "Carla Dias", "carlaDias", hash, agora),
            [Diego] = Usuario.CadastrarAluno(diego, ads, "Diego Reis", "diegoReis", hash, agora),
            [Renata] = ComSenha(Usuario.CriarPeloSeed(renata, "renataA", agora), hash),
            [Paulo] = ComSenha(Usuario.CriarPeloSeed(paulo, "pauloS", agora), hash),
            [Marta] = ComSenha(Usuario.CriarPeloSeed(marta, "martaL", agora), hash),
            [Coordenacao] = ComSenha(Usuario.CriarPeloSeed(coordenacao, "coordenacao", agora), hash),
        };
        contas[Carla].MarcarExAluno(agora);

        await BancoDeTeste.SalvarAsync(
            factory,
            [
                ano, cc, ads, bd, alg, web, turmaBd, turmaAlg, turmaWeb, turmaBdSeguinte,
                ana, bruno, carla, diego, eva, renata, paulo, marta, coordenacao,
                new VinculoProfessor(renata, turmaBd),
                new VinculoProfessor(renata, turmaAlg),
                new VinculoProfessor(renata, turmaBdSeguinte),
                new VinculoProfessor(paulo, turmaWeb),
                new Matricula(ana, turmaBd),
                new Matricula(ana, turmaAlg),
                new Matricula(bruno, turmaAlg),
                new Matricula(carla, turmaBd),
                new Matricula(diego, turmaWeb),
                new Matricula(eva, turmaBd),
                .. contas.Values,
            ]);

        return new MundoDoMural(factory, relogio, contas.ToDictionary(par => par.Key, par => par.Value.Id))
        {
            BancoDeDados = turmaBd.Id,
            Algoritmos = turmaAlg.Id,
            Web = turmaWeb.Id,
            BancoDeDadosDoProximoSemestre = turmaBdSeguinte.Id,
            CC = cc.Id,
            ADS = ads.Id,
        };
    }

    public async Task<HttpClient> ComoAsync(string email)
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative), new { identificador = email, senha = Senha }, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = (await response.Content.ReadFromJsonAsync<SessaoResponse>(Ct))!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // Pedido de upload de aviso e os dois PUTs, como o app fará.
    public static async Task<UploadResponse> FotoDeAvisoAsync(HttpClient client, bool enviar = true)
    {
        var resposta = await client.PostAsJsonAsync(
            new Uri("/api/v1/uploads", UriKind.Relative),
            new
            {
                finalidade = "aviso",
                tipoDeConteudo = "image/jpeg",
                tamanhoDoFeed = MundoSocial.TamanhoDoFeed,
                tamanhoDaGrade = MundoSocial.TamanhoDaGrade,
            },
            Ct);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var upload = (await resposta.Content.ReadFromJsonAsync<UploadResponse>(Ct))!;
        if (enviar)
        {
            await PutAsync(upload.Feed, MundoSocial.TamanhoDoFeed);
            await PutAsync(upload.Grade, MundoSocial.TamanhoDaGrade);
        }

        return upload;
    }

    public async Task EsvaziarFilaAsync()
    {
        var consumidor = Factory.Services.GetRequiredService<ConsumidorDeTrabalhos>();
        while (await consumidor.ProcessarProximoAsync(Ct))
        {
        }
    }

    public async Task<List<T>> LinhasAsync<T>()
        where T : class
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<T>().AsNoTracking().ToListAsync(Ct);
    }

    public static Task AssertRecusaAsync(HttpResponseMessage response, HttpStatusCode status, string codigo) =>
        MundoSocial.AssertRecusaAsync(response, status, codigo);

    public ValueTask DisposeAsync() => Factory.DisposeAsync();

    private static async Task PutAsync(EnvioResponse envio, long tamanho)
    {
        using var http = new HttpClient();
        using var corpo = new ByteArrayContent(new byte[tamanho]);
        corpo.Headers.ContentType = new MediaTypeHeaderValue(envio.TipoDeConteudo);
        Assert.Equal(HttpStatusCode.OK, (await http.PutAsync(new Uri(envio.Url), corpo, Ct)).StatusCode);
    }

    private static Usuario ComSenha(Usuario usuario, string hash)
    {
        usuario.DefinirSenha(hash);
        return usuario;
    }
}
