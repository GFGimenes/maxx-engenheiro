using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Domain.Social;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maxx.Infrastructure.Persistence.Configurations;

// O prêmio de participação. Nada apaga em cascata: o prêmio segura aluno, item e evento, e
// sai só pela purga da demonstração, antes do usuário.
internal sealed class PremioParticipacaoConfiguration : IEntityTypeConfiguration<PremioParticipacao>
{
    public void Configure(EntityTypeBuilder<PremioParticipacao> builder)
    {
        builder.ToTable("premio_participacao", table =>
        {
            table.HasCheckConstraint("ck_premio_participacao_pontos", "pontos >= 1");
            table.HasCheckConstraint("ck_premio_participacao_versao_do_item", "versao_do_item >= 1");
        });
        builder.HasKey(premio => premio.Id);
        builder.Property(premio => premio.Id).ValueGeneratedNever();

        builder.Property(premio => premio.ItemCodigo).IsRequired();
        builder.Property(premio => premio.Tier)
            .HasConversion(tier => NomeJson.De(tier), texto => Enum.Parse<Tier>(texto, ignoreCase: true));
        builder.Property(premio => premio.Estado)
            .HasConversion(estado => NomeJson.De(estado), texto => Enum.Parse<EstadoDoPremioParticipacao>(texto, ignoreCase: true));
        builder.Property(premio => premio.ChaveDeDeduplicacao).IsRequired();

        // Um prêmio por evento (produto.md 4.6): uma chave entre os que não foram reprovados.
        // Decide a corrida de duas reivindicações; o reprovado sai e libera a nova tentativa.
        builder.HasIndex(premio => premio.ChaveDeDeduplicacao)
            .HasDatabaseName("ix_premio_participacao_chave_ativa")
            .IsUnique()
            .HasFilter("estado <> 'reprovado'");

        builder.HasIndex(premio => new { premio.AlunoId, premio.ItemCodigo });

        builder.HasOne<Usuario>().WithMany().HasForeignKey(premio => premio.AlunoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ItemCatalogoRow>().WithMany().HasForeignKey(premio => premio.ItemCodigo).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Evento>().WithMany().HasForeignKey(premio => premio.EventoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Materia>().WithMany().HasForeignKey(premio => premio.MateriaId).OnDelete(DeleteBehavior.Restrict);

        // Duas decisões ao mesmo tempo (passo 8, etapas 3 e 4): o xmin deixa só a primeira gravar.
        builder.Property<uint>("Versao").IsRowVersion();

        builder.Ignore(premio => premio.SeguraAChave);
        builder.Ignore(premio => premio.Pontua);
        builder.Ignore(premio => premio.PrendeOPost);
    }
}

// A comprovação: uma por prêmio, filtrável por evento e lida das mais antigas para as mais
// novas (fila de comprovações, etapa 3). O post sai só quando já não prende a foto, e aí a
// comprovação fica sem ele.
internal sealed class ComprovacaoConfiguration : IEntityTypeConfiguration<Comprovacao>
{
    public void Configure(EntityTypeBuilder<Comprovacao> builder)
    {
        builder.ToTable("comprovacao");
        builder.HasKey(comprovacao => comprovacao.Id);
        builder.Property(comprovacao => comprovacao.Id).ValueGeneratedNever();

        builder.HasIndex(comprovacao => comprovacao.PremioParticipacaoId).IsUnique();
        builder.HasIndex(comprovacao => new { comprovacao.EventoId, comprovacao.CriadaEm, comprovacao.Id });
        builder.HasIndex(comprovacao => new { comprovacao.CriadaEm, comprovacao.Id });
        builder.HasIndex(comprovacao => comprovacao.PostId);

        builder.HasOne<PremioParticipacao>().WithMany().HasForeignKey(comprovacao => comprovacao.PremioParticipacaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(comprovacao => comprovacao.AlunoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Evento>().WithMany().HasForeignKey(comprovacao => comprovacao.EventoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Post>().WithMany().HasForeignKey(comprovacao => comprovacao.PostId).OnDelete(DeleteBehavior.SetNull);
    }
}
