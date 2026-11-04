using System.Security.Cryptography;
using Maxx.Application.Autenticacao;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Maxx.Api.Autenticacao;

internal static class AutenticacaoServiceCollectionExtensions
{
    // exigirChave é falso só nos subcomandos (migrate, seed) e na geração do OpenAPI, que
    // não servem requisição. Fora de desenvolvimento, sem chave válida a API não sobe.
    public static WebApplicationBuilder AddAutenticacao(this WebApplicationBuilder builder, bool exigirChave)
    {
        var chave = LerChave(builder, exigirChave);
        builder.Services.AddSingleton(new ChaveDeAssinatura(chave));
        builder.Services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Claims com o nome que vieram (sub, papel), sem o mapeamento para os URIs
                // do WS-Federation.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = JwtOptions.Emissor,
                    ValidAudience = JwtOptions.Audiencia,
                    IssuerSigningKey = chave,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = JwtOptions.Tolerancia,
                    NameClaimType = MaxxClaims.Sub,
                    RoleClaimType = MaxxClaims.Papel,
                };
            });
        builder.Services.AddAuthorization();

        return builder;
    }

    private static SymmetricSecurityKey LerChave(WebApplicationBuilder builder, bool exigirChave)
    {
        var texto = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>()?.Chave;

        if (string.IsNullOrWhiteSpace(texto))
        {
            if (exigirChave && !builder.Environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"{JwtOptions.Section}__Chave não configurada: defina uma chave base64 de ao menos {JwtOptions.TamanhoMinimoDaChaveEmBytes} bytes.");
            }

            // Desenvolvimento sem chave: uma por processo, que some no restart. Nada
            // versionado, nem a chave de desenvolvimento.
            return new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(JwtOptions.TamanhoMinimoDaChaveEmBytes));
        }

        var bytes = new byte[texto.Length];
        if (!Convert.TryFromBase64String(texto.Trim(), bytes, out var escritos) || escritos < JwtOptions.TamanhoMinimoDaChaveEmBytes)
        {
            throw new InvalidOperationException(
                $"{JwtOptions.Section}__Chave inválida: tem de ser base64 de ao menos {JwtOptions.TamanhoMinimoDaChaveEmBytes} bytes.");
        }

        return new SymmetricSecurityKey(bytes[..escritos]);
    }
}
