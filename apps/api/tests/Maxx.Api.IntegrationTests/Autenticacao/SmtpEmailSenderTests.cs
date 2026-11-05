using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Maxx.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Maxx.Api.IntegrationTests.Autenticacao;

// O SMTP de verdade, com MailKit, contra o mesmo Mailpit do compose.
public sealed class SmtpEmailSenderTests : IAsyncLifetime
{
    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:v1.33.0")
        .WithPortBinding(1025, assignRandomHostPort: true)
        .WithPortBinding(8025, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(pedido => pedido.ForPort(8025).ForPath("/livez")))
        .Build();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await _mailpit.StartAsync(Ct);

    public async ValueTask DisposeAsync() => await _mailpit.DisposeAsync();

    [Fact]
    public async Task Entrega_o_texto_ao_servidor_smtp_com_o_remetente_configurado()
    {
        await using var factory = new MaxxApiFactory(
            MaxxApiFactory.UnreachableDatabase,
            configuracao: new Dictionary<string, string>
            {
                ["Email:Remetente"] = "Maxx <nao-responda@maxx.teste>",
                ["Email:Smtp:Host"] = _mailpit.Hostname,
                ["Email:Smtp:Porta"] = _mailpit.GetMappedPublicPort(1025).ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Email:Smtp:Seguranca"] = "None",
            });

        await factory.Services.GetRequiredService<IEmailSender>().EnviarAsync(
            new MensagemDeEmail("ana.souza@al.unieduk.com.br", "Maxx: defina sua senha", "Olá, Ana. Acentuação: ç, ã, é."), Ct);

        using var http = new HttpClient { BaseAddress = new Uri($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(8025)}") };
        using var lista = JsonDocument.Parse(await http.GetStringAsync(new Uri("/api/v1/messages", UriKind.Relative), Ct));
        var mensagem = Assert.Single(lista.RootElement.GetProperty("messages").EnumerateArray());
        Assert.Equal("Maxx: defina sua senha", mensagem.GetProperty("Subject").GetString());
        Assert.Equal("nao-responda@maxx.teste", mensagem.GetProperty("From").GetProperty("Address").GetString());
        Assert.Equal("ana.souza@al.unieduk.com.br", mensagem.GetProperty("To")[0].GetProperty("Address").GetString());

        var completa = await http.GetFromJsonAsync<JsonElement>(
            new Uri($"/api/v1/message/{mensagem.GetProperty("ID").GetString()}", UriKind.Relative), Ct);
        Assert.Equal("Olá, Ana. Acentuação: ç, ã, é.", completa.GetProperty("Text").GetString()!.Trim());
    }
}
