using Maxx.Domain.Conquistas;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Domain.Social;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maxx.Infrastructure.Persistence.Configurations;

// O post (produto.md 6.1). As regras de forma do domínio repetidas no banco, como última
// barreira: post vazio não existe, a foto são sempre as duas derivadas, a remoção pela
// instituição grava quem e quando juntos.
internal sealed class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("post", table =>
        {
            table.HasCheckConstraint(
                "ck_post_nao_vazio",
                "legenda IS NOT NULL OR chave_da_foto_do_feed IS NOT NULL OR medalha_id IS NOT NULL OR premio_participacao_id IS NOT NULL" +
                " OR conquista_id IS NOT NULL");
            table.HasCheckConstraint(
                "ck_post_um_premio", "num_nonnulls(medalha_id, premio_participacao_id, conquista_id) <= 1");
            table.HasCheckConstraint(
                "ck_post_foto_completa", "(chave_da_foto_do_feed IS NULL) = (chave_da_foto_da_grade IS NULL)");
            table.HasCheckConstraint("ck_post_remocao", "(removido_em IS NULL) = (removido_por_id IS NULL)");
        });
        builder.HasKey(post => post.Id);
        builder.Property(post => post.Id).ValueGeneratedNever();

        // Um prêmio rende um post. O removido pela instituição sai do índice: o aluno pode
        // postar o prêmio de novo, como depois de apagar.
        builder.HasIndex(post => post.MedalhaId)
            .HasDatabaseName("ix_post_medalha_ativa")
            .IsUnique()
            .HasFilter("medalha_id IS NOT NULL AND removido_em IS NULL");
        builder.HasIndex(post => post.PremioParticipacaoId)
            .HasDatabaseName("ix_post_premio_participacao_ativo")
            .IsUnique()
            .HasFilter("premio_participacao_id IS NOT NULL AND removido_em IS NULL");
        builder.HasIndex(post => post.ConquistaId)
            .HasDatabaseName("ix_post_conquista_ativa")
            .IsUnique()
            .HasFilter("conquista_id IS NOT NULL AND removido_em IS NULL");

        // Os posts de um autor, do mais novo ao mais antigo: o perfil e o trecho de seguidos
        // do feed (passo 7, etapas 5 e 6) andam por este índice.
        builder.HasIndex(post => new { post.AutorId, post.CriadoEm, post.Id })
            .IsDescending(false, true, true);

        builder.HasOne<Usuario>().WithMany().HasForeignKey(post => post.AutorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(post => post.RemovidoPorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Medalha>().WithMany().HasForeignKey(post => post.MedalhaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PremioParticipacao>().WithMany().HasForeignKey(post => post.PremioParticipacaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Conquista>().WithMany().HasForeignKey(post => post.ConquistaId).OnDelete(DeleteBehavior.Restrict);

        // Duas remoções, ou remoção e anulação, ao mesmo tempo: o xmin deixa só a primeira
        // gravar.
        builder.Property<uint>("Versao").IsRowVersion();

        builder.Ignore(post => post.TemFoto);
        builder.Ignore(post => post.TemLegenda);
        builder.Ignore(post => post.TemPremio);
        builder.Ignore(post => post.EstaRemovido);
        builder.Ignore(post => post.ChavesDaFoto);
        builder.Ignore(post => post.FicariaVazioSemPremio);
    }
}
