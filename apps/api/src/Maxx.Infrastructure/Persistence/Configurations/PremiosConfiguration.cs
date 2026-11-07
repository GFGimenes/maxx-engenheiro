using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maxx.Infrastructure.Persistence.Configurations;

// A medalha concedida. Nada apaga em cascata: a medalha segura aluno, professor, turma e
// item, e sai só pela purga da demonstração, antes do usuário.
internal sealed class MedalhaConfiguration : IEntityTypeConfiguration<Medalha>
{
    public void Configure(EntityTypeBuilder<Medalha> builder)
    {
        builder.ToTable("medalha", table =>
        {
            table.HasCheckConstraint("ck_medalha_pontos", "pontos >= 1");
            table.HasCheckConstraint("ck_medalha_ocorrencia", "ocorrencia >= 1");
            table.HasCheckConstraint("ck_medalha_versao_do_item", "versao_do_item >= 1");

            // Anulação grava quem e quando, sempre juntos.
            table.HasCheckConstraint(
                "ck_medalha_anulacao", "(anulada_em IS NULL) = (anulada_por_id IS NULL)");
        });
        builder.HasKey(medalha => medalha.Id);
        builder.Property(medalha => medalha.Id).ValueGeneratedNever();

        builder.Property(medalha => medalha.ItemCodigo).IsRequired();
        builder.Property(medalha => medalha.Tier)
            .HasConversion(tier => NomeJson.De(tier), texto => Enum.Parse<Tier>(texto, ignoreCase: true));
        builder.Property(medalha => medalha.ChaveDeDeduplicacao).IsRequired();

        // A vaga do período: uma medalha ativa por chave. Decide a corrida de duas
        // concessões; a anulada sai do índice e libera a vaga.
        builder.HasIndex(medalha => medalha.ChaveDeDeduplicacao)
            .HasDatabaseName("ix_medalha_chave_ativa")
            .IsUnique()
            .HasFilter("anulada_em IS NULL");

        // As medalhas do aluno num item, que a concessão lê para achar a vaga.
        builder.HasIndex(medalha => new { medalha.AlunoId, medalha.ItemCodigo });
        builder.HasIndex(medalha => medalha.TurmaId);

        builder.HasOne<Usuario>().WithMany().HasForeignKey(medalha => medalha.AlunoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(medalha => medalha.ConcedidaPorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(medalha => medalha.AnuladaPorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ItemCatalogoRow>().WithMany().HasForeignKey(medalha => medalha.ItemCodigo).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Turma>().WithMany().HasForeignKey(medalha => medalha.TurmaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Materia>().WithMany().HasForeignKey(medalha => medalha.MateriaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SemestreLetivo>().WithMany().HasForeignKey(medalha => medalha.SemestreLetivoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Bimestre>().WithMany().HasForeignKey(medalha => medalha.BimestreId).OnDelete(DeleteBehavior.Restrict);

        // Duas anulações da mesma medalha ao mesmo tempo: o xmin deixa só a primeira gravar.
        builder.Property<uint>("Versao").IsRowVersion();

        builder.Ignore(medalha => medalha.EstaAtiva);
    }
}
