using System.Globalization;
using System.Threading.RateLimiting;
using Maxx.Api.Erros;

namespace Maxx.Api.LimiteDeTaxa;

// Os limites de taxa (decisoes/README.md, Autenticação), por réplica e em memória, com os
// limitadores nativos de System.Threading.RateLimiting. Filtro de endpoint, e não policy
// do middleware, porque três das chaves estão no corpo (identificador, RA, e-mail), e o
// particionador do middleware roda antes de o corpo ser lido.
internal enum Limite
{
    LoginPorIdentificador,
    LoginPorIp,
    CadastroPorRa,

    // Sugestão e disponibilidade de arroba, juntas: as duas respondem diferente para um par
    // de RA e e-mail que existe. 60 por minuto cabe na digitação porque o cliente do passo
    // 5 faz debounce antes de perguntar.
    ConsultaDeCadastroPorIp,
    RecuperacaoPorEmail,

    // Alto de propósito: a banca inteira entra pelo mesmo celular roteado.
    RegistroDeDemonstracaoPorIp,

    // Por pedido de assinatura, que cobre as duas derivadas de uma foto.
    UploadPorUsuario,
}

internal sealed record RegraDeLimite(int Permitidos, TimeSpan Janela);

internal sealed partial class LimitesDeTaxa : IDisposable
{
    // Chave mais longa que isto é truncada: o corpo ainda não passou pela validação.
    private const int _tamanhoMaximoDaChave = 256;

    private readonly Dictionary<Limite, PartitionedRateLimiter<string>> _limitadores;
    private readonly ILogger<LimitesDeTaxa> _logger;

    public LimitesDeTaxa(ILogger<LimitesDeTaxa> logger)
    {
        _logger = logger;
        _limitadores = Regras.ToDictionary(regra => regra.Key, regra => Criar(regra.Value));
    }

    public static IReadOnlyDictionary<Limite, RegraDeLimite> Regras { get; } = new Dictionary<Limite, RegraDeLimite>
    {
        [Limite.LoginPorIdentificador] = new(5, TimeSpan.FromMinutes(1)),
        [Limite.LoginPorIp] = new(30, TimeSpan.FromMinutes(1)),
        [Limite.CadastroPorRa] = new(3, TimeSpan.FromMinutes(15)),
        [Limite.ConsultaDeCadastroPorIp] = new(60, TimeSpan.FromMinutes(1)),
        [Limite.RecuperacaoPorEmail] = new(3, TimeSpan.FromMinutes(15)),
        [Limite.RegistroDeDemonstracaoPorIp] = new(60, TimeSpan.FromHours(1)),

        // Revisto no passo 7: um post ou uma foto de perfil por pedido, 30 por hora sobram.
        [Limite.UploadPorUsuario] = new(30, TimeSpan.FromHours(1)),
    };

    public RateLimitLease Tentar(Limite limite, string chave)
    {
        var lease = _limitadores[limite].AttemptAcquire(chave.Length > _tamanhoMaximoDaChave ? chave[.._tamanhoMaximoDaChave] : chave);
        if (!lease.IsAcquired)
        {
            // A chave (e-mail, RA, identificador) é dado pessoal: o log leva só o limite.
            LogEstourado(_logger, limite);
        }

        return lease;
    }

    public void Dispose()
    {
        foreach (var limitador in _limitadores.Values)
        {
            limitador.Dispose();
        }
    }

    // Janela fixa, sem fila: estourou, recusa na hora com o tempo até a janela virar.
    private static PartitionedRateLimiter<string> Criar(RegraDeLimite regra) =>
        PartitionedRateLimiter.Create<string, string>(chave => RateLimitPartition.GetFixedWindowLimiter(
            chave,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = regra.Permitidos,
                Window = regra.Janela,
                QueueLimit = 0,
                AutoReplenishment = true,
            }));

    [LoggerMessage(Level = LogLevel.Warning, Message = "Limite de taxa {Limite} estourado.")]
    private static partial void LogEstourado(ILogger logger, Limite limite);
}

internal static class LimiteDeTaxaExtensions
{
    public static IServiceCollection AddLimitesDeTaxa(this IServiceCollection services) =>
        services.AddSingleton<LimitesDeTaxa>();

    // O IP real: o ForwardedHeaders já trocou o do Caddy pelo do X-Forwarded-For, e só
    // quando o pedido veio da rede confiável (ProxyConfiavel).
    public static string Ip(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "sem-ip";
    }

    // Chave nula deixa passar: é corpo inválido, que a validação recusa logo depois.
    public static RouteHandlerBuilder ComLimiteDeTaxa(
        this RouteHandlerBuilder builder, Limite limite, Func<EndpointFilterInvocationContext, string?> chave)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(chave);

        builder.AddEndpointFilter(async (contexto, proximo) =>
        {
            if (chave(contexto) is not { } valor)
            {
                return await proximo(contexto);
            }

            using var lease = contexto.HttpContext.RequestServices.GetRequiredService<LimitesDeTaxa>().Tentar(limite, valor);
            if (lease.IsAcquired)
            {
                return await proximo(contexto);
            }

            var espera = lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) ? retryAfter : LimitesDeTaxa.Regras[limite].Janela;
            contexto.HttpContext.Response.Headers.RetryAfter =
                Math.Max(1, (int)Math.Ceiling(espera.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            return TypedResults.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                extensions: new Dictionary<string, object?> { [Erro.CampoCodigo] = "limite-de-taxa" });
        });

        return builder.ProducesProblem(StatusCodes.Status429TooManyRequests);
    }

    // Do argumento do tipo pedido, já desserializado.
    public static string? Do<TPedido>(EndpointFilterInvocationContext contexto, Func<TPedido, string?> campo)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(campo);
        return contexto.Arguments.OfType<TPedido>().FirstOrDefault() is { } pedido
            ? campo(pedido)?.Trim().ToLowerInvariant()
            : null;
    }
}
