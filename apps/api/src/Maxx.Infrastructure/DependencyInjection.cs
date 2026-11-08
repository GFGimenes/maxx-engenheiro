using Maxx.Application;
using Maxx.Application.Autenticacao;
using Maxx.Application.Autenticacao.Recuperacao;
using Maxx.Application.Autorizacao;
using Maxx.Application.Conquistas;
using Maxx.Application.Demonstracao;
using Maxx.Application.Feed;
using Maxx.Application.Fila;
using Maxx.Application.Fotos;
using Maxx.Application.Medalhas;
using Maxx.Application.Notificacoes;
using Maxx.Application.Perfil;
using Maxx.Application.Posts;
using Maxx.Application.Premios;
using Maxx.Application.PremiosParticipacao;
using Maxx.Application.Social;
using Maxx.Domain.Conquistas;
using Maxx.Infrastructure.Autenticacao;
using Maxx.Infrastructure.Autorizacao;
using Maxx.Infrastructure.Conquistas;
using Maxx.Infrastructure.Demonstracao;
using Maxx.Infrastructure.Email;
using Maxx.Infrastructure.Feed;
using Maxx.Infrastructure.Fila;
using Maxx.Infrastructure.Fotos;
using Maxx.Infrastructure.Medalhas;
using Maxx.Infrastructure.Notificacoes;
using Maxx.Infrastructure.Perfil;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Posts;
using Maxx.Infrastructure.Premios;
using Maxx.Infrastructure.PremiosParticipacao;
using Maxx.Infrastructure.RaridadeDosItens;
using Maxx.Infrastructure.Seed;
using Maxx.Infrastructure.Social;
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
        services.AddScoped<IEventoRepository, EventoRepository>();
        services.AddScoped<IPremioParticipacaoRepository, PremioParticipacaoRepository>();
        services.AddScoped<IConsultaDeComprovacoes, ConsultaDeComprovacoes>();
        services.AddScoped<IConsultaDeContestacoes, ConsultaDeContestacoes>();
        services.AddScoped<IConquistaRepository, ConquistaRepository>();
        services.AddScoped<ContextoDasConquistas>();

        // O avaliador único das conquistas (ADR 0004), com as regras customizadas registradas.
        // Nenhuma conquista da fase 1 usa o escape, e nenhuma está registrada.
        services.AddSingleton(servicos => new AvaliadorDeConquista(servicos.GetServices<IRegraCustomizada>()));

        // O bucket das fotos (ADR 0002). As opções vêm da borda, que recusa subir sem elas
        // fora de desenvolvimento.
        services.AddSingleton<IArmazenamentoDeFotos, ArmazenamentoS3>();
        services.AddScoped<IUploadPendenteRepository, UploadPendenteRepository>();
        services.AddScoped<IPostRepository, PostRepository>();
        services.AddScoped<IConsultaDePosts, ConsultaDePosts>();
        services.AddScoped<ICurtidaRepository, CurtidaRepository>();
        services.AddScoped<IComentarioRepository, ComentarioRepository>();
        services.AddScoped<ISeguimentoRepository, SeguimentoRepository>();
        services.AddScoped<IConsultaSocial, ConsultaSocial>();
        services.AddScoped<IConsultaDoFeed, ConsultaDoFeed>();
        services.AddScoped<IConsultaDePerfil, ConsultaDePerfil>();

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
        services.AddTrabalho<LimparUploadAbandonado, LimparUploadAbandonadoHandler>();
        services.AddTrabalho<RemoverFotos, RemoverFotosHandler>();
        services.AddTrabalho<NotificarComprovacaoAprovada, NotificarComprovacaoHandler>();
        services.AddTrabalho<NotificarComprovacaoReprovada, NotificarComprovacaoHandler>();
        services.AddTrabalho<TornarPremioDefinitivo, TornarPremioDefinitivoHandler>();
        services.AddTrabalho<NotificarContestacaoAberta, NotificarContestacaoHandler>();
        services.AddTrabalho<NotificarDecisaoDeContestacao, NotificarContestacaoHandler>();
        services.AddTrabalho<NotificarPremioRevogado, NotificarContestacaoHandler>();
        services.AddTrabalho<ReavaliarConquistas, ReavaliarConquistasHandler>();
        services.AddTrabalho<RecalcularConquistasDaMedalha, RecalcularConquistasDaMedalhaHandler>();

        // Toda verificação registrada aqui entra no /ready.
        services.AddHealthChecks()
            .AddCheck<DatabaseReadinessCheck>("database", timeout: TimeSpan.FromSeconds(3));

        return services;
    }
}
