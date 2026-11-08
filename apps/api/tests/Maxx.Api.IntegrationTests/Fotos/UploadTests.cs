using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Maxx.Api.Autenticacao;
using Maxx.Api.Fotos;
using Maxx.Application.Fila;
using Maxx.Application.Fotos;
using Maxx.Domain.Academico;
using Maxx.Domain.Fotos;
using Maxx.Domain.Identidade;
using Maxx.Infrastructure.Autenticacao;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Maxx.Api.IntegrationTests.Fotos;

// O pedido de envio de foto e a limpeza do que ninguém confirmou, contra o Postgres e o
// emulador do bucket de verdade (ADR 0002, ADR 0003 e ADR 0008).
public sealed class UploadTests(PostgresFixture postgres, ArmazenamentoFixture armazenamento)
    : IClassFixture<PostgresFixture>, IClassFixture<ArmazenamentoFixture>
{
    private const string _senha = "senha-boa-123";
    private const string _ana = "ana.souza@al.unieduk.com.br";
    private const string _bruno = "bruno.lima@al.unieduk.com.br";
    private const string _carla = "carla.dias@al.unieduk.com.br";
    private const string _renata = "renata@prof.unieduk.com.br";
    private const string _coordenacao = "coordenacao@unieduk.com.br";
    private const long _feed = 300 * 1024;
    private const long _grade = 40 * 1024;

    private static readonly Uri _uploads = new("/api/v1/uploads", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Aluna_recebe_duas_urls_e_envia_as_duas_derivadas_direto_ao_bucket()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var ana = await ComoAsync(factory, _ana);

        var resposta = await PedirAsync(ana, "post", "image/jpeg", _feed, _grade);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var upload = (await resposta.Content.ReadFromJsonAsync<UploadResponse>(Ct))!;
        Assert.Equal((_feed, _grade), (upload.Feed.Tamanho, upload.Grade.Tamanho));
        Assert.All([upload.Feed, upload.Grade], envio => Assert.Equal("image/jpeg", envio.TipoDeConteudo));
        Assert.Equal(mundo.Relogio.GetUtcNow().AddMinutes(15), upload.ExpiraEm);

        // A URL aponta para o endereço público do bucket, nunca para a API.
        Assert.All([upload.Feed, upload.Grade], envio => Assert.StartsWith(armazenamento.Endpoint.ToString(), envio.Url, StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.OK, (await PutAsync(upload.Feed, _feed)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PutAsync(upload.Grade, _grade)).StatusCode);

        var linha = Assert.Single(await LinhasAsync<UploadPendente>(factory));
        Assert.Equal((upload.Id, mundo.Ana, FinalidadeDoUpload.Post), (linha.Id, linha.UsuarioId, linha.Finalidade));
        Assert.Null(linha.ConfirmadoEm);
        Assert.True(await Bucket(factory).ExisteAsync(linha.ChaveDoFeed, Ct));
        Assert.True(await Bucket(factory).ExisteAsync(linha.ChaveDaGrade, Ct));

        // A limpeza já nasce na fila, adiada 24 horas, na mesma transação do registro.
        var limpeza = Assert.Single(await LinhasAsync<Trabalho>(factory));
        Assert.Equal(LimparUploadAbandonado.Tipo, limpeza.Tipo);
        Assert.Equal(mundo.Relogio.GetUtcNow().AddHours(24), limpeza.DisponivelEm);
        Assert.Contains(upload.Id.ToString(), limpeza.Carga, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Url_assinada_nao_aceita_derivada_de_outro_tamanho_nem_de_outro_tipo()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var ana = await ComoAsync(factory, _ana);
        var upload = (await (await PedirAsync(ana, "post", "image/jpeg", _feed, _grade)).Content.ReadFromJsonAsync<UploadResponse>(Ct))!;

        // A grade enviada na URL do feed: outro tamanho. E o feed declarado como PNG.
        Assert.Equal(HttpStatusCode.Forbidden, (await PutAsync(upload.Feed, _grade)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PutAsync(upload.Feed, _feed, "image/png")).StatusCode);
    }

    [Theory]
    [InlineData("image/png", _feed, _grade, "tipo-de-foto-nao-aceito")]
    [InlineData("image/heic", _feed, _grade, "tipo-de-foto-nao-aceito")]
    [InlineData("image/jpeg", 0, _grade, "tamanho-de-foto-invalido")]
    [InlineData("image/jpeg", UploadPendente.TamanhoMaximoDoFeed + 1, _grade, "tamanho-de-foto-invalido")]
    [InlineData("image/jpeg", _feed, UploadPendente.TamanhoMaximoDaGrade + 1, "tamanho-de-foto-invalido")]
    public async Task Tipo_ou_tamanho_fora_da_regra_e_400_e_nada_e_registrado(string tipo, long feed, long grade, string codigo)
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var ana = await ComoAsync(factory, _ana);

        await AssertRecusaAsync(await PedirAsync(ana, "post", tipo, feed, grade), HttpStatusCode.BadRequest, codigo);

        Assert.Empty(await LinhasAsync<UploadPendente>(factory));
        Assert.Empty(await LinhasAsync<Trabalho>(factory));
    }

    [Theory]
    [InlineData("story")]
    [InlineData("")]
    public async Task Finalidade_desconhecida_e_dados_invalidos(string finalidade)
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var ana = await ComoAsync(factory, _ana);

        await AssertRecusaAsync(await PedirAsync(ana, finalidade, "image/jpeg", _feed, _grade), HttpStatusCode.BadRequest, "dados-invalidos");
    }

    [Theory]
    [InlineData(_ana, "post", HttpStatusCode.Created)]
    [InlineData(_ana, "fotoDePerfil", HttpStatusCode.Created)]
    [InlineData(_carla, "post", HttpStatusCode.Forbidden)]
    [InlineData(_carla, "fotoDePerfil", HttpStatusCode.Created)]
    [InlineData(_renata, "post", HttpStatusCode.Forbidden)]
    [InlineData(_renata, "fotoDePerfil", HttpStatusCode.Created)]
    [InlineData(_coordenacao, "post", HttpStatusCode.Forbidden)]
    [InlineData(_coordenacao, "fotoDePerfil", HttpStatusCode.Forbidden)]
    public async Task Post_e_de_aluno_ativo_e_foto_de_perfil_de_aluno_e_professor(string email, string finalidade, HttpStatusCode esperado)
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var client = await ComoAsync(factory, email);

        var resposta = await PedirAsync(client, finalidade, "image/jpeg", _feed, _grade);

        if (esperado == HttpStatusCode.Forbidden)
        {
            await AssertRecusaAsync(resposta, HttpStatusCode.Forbidden, "envio-nao-permitido");
        }
        else
        {
            Assert.Equal(esperado, resposta.StatusCode);
        }
    }

    [Fact]
    public async Task Sem_login_e_401()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var anonimo = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await PedirAsync(anonimo, "post", "image/jpeg", _feed, _grade)).StatusCode);
    }

    [Fact]
    public async Task Limite_e_30_pedidos_por_hora_por_usuario_e_o_31o_e_429()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var ana = await ComoAsync(factory, _ana);
        using var bruno = await ComoAsync(factory, _bruno);

        for (var pedido = 0; pedido < 30; pedido++)
        {
            Assert.Equal(HttpStatusCode.Created, (await PedirAsync(ana, "post", "image/jpeg", _feed, _grade)).StatusCode);
        }

        var estourado = await PedirAsync(ana, "post", "image/jpeg", _feed, _grade);
        await AssertRecusaAsync(estourado, HttpStatusCode.TooManyRequests, "limite-de-taxa");
        var espera = int.Parse(Assert.Single(estourado.Headers.GetValues("Retry-After")), System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(espera, 1, 3600);

        // O limite é de cada um: o Bruno, na mesma réplica, segue enviando.
        Assert.Equal(HttpStatusCode.Created, (await PedirAsync(bruno, "post", "image/jpeg", _feed, _grade)).StatusCode);
        Assert.Equal(31, (await LinhasAsync<UploadPendente>(factory)).Count);
    }

    [Fact]
    public async Task Pedido_recusado_tambem_conta_no_limite()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var ana = await ComoAsync(factory, _ana);

        for (var pedido = 0; pedido < 30; pedido++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await PedirAsync(ana, "post", "image/png", _feed, _grade)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await PedirAsync(ana, "post", "image/jpeg", _feed, _grade)).StatusCode);
    }

    [Fact]
    public async Task Upload_abandonado_sai_do_bucket_e_do_banco_so_depois_de_24_horas()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var ana = await ComoAsync(factory, _ana);
        var upload = await EnviadoAsync(ana);
        var linha = Assert.Single(await LinhasAsync<UploadPendente>(factory));

        mundo.Relogio.Avancar(TimeSpan.FromHours(23));
        await EsvaziarAsync(factory);
        Assert.Single(await LinhasAsync<UploadPendente>(factory));
        Assert.True(await Bucket(factory).ExisteAsync(linha.ChaveDoFeed, Ct));

        mundo.Relogio.Avancar(TimeSpan.FromHours(1));
        await EsvaziarAsync(factory);

        Assert.Empty(await LinhasAsync<UploadPendente>(factory));
        Assert.Empty(await LinhasAsync<Trabalho>(factory));
        Assert.False(await Bucket(factory).ExisteAsync(linha.ChaveDoFeed, Ct));
        Assert.False(await Bucket(factory).ExisteAsync(linha.ChaveDaGrade, Ct));
        Assert.Equal(upload.Id, linha.Id);
    }

    [Fact]
    public async Task Upload_cujo_put_nunca_chegou_tambem_e_limpo()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var ana = await ComoAsync(factory, _ana);
        Assert.Equal(HttpStatusCode.Created, (await PedirAsync(ana, "post", "image/jpeg", _feed, _grade)).StatusCode);

        mundo.Relogio.Avancar(TimeSpan.FromHours(24));
        await EsvaziarAsync(factory);

        Assert.Empty(await LinhasAsync<UploadPendente>(factory));
        Assert.Empty(await LinhasAsync<Trabalho>(factory));
    }

    [Fact]
    public async Task Upload_confirmado_nao_e_tocado_pela_limpeza()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var ana = await ComoAsync(factory, _ana);
        await EnviadoAsync(ana);
        var linha = Assert.Single(await LinhasAsync<UploadPendente>(factory));
        await ConfirmarNoBancoAsync(factory, linha.Id);

        mundo.Relogio.Avancar(TimeSpan.FromHours(24));
        await EsvaziarAsync(factory);

        Assert.NotNull(Assert.Single(await LinhasAsync<UploadPendente>(factory)).ConfirmadoEm);
        Assert.Empty(await LinhasAsync<Trabalho>(factory));
        Assert.True(await Bucket(factory).ExisteAsync(linha.ChaveDoFeed, Ct));
        Assert.True(await Bucket(factory).ExisteAsync(linha.ChaveDaGrade, Ct));
    }

    [Fact]
    public async Task Limpeza_que_roda_de_novo_nao_acha_nada_e_termina()
    {
        var mundo = await MundoAsync();
        await using var factory = mundo.Factory;
        using var ana = await ComoAsync(factory, _ana);
        await EnviadoAsync(ana);
        var linha = Assert.Single(await LinhasAsync<UploadPendente>(factory));

        for (var vez = 0; vez < 2; vez++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ITrabalhoHandler<LimparUploadAbandonado>>()
                .ExecutarAsync(new LimparUploadAbandonado(linha.Id), Ct);
        }

        Assert.Empty(await LinhasAsync<UploadPendente>(factory));
        Assert.False(await Bucket(factory).ExisteAsync(linha.ChaveDoFeed, Ct));
    }

    [Fact]
    public async Task Confirmacao_no_meio_da_limpeza_vence_e_a_foto_fica()
    {
        // Entre a leitura da limpeza e o DELETE dela, outra conexão confirma o upload. O
        // DELETE perde pelo xmin antes de tocar no bucket; na retentativa, o upload já está
        // confirmado, e a limpeza termina sem remover nada.
        var relogio = new RelogioDeTeste();
        var mundo = await MundoAsync(relogio, ConfirmaDepoisDeLer.Decorar);
        await using var factory = mundo.Factory;
        using var ana = await ComoAsync(factory, _ana);
        await EnviadoAsync(ana);
        var linha = Assert.Single(await LinhasAsync<UploadPendente>(factory));
        ConfirmaDepoisDeLer.Conexao = mundo.ConnectionString;

        relogio.Avancar(TimeSpan.FromHours(24));
        await EsvaziarAsync(factory);

        var falhou = Assert.Single(await LinhasAsync<Trabalho>(factory));
        Assert.Equal(1, falhou.Tentativas);
        Assert.True(await Bucket(factory).ExisteAsync(linha.ChaveDoFeed, Ct));

        ConfirmaDepoisDeLer.Conexao = null;
        relogio.Avancar(TimeSpan.FromMinutes(1));
        await EsvaziarAsync(factory);

        Assert.Empty(await LinhasAsync<Trabalho>(factory));
        Assert.NotNull(Assert.Single(await LinhasAsync<UploadPendente>(factory)).ConfirmadoEm);
        Assert.True(await Bucket(factory).ExisteAsync(linha.ChaveDoFeed, Ct));
        Assert.True(await Bucket(factory).ExisteAsync(linha.ChaveDaGrade, Ct));
    }

    private async Task<Mundo> MundoAsync(RelogioDeTeste? relogio = null, Action<IServiceCollection>? servicos = null)
    {
        relogio ??= new RelogioDeTeste();
        var conexao = await postgres.CreateDatabaseAsync();
        var factory = new MaxxApiFactory(
            conexao,
            configuracao: armazenamento.Configuracao,
            servicos: colecao =>
            {
                colecao.Replace(ServiceDescriptor.Singleton<TimeProvider>(relogio));
                servicos?.Invoke(colecao);
            });
        await factory.MigrateAsync(Ct);

        var hash = new Argon2idSenhaHasher().Hash(_senha);
        var agora = DateTimeOffset.UtcNow;
        var cc = new Curso("Ciência da Computação", "CC", 8);
        var ana = CadastroElegivel.Aluno("20260101", _ana, "Ana Souza", cc, 3);
        var bruno = CadastroElegivel.Aluno("20260102", _bruno, "Bruno Lima", cc, 3);
        var carla = CadastroElegivel.Aluno("20260103", _carla, "Carla Dias", cc, 8);
        var renata = CadastroElegivel.Professor(_renata, "Renata Albuquerque");
        var coordenacao = CadastroElegivel.Instituicao(_coordenacao, "Coordenação UniMax");
        var contaDaAna = Usuario.CadastrarAluno(ana, cc, "Ana", "anaSouza", hash, agora);
        var contaDaCarla = Usuario.CadastrarAluno(carla, cc, "Carla", "carlaDias", hash, agora);
        contaDaCarla.MarcarExAluno(agora);

        await BancoDeTeste.SalvarAsync(
            factory,
            cc,
            ana,
            bruno,
            carla,
            renata,
            coordenacao,
            contaDaAna,
            Usuario.CadastrarAluno(bruno, cc, "Bruno", "brunoLima", hash, agora),
            contaDaCarla,
            ComSenha(Usuario.CriarPeloSeed(renata, "renataA", agora), hash),
            ComSenha(Usuario.CriarPeloSeed(coordenacao, "coordenacao", agora), hash));

        return new Mundo(factory, relogio, conexao, contaDaAna.Id);
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

    private static Task<HttpResponseMessage> PedirAsync(HttpClient client, string finalidade, string tipo, long feed, long grade) =>
        client.PostAsJsonAsync(_uploads, new { finalidade, tipoDeConteudo = tipo, tamanhoDoFeed = feed, tamanhoDaGrade = grade }, Ct);

    private static async Task<UploadResponse> EnviadoAsync(HttpClient client)
    {
        var resposta = await PedirAsync(client, "post", "image/jpeg", _feed, _grade);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var upload = (await resposta.Content.ReadFromJsonAsync<UploadResponse>(Ct))!;
        Assert.Equal(HttpStatusCode.OK, (await PutAsync(upload.Feed, _feed)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PutAsync(upload.Grade, _grade)).StatusCode);
        return upload;
    }

    // Como o app fará: PUT direto na URL assinada, sem token da API.
    private static async Task<HttpResponseMessage> PutAsync(EnvioResponse envio, long tamanho, string? tipo = null)
    {
        using var http = new HttpClient();
        using var corpo = new ByteArrayContent(new byte[tamanho]);
        corpo.Headers.ContentType = new MediaTypeHeaderValue(tipo ?? envio.TipoDeConteudo);
        return await http.PutAsync(new Uri(envio.Url), corpo, Ct);
    }

    private static IArmazenamentoDeFotos Bucket(MaxxApiFactory factory) => factory.Services.GetRequiredService<IArmazenamentoDeFotos>();

    private static async Task ConfirmarNoBancoAsync(MaxxApiFactory factory, Guid uploadId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Database
            .ExecuteSqlAsync($"UPDATE upload_pendente SET confirmado_em = now() WHERE id = {uploadId}", Ct);
    }

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

    private static async Task AssertRecusaAsync(HttpResponseMessage response, HttpStatusCode status, string codigo)
    {
        Assert.Equal(status, response.StatusCode);
        using var corpo = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(codigo, corpo.RootElement.GetProperty("codigo").GetString());
    }

    private sealed record Mundo(MaxxApiFactory Factory, RelogioDeTeste Relogio, string ConnectionString, Guid Ana);

    internal sealed class RelogioDeTeste : TimeProvider
    {
        // Em segundo cheio: o Postgres guarda microssegundos, e a comparação é exata.
        private DateTimeOffset _agora = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan quanto) => _agora += quanto;
    }

    // O repositório da limpeza, com uma confirmação vinda de outra conexão logo depois da
    // leitura: a corrida que o xmin precisa decidir.
    private sealed class ConfirmaDepoisDeLer(IUploadPendenteRepository interno) : IUploadPendenteRepository
    {
        public static string? Conexao { get; set; }

        // Troca o repositório registrado pela infraestrutura por este, por cima dele.
        public static void Decorar(IServiceCollection servicos)
        {
            var original = servicos.Single(descritor => descritor.ServiceType == typeof(IUploadPendenteRepository));
            servicos.Remove(original);
            servicos.AddScoped<IUploadPendenteRepository>(provider => new ConfirmaDepoisDeLer(
                (IUploadPendenteRepository)ActivatorUtilities.CreateInstance(provider, original.ImplementationType!)));
        }

        public void Adicionar(UploadPendente upload) => interno.Adicionar(upload);

        public async Task<UploadPendente?> BuscarAsync(Guid id, CancellationToken cancellationToken)
        {
            var upload = await interno.BuscarAsync(id, cancellationToken);
            if (Conexao is { } conexao)
            {
                await using var outra = new NpgsqlConnection(conexao);
                await outra.OpenAsync(cancellationToken);
                await using var comando = new NpgsqlCommand("UPDATE upload_pendente SET confirmado_em = now() WHERE id = @id", outra);
                comando.Parameters.AddWithValue("id", id);
                await comando.ExecuteNonQueryAsync(cancellationToken);
            }

            return upload;
        }

        public void Remover(UploadPendente upload) => interno.Remover(upload);
    }
}
