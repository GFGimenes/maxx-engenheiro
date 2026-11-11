using System.Security.Cryptography;
using Maxx.Application.Autenticacao;
using Maxx.Application.Autenticacao.Cadastro;
using Maxx.Application.Autenticacao.Login;
using Maxx.Application.Autenticacao.Recuperacao;
using Maxx.Application.Autenticacao.Termos;
using Maxx.Domain.Identidade;
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
        builder.Services.AddScoped<TermoDeUsoHandler>();
        builder.Services.AddSingleton(LerDominiosAceitos(builder));
        builder.Services.AddScoped<CadastroHandler>();
        builder.Services.AddScoped<LoginHandler>();
        builder.Services.AddScoped<RefreshHandler>();
        builder.Services.AddScoped<LogoutHandler>();
        builder.Services.AddScoped<RecuperacaoHandler>();
        builder.AddEnvioDeEmail(exigirChave);

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

                // O access token vive 15 min sem consultar o banco. O da conta de demonstração é
                // a exceção (passo 14, P1): a purga apaga o aluno e encerra o professor e a
                // coordenação do lote, e o token que sobrou não pode seguir agindo. Uma consulta
                // pela chave primária, só nesses pedidos.
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async contexto =>
                    {
                        if (contexto.Principal?.FindFirst(MaxxClaims.Demonstracao) is null)
                        {
                            return;
                        }

                        var conta = Guid.TryParse(contexto.Principal.FindFirst(MaxxClaims.Sub)?.Value, out var id)
                            ? await contexto.HttpContext.RequestServices.GetRequiredService<IUsuarioRepository>()
                                .BuscarAsync(id, contexto.HttpContext.RequestAborted)
                            : null;
                        if (conta is null || conta.EhEncerrada)
                        {
                            contexto.Fail("Conta de demonstração apagada ou encerrada pela purga.");
                        }
                    },
                };
            });
        builder.Services.AddAuthorization(Politicas.Configurar);

        return builder;
    }

    // O que o handler do e-mail de recuperação precisa da borda: SMTP e URL pública do app.
    // Na API e no worker: quem executa o trabalho é o worker, e a API e os testes de
    // integração montam os mesmos tipos de trabalho.
    public static IHostApplicationBuilder AddEnvioDeEmail(this IHostApplicationBuilder builder, bool exigirUrl)
    {
        builder.Services.AddSingleton<ILinkDoApp>(new LinkDoApp(LerUrlPublica(builder, exigirUrl)));
        builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.Section));
        return builder;
    }

    // Sem ela o link do e-mail de recuperação não teria para onde apontar: fora de
    // desenvolvimento nem a API nem o worker sobem.
    // Os domínios de e-mail aceitos no cadastro e na correção pela instituição (produto.md
    // 2.4), em Cadastro:DominiosDeEmailAceitos do appsettings.json: não é segredo, e a lista
    // é a mesma em todo ambiente. Vazia ou mal formada, nada sobe.
    private static DominiosDeEmailAceitos LerDominiosAceitos(IHostApplicationBuilder builder) =>
        new(builder.Configuration.GetSection("Cadastro:DominiosDeEmailAceitos").Get<string[]>() ?? []);

    private static Uri LerUrlPublica(IHostApplicationBuilder builder, bool exigir)
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
