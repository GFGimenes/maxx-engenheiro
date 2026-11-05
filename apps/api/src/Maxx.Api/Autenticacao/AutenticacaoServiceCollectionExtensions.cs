using System.Security.Cryptography;
using Maxx.Application.Autenticacao;
using Maxx.Application.Autenticacao.Cadastro;
using Maxx.Application.Autenticacao.Login;
using Maxx.Application.Autenticacao.Recuperacao;
using Maxx.Infrastructure.Email;
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
        builder.Services.AddScoped<SessaoService>();
        builder.Services.AddScoped<SugestaoDeArroba>();
        builder.Services.AddScoped<CadastroHandler>();
        builder.Services.AddScoped<LoginHandler>();
        builder.Services.AddScoped<RefreshHandler>();
        builder.Services.AddScoped<LogoutHandler>();
        builder.Services.AddScoped<RecuperacaoHandler>();
        builder.Services.AddSingleton<ILinkDoApp>(new LinkDoApp(LerUrlPublica(builder, exigirChave)));
        builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.Section));

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
        builder.Services.AddAuthorization(Politicas.Configurar);

        return builder;
    }

    // Sem ela o link do e-mail de recuperação não teria para onde apontar: fora de
    // desenvolvimento a API não sobe.
    private static Uri LerUrlPublica(WebApplicationBuilder builder, bool exigir)
    {
        var texto = builder.Configuration[$"{LinkDoApp.Section}:UrlPublica"];
        if (string.IsNullOrWhiteSpace(texto))
        {
            if (exigir && !builder.Environment.IsDevelopment())
            {
                throw new InvalidOperationException($"{LinkDoApp.Section}__UrlPublica não configurada: defina a URL pública do app.");
            }

            texto = "http://localhost";
        }

        if (!Uri.TryCreate(texto.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException($"{LinkDoApp.Section}__UrlPublica inválida: {texto}.");
        }

        return url;
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
