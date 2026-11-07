using Maxx.Application;
using Maxx.Application.Autenticacao;
using Maxx.Application.Autenticacao.Recuperacao;
using Maxx.Application.Autorizacao;
using Maxx.Application.Demonstracao;
using Maxx.Application.Fila;
using Maxx.Application.Medalhas;
using Maxx.Application.Notificacoes;
using Maxx.Application.Premios;
using Maxx.Infrastructure.Autenticacao;
using Maxx.Infrastructure.Autorizacao;
using Maxx.Infrastructure.Demonstracao;
using Maxx.Infrastructure.Email;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Medalhas;
using Maxx.Infrastructure.Notificacoes;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Premios;
using Maxx.Infrastructure.RaridadeDosItens;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Maxx.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddNpgsqlDataSource(connectionString);

        services.AddDbContext<MaxxDbContext>((provider, options) =>
            MaxxDbContext.Configure(options, builder => builder.UseNpgsql(
                provider.GetRequiredService<NpgsqlDataSource>(),
                npgsql => npgsql.MigrationsHistoryTable(MaxxDbContext.MigrationsHistoryTable))));

        services.AddScoped<DatabaseMigrator>();
        services.AddScoped<DatabaseSeeder>();

        services.AddSingleton<ISenhaHasher, Argon2idSenhaHasher>();
        services.AddSingleton<ITokenGenerator, TokenGenerator>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICadastroElegivelRepository, CadastroElegivelRepository>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<ITermoDeUsoRepository, TermoDeUsoRepository>();
        services.AddScoped<IFamiliaDeSessaoRepository, FamiliaDeSessaoRepository>();
        services.AddScoped<IEscopoDeTurma, EscopoDeTurma>();
        services.AddScoped<IRecuperacaoDeSenhaRepository, RecuperacaoDeSenhaRepository>();
        services.AddScoped<IDemonstracaoRepository, DemonstracaoRepository>();
        services.AddScoped<IMedalhaRepository, MedalhaRepository>();
        services.AddScoped<IConsultaDoProfessor, ConsultaDoProfessor>();
        services.AddScoped<INotificacaoRepository, NotificacaoRepository>();
        services.AddScoped<IConsultaDePremios, ConsultaDePremios>();

        // Fila no Postgres (ADR 0008): a API e o worker enfileiram; só o worker consome.
        services.AddOptions<FilaOptions>();
        services.AddScoped<IFilaDeTrabalhos, FilaDeTrabalhos>();
        services.AddSingleton<TiposDeTrabalho>();
        services.AddSingleton<ConsumidorDeTrabalhos>();

        // Os tipos de trabalho. O e-mail sai só por eles, no worker; as opções (EmailOptions)
        // e o link do app (ILinkDoApp) vêm da borda.
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddTrabalho<EmailDeRecuperacao, EmailDeRecuperacaoHandler>();
        services.AddTrabalho<NotificarMedalhaRecebida, NotificarMedalhaRecebidaHandler>();
        services.AddTrabalho<RecalcularRaridade, RecalculoDeRaridade>();

        // Toda verificação registrada aqui entra no /ready.
        services.AddHealthChecks()
            .AddCheck<DatabaseReadinessCheck>("database", timeout: TimeSpan.FromSeconds(3));

        return services;
    }
}
