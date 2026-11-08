using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Maxx.Api.Autenticacao;
using Maxx.Api.Fotos;
using Maxx.Api.IntegrationTests.Fotos;
using Maxx.Api.IntegrationTests.Medalhas;
using Maxx.Application.Fotos;
using Maxx.Application.Medalhas;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Infrastructure.Autenticacao;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Maxx.Api.IntegrationTests.Social;

// O mundo dos testes da parte social (passo 7): um curso com uma turma vigente de Banco de
// Dados, três alunos com conta (Ana e Bruno no 3º semestre, Diego no 5º), uma ex-aluna
// (Carla), uma professora da turma (Renata) e a instituição. Postgres e bucket reais.
internal sealed class MundoSocial : IAsyncDisposable
{
    public const string Senha = "senha-boa-123";
    public const string Ana = "ana.souza@al.unieduk.com.br";
    public const string Bruno = "bruno.lima@al.unieduk.com.br";
    public const string Carla = "carla.dias@al.unieduk.com.br";
    public const string Diego = "diego.reis@al.unieduk.com.br";
    public const string Renata = "renata@prof.unieduk.com.br";
    public const string Coordenacao = "coordenacao@unieduk.com.br";
    public const long Feed = 300 * 1024;
    public const long Grade = 40 * 1024;

    private readonly Dictionary<string, Guid> _ids;

    private MundoSocial(MaxxApiFactory factory, RelogioDeTeste relogio, Guid turma, Dictionary<string, Guid> ids)
    {
        Factory = factory;
        Relogio = relogio;
        Turma = turma;
        _ids = ids;
    }

    public MaxxApiFactory Factory { get; }

    public RelogioDeTeste Relogio { get; }

    public Guid Turma { get; }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public Guid Id(string email) => _ids[email];

    public static async Task<MundoSocial> CriarAsync(
        PostgresFixture postgres, ArmazenamentoFixture armazenamento, Action<IServiceCollection>? servicos = null)
    {
        var relogio = new RelogioDeTeste();
        var factory = new MaxxApiFactory(
            await postgres.CreateDatabaseAsync(),
            configuracao: armazenamento.Configuracao,
            servicos: colecao =>
            {
                colecao.Replace(ServiceDescriptor.Singleton<TimeProvider>(relogio));
                servicos?.Invoke(colecao);
            });
        await factory.MigrateAsync(Ct);

        // Hoje cai no bimestre 2 do semestre 1 de um ano letivo montado em volta dele.
        var hoje = FusoHorario.DataLocal(DateTimeOffset.UtcNow);
        var inicio = hoje.AddDays(-75);
        Bimestre Bimestre(int numero) => new(numero, inicio.AddDays((numero - 1) * 51), inicio.AddDays(((numero - 1) * 51) + 49));
        var ano = new AnoLetivo(
            hoje.Year, new SemestreLetivo(hoje.Year, 1, Bimestre(1), Bimestre(2)), new SemestreLetivo(hoje.Year, 2, Bimestre(3), Bimestre(4)));
        var cc = new Curso("Ciência da Computação", "CC", 8);
        var bd = new Materia(cc, "Banco de Dados");
        var turma = new Turma(bd, ano.Semestres[0]);

        var hash = new Argon2idSenhaHasher().Hash(Senha);
        var agora = DateTimeOffset.UtcNow;
        var ana = CadastroElegivel.Aluno("20260101", Ana, "Ana Souza", cc, 3);
        var bruno = CadastroElegivel.Aluno("20260102", Bruno, "Bruno Lima", cc, 3);
        var carla = CadastroElegivel.Aluno("20260103", Carla, "Carla Dias", cc, 8);
        var diego = CadastroElegivel.Aluno("20260104", Diego, "Diego Reis", cc, 5);
        var renata = CadastroElegivel.Professor(Renata, "Renata Albuquerque");
        var coordenacao = CadastroElegivel.Instituicao(Coordenacao, "Coordenação UniMax");
        var contas = new Dictionary<string, Usuario>
        {
            [Ana] = Usuario.CadastrarAluno(ana, cc, "Ana Souza", "anaSouza", hash, agora),
            [Bruno] = Usuario.CadastrarAluno(bruno, cc, "Bruno Lima", "brunoLima", hash, agora),
            [Carla] = Usuario.CadastrarAluno(carla, cc, "Carla Dias", "carlaDias", hash, agora),
            [Diego] = Usuario.CadastrarAluno(diego, cc, "Diego Reis", "diegoReis", hash, agora),
            [Renata] = ComSenha(Usuario.CriarPeloSeed(renata, "renataA", agora), hash),
            [Coordenacao] = ComSenha(Usuario.CriarPeloSeed(coordenacao, "coordenacao", agora), hash),
        };
        contas[Carla].MarcarExAluno(agora);

        await BancoDeTeste.SalvarAsync(
            factory,
            [
                ano, cc, bd, turma, ana, bruno, carla, diego, renata, coordenacao,
                new VinculoProfessor(renata, turma),
                new Matricula(ana, turma),
                new Matricula(bruno, turma),
                new Matricula(diego, turma),
                CatalogoDeMedalhas.M01(),
                CatalogoDeMedalhas.M03(),
                .. contas.Values,
            ]);

        return new MundoSocial(factory, relogio, turma.Id, contas.ToDictionary(par => par.Key, par => par.Value.Id));
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

    // Pela concessão de verdade, da Renata, na turma de Banco de Dados.
    public async Task<Guid> MedalhaAsync(string aluno, string item = "M01")
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var medalha = await scope.ServiceProvider.GetRequiredService<ConcederMedalhaHandler>()
            .ConcederAsync(Id(Renata), Turma, Id(aluno), item, Ct);
        return medalha.Id;
    }

    public async Task AnularAsync(Guid medalha)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AnularMedalhaHandler>().AnularAsync(medalha, Coordenacao, Ct);
    }

    // Pedido de upload de post e os dois PUTs, como o app fará.
    public static async Task<UploadResponse> FotoEnviadaAsync(HttpClient client, bool enviar = true)
    {
        var resposta = await client.PostAsJsonAsync(
            new Uri("/api/v1/uploads", UriKind.Relative),
            new { finalidade = "post", tipoDeConteudo = "image/jpeg", tamanhoDoFeed = Feed, tamanhoDaGrade = Grade },
            Ct);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var upload = (await resposta.Content.ReadFromJsonAsync<UploadResponse>(Ct))!;
        if (enviar)
        {
            await PutAsync(upload.Feed, Feed);
            await PutAsync(upload.Grade, Grade);
        }

        return upload;
    }

    public IArmazenamentoDeFotos Bucket => Factory.Services.GetRequiredService<IArmazenamentoDeFotos>();

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

    public static async Task AssertRecusaAsync(HttpResponseMessage response, HttpStatusCode status, string codigo)
    {
        Assert.Equal(status, response.StatusCode);
        using var corpo = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(codigo, corpo.RootElement.GetProperty("codigo").GetString());
    }

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

internal sealed class RelogioDeTeste : TimeProvider
{
    // Em segundo cheio: o Postgres guarda microssegundos, e a comparação é exata.
    private DateTimeOffset _agora = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    public override DateTimeOffset GetUtcNow() => _agora;

    public void Avancar(TimeSpan quanto) => _agora += quanto;
}
