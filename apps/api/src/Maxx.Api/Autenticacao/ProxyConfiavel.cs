using Microsoft.AspNetCore.HttpOverrides;

namespace Maxx.Api.Autenticacao;

// X-Forwarded-For e X-Forwarded-Proto aceitos só de quem está na rede do Caddy. Sem a
// configuração, só do loopback (o padrão do ASP.NET Core): um cliente qualquer nunca
// escolhe o IP que o limite de taxa enxerga.
internal static class ProxyConfiavel
{
    public const string Section = "Proxy";

    public static IServiceCollection AddProxyConfiavel(this IServiceCollection services, IConfiguration configuration)
    {
        var redes = configuration.GetSection($"{Section}:RedesConfiaveis").Get<string[]>() ?? [];

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Um salto só: o Caddy. Ele acrescenta o IP de quem o chamou.
            options.ForwardLimit = 1;
            foreach (var rede in redes)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(rede));
            }
        });

        return services;
    }
}
