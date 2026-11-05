using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Maxx.Api.Autenticacao;
using Maxx.Application;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Infrastructure.Autenticacao;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Maxx.Api.IntegrationTests.BancoDeTeste;

namespace Maxx.Api.IntegrationTests.Autenticacao;

// Recuperação de senha e primeiro acesso pela API, contra o Postgres real. O SMTP é trocado
// por uma caixa em memória; o envio em segundo plano é o de verdade.
public sealed partial class RecuperacaoTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string _senha = "senha-boa-123";
    private const string _senhaNova = "senha-nova-456";
    private const string _outraSenha = "outra-senha-789";
    private const string _ana = "ana.souza@al.unieduk.com.br";
    private const string _renata = "renata@prof.unieduk.com.br";

    private static readonly Uri _recuperacao = new("/api/v1/auth/recuperacao", UriKind.Relative);
    private static readonly Uri _confirmar = new("/api/v1/auth/recuperacao/confirmar", UriKind.Relative);
    private static readonly Uri _login = new("/api/v1/auth/login", UriKind.Relative);
    private static readonly Uri _refresh = new("/api/v1/auth/refresh", UriKind.Relative);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Email_com_conta_sem_conta_e_inexistente_tem_a_mesma_resposta_e_so_a_conta_recebe()
    {
        var caixa = new CaixaDeEmail();
        await using var factory = await MundoAsync(caixa);
        using var client = factory.CreateClient();

        var semConta = await PedirAsync(client, "bruno.lima@al.unieduk.com.br");
        var inexistente = await PedirAsync(client, "ninguem@al.unieduk.com.br");
        var comConta = await PedirAsync(client, " ANA.Souza@al.unieduk.com.br ");

        foreach (var response in new[] { semConta, inexistente, comConta })
        {
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsByteArrayAsync(Ct));
        }

        // A fila é uma só e em ordem: se os dois primeiros tivessem gerado e-mail, ele
        // chegaria antes.
        var email = await caixa.ProximoAsync();
        Assert.Equal(_ana, email.Para);
        Assert.False(caixa.TemMais);
    }

    [Fact]
    public async Task O_email_leva_o_link_com_o_token_no_fragmento()
    {
        var caixa = new CaixaDeEmail();
        await using var factory = await MundoAsync(caixa);
        using var client = factory.CreateClient();

        await PedirAsync(client, _ana);
        var email = await caixa.ProximoAsync();

        Assert.Equal("Maxx: defina sua senha", email.Assunto);
        Assert.Contains("Olá, Ana.", email.Texto, StringComparison.Ordinal);
        Assert.Contains("30 minutos", email.Texto, StringComparison.Ordinal);
        Assert.Matches(@"http://localhost/redefinir-senha#token=[A-Za-z0-9_-]{43}\b", email.Texto);

        // O banco guarda o hash, nunca o token.
        await using var scope = factory.Services.CreateAsyncScope();
        var pedido = await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<RecuperacaoDeSenha>().SingleAsync(Ct);
        Assert.DoesNotContain(Token(email), pedido.Hash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Senha_nova_entra_e_derruba_todas_as_sessoes()
    {
        var caixa = new CaixaDeEmail();
        await using var factory = await MundoAsync(caixa);
        using var client = factory.CreateClient();
        var celular = await EntrarAsync(client, _ana, _senha);
        var notebook = await EntrarAsync(client, _ana, _senha);

        await PedirAsync(client, _ana);
        var response = await ConfirmarAsync(client, Token(await caixa.ProximoAsync()), _senhaNova);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        foreach (var refresh in new[] { celular, notebook })
        {
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                (await client.PostAsJsonAsync(_refresh, new { refreshToken = refresh }, Ct)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await PedirLoginAsync(client, _ana, _senha)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PedirLoginAsync(client, _ana, _senhaNova)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var familias = await scope.ServiceProvider.GetRequiredService<MaxxDbContext>().Set<FamiliaDeSessao>()
            .Where(familia => familia.RevogadaEm != null).ToListAsync(Ct);
        Assert.Equal(2, familias.Count);
        Assert.All(familias, familia => Assert.Equal(MotivoDaRevogacao.SenhaRedefinida, familia.MotivoDaRevogacao));
    }

    [Fact]
    public async Task Primeiro_acesso_do_professor_que_veio_do_seed_sem_senha()
    {
        var caixa = new CaixaDeEmail();
        await using var factory = await MundoAsync(caixa);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await PedirLoginAsync(client, _renata, _senhaNova)).StatusCode);

        await PedirAsync(client, _renata);
        await ConfirmarAsync(client, Token(await caixa.ProximoAsync()), _senhaNova);

        Assert.Equal(HttpStatusCode.OK, (await PedirLoginAsync(client, _renata, _senhaNova)).StatusCode);
    }

    [Fact]
    public async Task O_link_vale_uma_vez_so()
    {
        var caixa = new CaixaDeEmail();
        await using var factory = await MundoAsync(caixa);
        using var client = factory.CreateClient();
        await PedirAsync(client, _ana);
        var token = Token(await caixa.ProximoAsync());
        await ConfirmarAsync(client, token, _senhaNova);

        await AssertRecusaAsync(await ConfirmarAsync(client, token, _outraSenha), "recuperacao-invalida");
        Assert.Equal(HttpStatusCode.OK, (await PedirLoginAsync(client, _ana, _senhaNova)).StatusCode);
    }

    [Fact]
    public async Task O_link_expira_em_30_minutos()
    {
        var caixa = new CaixaDeEmail();
        var relogio = new RelogioDeTeste();
        await using var factory = await MundoAsync(caixa, relogio);
        using var client = factory.CreateClient();
        await PedirAsync(client, _ana);
        var token = Token(await caixa.ProximoAsync());

        relogio.Avancar(RecuperacaoDeSenha.Validade + TimeSpan.FromSeconds(1));

        await AssertRecusaAsync(await ConfirmarAsync(client, token, _senhaNova), "recuperacao-invalida");
    }

    [Fact]
    public async Task Concluir_um_pedido_invalida_o_link_anterior()
    {
        var caixa = new CaixaDeEmail();
        await using var factory = await MundoAsync(caixa);
        using var client = factory.CreateClient();
        await PedirAsync(client, _ana);
        var primeiro = Token(await caixa.ProximoAsync());
        await PedirAsync(client, _ana);
        var segundo = Token(await caixa.ProximoAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await ConfirmarAsync(client, segundo, _senhaNova)).StatusCode);

        await AssertRecusaAsync(await ConfirmarAsync(client, primeiro, _outraSenha), "recuperacao-invalida");
    }

    [Fact]
    public async Task Senha_fora_da_politica_e_recusada_e_o_link_continua_valendo()
    {
        var caixa = new CaixaDeEmail();
        await using var factory = await MundoAsync(caixa);
        using var client = factory.CreateClient();
        await PedirAsync(client, _ana);
        var token = Token(await caixa.ProximoAsync());

        await AssertRecusaAsync(await ConfirmarAsync(client, token, "curta"), "senha-fora-da-politica");

        Assert.Equal(HttpStatusCode.NoContent, (await ConfirmarAsync(client, token, _senhaNova)).StatusCode);
    }

    [Fact]
    public async Task Token_desconhecido_e_recusado()
    {
        await using var factory = await MundoAsync(new CaixaDeEmail());
        using var client = factory.CreateClient();

        await AssertRecusaAsync(await ConfirmarAsync(client, "nao-e-um-token", _senhaNova), "recuperacao-invalida");
    }

    [Fact]
    public async Task Duas_confirmacoes_simultaneas_do_mesmo_link_deixam_um_204_e_um_400()
    {
        var caixa = new CaixaDeEmail();
        await using var factory = await MundoAsync(caixa);
        using var client = factory.CreateClient();
        await PedirAsync(client, _ana);
        var token = Token(await caixa.ProximoAsync());

        var respostas = await Task.WhenAll(
            ConfirmarAsync(client, token, _senhaNova), ConfirmarAsync(client, token, _outraSenha));

        Assert.Single(respostas, response => response.StatusCode == HttpStatusCode.NoContent);
        await AssertRecusaAsync(
            Assert.Single(respostas, response => response.StatusCode != HttpStatusCode.NoContent), "recuperacao-invalida");
    }

    [Fact]
    public async Task Falha_no_smtp_nao_muda_a_resposta_e_fica_no_log()
    {
        var caixa = new CaixaDeEmail { Falhar = true };
        await using var factory = await MundoAsync(caixa);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Accepted, (await PedirAsync(client, _ana)).StatusCode);

        await caixa.ProximoAsync();
        await Esperar(() => factory.Log.Linhas.Any(linha => linha.StartsWith("Error", StringComparison.Ordinal)
            && linha.Contains("Falha ao enviar o e-mail", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Token_e_senha_nunca_aparecem_no_log()
    {
        var caixa = new CaixaDeEmail();
        await using var factory = await MundoAsync(caixa);
        using var client = factory.CreateClient();

        await PedirAsync(client, _ana);
        var token = Token(await caixa.ProximoAsync());
        await ConfirmarAsync(client, token, "curta-demais");
        await ConfirmarAsync(client, token, _senhaNova);
        await ConfirmarAsync(client, token, _senhaNova);
        await Esperar(() => factory.Log.Linhas.Any(linha => linha.Contains("enviado", StringComparison.Ordinal)));

        var log = string.Join('\n', factory.Log.Linhas);
        Assert.Contains("recuperacao-invalida", log, StringComparison.Ordinal);
        foreach (var segredo in new[] { token, _senhaNova, "curta-demais" })
        {
            Assert.DoesNotContain(segredo, log, StringComparison.Ordinal);
        }
    }

    private async Task<MaxxApiFactory> MundoAsync(CaixaDeEmail caixa, TimeProvider? relogio = null)
    {
        var factory = new MaxxApiFactory(
            await postgres.CreateDatabaseAsync(),
            servicos: servicos =>
            {
                servicos.AddSingleton<IEmailSender>(caixa);
                if (relogio is not null)
                {
                    servicos.AddSingleton(relogio);
                }
            });
        await factory.MigrateAsync(Ct);

        var cc = new Curso("Ciência da Computação", "CC", 8);
        var ana = CadastroElegivel.Aluno("20260101", _ana, "Ana Souza", cc, 3);
        var renata = CadastroElegivel.Professor(_renata, "Renata Albuquerque");
        await SalvarAsync(
            factory,
            cc,
            ana,
            CadastroElegivel.Aluno("20260102", "bruno.lima@al.unieduk.com.br", "Bruno Lima", cc, 3),
            renata,
            Usuario.CadastrarAluno(ana, cc, "Ana", "anaSouza", new Argon2idSenhaHasher().Hash(_senha), DateTimeOffset.UtcNow),
            Usuario.CriarPeloSeed(renata, "renataA", DateTimeOffset.UtcNow));
        return factory;
    }

    private static Task<HttpResponseMessage> PedirAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync(_recuperacao, new { email }, Ct);

    private static Task<HttpResponseMessage> ConfirmarAsync(HttpClient client, string token, string senha) =>
        client.PostAsJsonAsync(_confirmar, new { token, senha }, Ct);

    private static Task<HttpResponseMessage> PedirLoginAsync(HttpClient client, string identificador, string senha) =>
        client.PostAsJsonAsync(_login, new { identificador, senha }, Ct);

    private static async Task<string> EntrarAsync(HttpClient client, string identificador, string senha)
    {
        var response = await PedirLoginAsync(client, identificador, senha);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SessaoResponse>(Ct))!.RefreshToken!;
    }

    private static string Token(MensagemDeEmail email) => LinkDoToken().Match(email.Texto).Groups[1].Value;

    [GeneratedRegex("#token=([A-Za-z0-9_-]+)")]
    private static partial Regex LinkDoToken();

    private static async Task AssertRecusaAsync(HttpResponseMessage response, string codigo)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var corpo = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal(codigo, corpo.RootElement.GetProperty("codigo").GetString());
    }

    private static async Task Esperar(Func<bool> condicao)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        limite.CancelAfter(TimeSpan.FromSeconds(10));
        while (!condicao())
        {
            await Task.Delay(20, limite.Token);
        }
    }

    // No lugar do SMTP: guarda o que o envio em segundo plano entregou.
    private sealed class CaixaDeEmail : IEmailSender
    {
        private readonly Channel<MensagemDeEmail> _recebidos = Channel.CreateUnbounded<MensagemDeEmail>();

        public bool Falhar { get; init; }

        public bool TemMais => _recebidos.Reader.Count > 0;

        public Task EnviarAsync(MensagemDeEmail mensagem, CancellationToken cancellationToken)
        {
            _recebidos.Writer.TryWrite(mensagem);
            return Falhar ? throw new IOException("SMTP fora do ar") : Task.CompletedTask;
        }

        public async Task<MensagemDeEmail> ProximoAsync()
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            limite.CancelAfter(TimeSpan.FromSeconds(10));
            return await _recebidos.Reader.ReadAsync(limite.Token);
        }
    }

    private sealed class RelogioDeTeste : TimeProvider
    {
        private DateTimeOffset _agora = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan quanto) => _agora += quanto;
    }
}
