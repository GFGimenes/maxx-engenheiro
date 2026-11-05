using Maxx.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Maxx.Api.Erros;

// Regra de domínio que escapou da validação de borda vira 400 no formato único. A
// mensagem da exceção é para o log, não para o usuário (DomainException): a resposta leva
// só o código, que o cliente traduz em texto amigável.
internal sealed partial class DomainExceptionHandler(
    IProblemDetailsService problemDetails, ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public const string Codigo = "requisicao-invalida";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
        {
            return false;
        }

        LogRecusada(logger, domainException.Message);

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Requisição inválida.",
                Extensions = { [Erro.CampoCodigo] = Codigo },
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Requisição recusada pelo domínio: {Motivo}")]
    private static partial void LogRecusada(ILogger logger, string motivo);
}

// O formato único de erro: ProblemDetails (RFC 9457) com a extensão "codigo", estável e
// em kebab-case, que o cliente usa para escolher a mensagem amigável.
public static class Erro
{
    public const string CampoCodigo = "codigo";

    // Toda resposta de erro sai com código: quem não trouxe o seu ganha o do status.
    public static void Customizar(ProblemDetailsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.CustomizeProblemDetails = context =>
        {
            var problema = context.ProblemDetails;
            if (problema.Extensions.ContainsKey(CampoCodigo))
            {
                return;
            }

            problema.Extensions[CampoCodigo] = problema switch
            {
                HttpValidationProblemDetails => "dados-invalidos",
                _ => problema.Status switch
                {
                    StatusCodes.Status400BadRequest => "requisicao-invalida",
                    StatusCodes.Status401Unauthorized => "nao-autenticado",
                    StatusCodes.Status403Forbidden => "sem-permissao",
                    StatusCodes.Status404NotFound => "nao-encontrado",
                    StatusCodes.Status429TooManyRequests => "limite-de-taxa",
                    StatusCodes.Status503ServiceUnavailable => "indisponivel",
                    _ => "erro-interno",
                },
            };
        };
    }
}
