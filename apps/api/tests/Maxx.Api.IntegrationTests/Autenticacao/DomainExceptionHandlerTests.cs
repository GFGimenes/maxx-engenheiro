using System.Text.Json;
using Maxx.Api.Erros;
using Maxx.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maxx.Api.IntegrationTests.Autenticacao;

public class DomainExceptionHandlerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Regra_de_dominio_vira_400_com_codigo_e_sem_a_mensagem_interna()
    {
        var (handler, context) = Montar();

        var tratou = await handler.TryHandleAsync(context, new DomainException("RA 20260101 já tem conta."), Ct);

        Assert.True(tratou);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);

        context.Response.Body.Position = 0;
        var texto = await new StreamReader(context.Response.Body).ReadToEndAsync(Ct);
        using var corpo = JsonDocument.Parse(texto);
        Assert.Equal("requisicao-invalida", corpo.RootElement.GetProperty("codigo").GetString());
        Assert.DoesNotContain("20260101", texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Outra_excecao_segue_para_o_tratamento_padrao()
    {
        var (handler, context) = Montar();

        Assert.False(await handler.TryHandleAsync(context, new InvalidOperationException("falha"), Ct));
    }

    private static (DomainExceptionHandler Handler, HttpContext Context) Montar()
    {
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();

        var handler = new DomainExceptionHandler(
            services.GetRequiredService<IProblemDetailsService>(), NullLogger<DomainExceptionHandler>.Instance);
        return (handler, context);
    }
}
