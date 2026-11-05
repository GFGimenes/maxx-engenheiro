using System.ComponentModel.DataAnnotations;
using Maxx.Api.Autenticacao;
using Maxx.Api.LimiteDeTaxa;
using Maxx.Application.Demonstracao;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Maxx.Api.Demonstracao;

// A flag da demonstração (CLAUDE.md, Segurança): desligada por padrão e ausente de todo
// appsettings versionado. Liga-se só por variável de ambiente, no compose da apresentação.
public sealed class DemoOptions
{
    public const string Section = "Demo";

    public bool Habilitada { get; set; }

    // Gravado em cada aluno de demonstração criado enquanto vale: a chave da purga.
    public string? Lote { get; set; }
}

internal static class DemonstracaoEndpoints
{
    // Devolve se as rotas devem existir. Desligada, elas nem são mapeadas: a resposta é o
    // 404 de uma rota inexistente, byte a byte. A geração do OpenAPI as mapeia mesmo assim,
    // para o cliente ter os tipos da tela do passo 5.
    public static bool AddDemonstracao(this WebApplicationBuilder builder, bool servindo, bool gerandoOpenApi)
    {
        var opcoes = builder.Configuration.GetSection(DemoOptions.Section).Get<DemoOptions>() ?? new DemoOptions();
        if (opcoes.Habilitada && servindo && string.IsNullOrWhiteSpace(opcoes.Lote))
        {
            throw new InvalidOperationException(
                $"{DemoOptions.Section}__Habilitada sem {DemoOptions.Section}__Lote: defina o lote da apresentação.");
        }

        builder.Services.AddSingleton(new ConfiguracaoDaDemonstracao(opcoes.Lote?.Trim() ?? string.Empty));
        builder.Services.AddScoped<DemonstracaoHandler>();

        return opcoes.Habilitada || gerandoOpenApi;
    }

    public static IEndpointRouteBuilder MapDemonstracaoEndpoints(this IEndpointRouteBuilder v1)
    {
        var demo = v1.MapGroup("/demo").WithTags("Demonstracao").AllowAnonymous();

        demo.MapGet("", CursosAsync).WithName("GetDemonstracao");

        demo.MapPost("/alunos", RegistrarAsync)
            .WithName("RegistrarAlunoDeDemonstracao")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ComLimiteDeTaxa(Limite.RegistroDeDemonstracaoPorIp, contexto => LimiteDeTaxaExtensions.Ip(contexto.HttpContext));

        demo.MapPost("/entrar", EntrarAsync)
            .WithName("EntrarNaDemonstracao")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return v1;
    }

    // Responder já diz que a demonstração está ligada; o corpo traz os cursos da escolha.
    private static async Task<Ok<DemonstracaoResponse>> CursosAsync(
        DemonstracaoHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(new DemonstracaoResponse(
            [.. (await handler.CursosAsync(cancellationToken)).Select(curso => new CursoDeDemonstracao(curso.Sigla, curso.Nome))]));

    private static async Task<Created<RegistroDeDemonstracaoResponse>> RegistrarAsync(
        RegistroDeDemonstracaoRequest pedido, DemonstracaoHandler handler, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var registro = await handler.RegistrarAsync(pedido.Nome, pedido.Curso, cancellationToken);
        return TypedResults.Created(
            (string?)null,
            new RegistroDeDemonstracaoResponse(registro.ChaveDoDispositivo, SessaoHttp.Escrever(httpContext, registro.Sessao)));
    }

    private static async Task<Ok<SessaoResponse>> EntrarAsync(
        EntradaDeDemonstracaoRequest pedido, DemonstracaoHandler handler, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var sessao = await handler.EntrarAsync(pedido.ChaveDoDispositivo, cancellationToken);
        return TypedResults.Ok(SessaoHttp.Escrever(httpContext, sessao));
    }
}

public sealed record DemonstracaoResponse(IReadOnlyList<CursoDeDemonstracao> Cursos);

public sealed record CursoDeDemonstracao(string Sigla, string Nome);

// O nome tem a forma do nome de exibição, conferida no domínio.
public sealed record RegistroDeDemonstracaoRequest(
    [property: Required(AllowEmptyStrings = true), StringLength(240)] string Nome,
    [property: Required, StringLength(16)] string Curso);

public sealed record EntradaDeDemonstracaoRequest([property: Required, StringLength(128)] string ChaveDoDispositivo)
{
    public override string ToString() => "EntradaDeDemonstracaoRequest { }";
}

// A chave do dispositivo vai sempre no corpo, na web também: o app a guarda para voltar,
// e ela só abre a conta de demonstração.
public sealed record RegistroDeDemonstracaoResponse(string ChaveDoDispositivo, SessaoResponse Sessao)
{
    public override string ToString() => "RegistroDeDemonstracaoResponse { }";
}
