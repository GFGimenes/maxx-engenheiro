using Maxx.Infrastructure.Fotos;

namespace Maxx.Api.Fotos;

internal static class ArmazenamentoServiceCollectionExtensions
{
    // Na API e no worker: a API assina e confere, o worker remove o que ficou sem dono.
    // exigir é falso só nos subcomandos e na geração do OpenAPI, que não tocam no bucket.
    // Fora de desenvolvimento, sem a configuração completa nenhum dos dois sobe; em
    // desenvolvimento sobe, e só o que depende do bucket falha.
    public static IHostApplicationBuilder AddArmazenamentoDeFotos(this IHostApplicationBuilder builder, bool exigir)
    {
        var secao = builder.Configuration.GetSection(ArmazenamentoOptions.Section);
        builder.Services.Configure<ArmazenamentoOptions>(secao);

        if (exigir && !builder.Environment.IsDevelopment()
            && (secao.Get<ArmazenamentoOptions>() ?? new ArmazenamentoOptions()).Faltando() is { Count: > 0 } faltando)
        {
            throw new InvalidOperationException($"Armazenamento de fotos não configurado: defina {string.Join(", ", faltando)}.");
        }

        return builder;
    }
}
