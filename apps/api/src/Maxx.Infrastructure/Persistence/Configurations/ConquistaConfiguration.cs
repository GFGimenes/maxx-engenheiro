using Maxx.Domain.Catalogo;
using Maxx.Domain.Conquistas;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Infrastructure.Conquistas;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maxx.Infrastructure.Persistence.Configurations;

// A conquista concedida pelo worker. A remoção é lógica (D3 do passo 9): a linha fica, com
// removida_em, e sai do índice de unicidade, o que deixa a mesma instância ganhar outra se o
// critério voltar a ser satisfeito. Sai de vez só pela purga da demonstração.
internal sealed class ConquistaConfiguration : IEntityTypeConfiguration<Conquista>
{
    internal static readonly string PeriodosDeConquista =
        string.Join(", ", new[] { Periodo.Bimestre, Periodo.Semestre, Periodo.Ano, Periodo.Curso }.Select(periodo => $"'{NomeJson.De(periodo)}'"));

    public void Configure(EntityTypeBuilder<Conquista> builder)
    {
        builder.ToTable("conquista", table =>
        {
            table.HasCheckConstraint("ck_conquista_pontos", "pontos >= 1");
            table.HasCheckConstraint("ck_conquista_versao_do_item", "versao_do_item >= 1");
            table.HasCheckConstraint("ck_conquista_tipo_de_periodo", $"tipo_de_periodo IN ({PeriodosDeConquista})");
        });
        builder.HasKey(conquista => conquista.Id);
        builder.Property(conquista => conquista.Id).ValueGeneratedNever();

        builder.Property(conquista => conquista.ItemCodigo).IsRequired();
        builder.Property(conquista => conquista.Tier)
            .HasConversion(tier => NomeJson.De(tier), texto => Enum.Parse<Tier>(texto, ignoreCase: true));
        builder.Property(conquista => conquista.TipoDePeriodo)
            .HasConversion(periodo => NomeJson.De(periodo), texto => Enum.Parse<Periodo>(texto, ignoreCase: true));

        // Uma conquista não removida por aluno, item e instância do período
        // (regras-conquista.md 4). A trava por aluno no worker já serializa a avaliação; o
        // índice é a rede de segurança.
        builder.HasIndex(conquista => new { conquista.AlunoId, conquista.ItemCodigo, conquista.TipoDePeriodo, conquista.ReferenciaDoPeriodo })
            .HasDatabaseName("ix_conquista_instancia_ativa")
            .IsUnique()
            .HasFilter("removida_em IS NULL");

        builder.HasMany(conquista => conquista.Evidencias)
            .WithOne()
            .HasForeignKey(evidencia => evidencia.ConquistaId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(conquista => conquista.Evidencias).HasField("_evidencias");

        builder.HasOne<Usuario>().WithMany().HasForeignKey(conquista => conquista.AlunoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ItemCatalogoRow>().WithMany().HasForeignKey(conquista => conquista.ItemCodigo).OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(conquista => conquista.EstaAtiva);
        builder.Ignore(conquista => conquista.Instancia);
    }
}

// Um prêmio que compôs a conquista. A medalha e o prêmio não apagam em cascata: a evidência
// sai com a conquista. A anulação procura as conquistas pela medalha (passo 9, etapa 3).
internal sealed class EvidenciaDaConquistaConfiguration : IEntityTypeConfiguration<EvidenciaDaConquista>
{
    public void Configure(EntityTypeBuilder<EvidenciaDaConquista> builder)
    {
        builder.ToTable("evidencia_da_conquista", table =>
            table.HasCheckConstraint(
                "ck_evidencia_da_conquista_um_premio", "(medalha_id IS NULL) <> (premio_participacao_id IS NULL)"));
        builder.HasKey(evidencia => evidencia.Id);
        builder.Property(evidencia => evidencia.Id).ValueGeneratedNever();

        builder.HasIndex(evidencia => evidencia.MedalhaId);
        builder.HasIndex(evidencia => evidencia.PremioParticipacaoId);

        builder.HasOne<Medalha>().WithMany().HasForeignKey(evidencia => evidencia.MedalhaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PremioParticipacao>().WithMany().HasForeignKey(evidencia => evidencia.PremioParticipacaoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(evidencia => evidencia.Origem);
        builder.Ignore(evidencia => evidencia.FatoId);
    }
}

// O progresso de cada conquista por instância, escrito pelo worker (D5 do passo 9). É
// leitura pronta, como a raridade: não é do domínio.
internal sealed class ProgressoDeConquistaConfiguration : IEntityTypeConfiguration<ProgressoDeConquistaRow>
{
    public void Configure(EntityTypeBuilder<ProgressoDeConquistaRow> builder)
    {
        builder.ToTable("progresso_de_conquista", table =>
        {
            table.HasCheckConstraint("ck_progresso_de_conquista_contagem", "contagem >= 0 AND alvo >= 0");
            table.HasCheckConstraint(
                "ck_progresso_de_conquista_tipo_de_periodo", $"tipo_de_periodo IN ({ConquistaConfiguration.PeriodosDeConquista})");
        });
        builder.HasKey(linha => new { linha.AlunoId, linha.ItemCodigo, linha.TipoDePeriodo, linha.ReferenciaDoPeriodo });

        builder.HasOne<Usuario>().WithMany().HasForeignKey(linha => linha.AlunoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ItemCatalogoRow>().WithMany().HasForeignKey(linha => linha.ItemCodigo).OnDelete(DeleteBehavior.Cascade);
    }
}
