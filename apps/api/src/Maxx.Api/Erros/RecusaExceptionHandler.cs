using Maxx.Application;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Maxx.Api.Erros;

// Recusa da Application vira o status do tipo e o código dela, no formato único.
internal sealed partial class RecusaExceptionHandler(
    IProblemDetailsService problemDetails, ILogger<RecusaExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not RecusaException recusa)
        {
            return false;
        }

        LogRecusada(logger, recusa.Codigo, recusa.Message);

        var status = recusa.Tipo switch
        {
            TipoDeRecusa.NaoEncontrado => StatusCodes.Status404NotFound,
            TipoDeRecusa.Conflito => StatusCodes.Status409Conflict,
            TipoDeRecusa.NaoAutorizado => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status400BadRequest,
        };

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Extensions = { [Erro.CampoCodigo] = recusa.Codigo },
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Pedido recusado ({Codigo}): {Motivo}")]
    private static partial void LogRecusada(ILogger logger, string codigo, string motivo);
}
