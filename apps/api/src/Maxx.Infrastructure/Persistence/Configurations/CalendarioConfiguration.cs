using Maxx.Domain.Academico;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maxx.Infrastructure.Persistence.Configurations;

// Ano, semestre e bimestre são um agregado: o ano é dono dos semestres, o semestre dos
// bimestres. As chaves para o dono são de sombra, porque o domínio não as precisa.
internal sealed class AnoLetivoConfiguration : IEntityTypeConfiguration<AnoLetivo>
{
    public void Configure(EntityTypeBuilder<AnoLetivo> builder)
    {
        builder.ToTable("ano_letivo");
        builder.HasKey(ano => ano.Id);
        builder.Property(ano => ano.Id).ValueGeneratedNever();
        builder.HasIndex(ano => ano.Ano).IsUnique();

        builder.HasMany(ano => ano.Semestres)
            .WithOne()
            .HasForeignKey("AnoLetivoId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(ano => ano.Semestres).HasField("_semestres");

        // Atalho de leitura sobre os semestres, não uma relação própria.
        builder.Ignore(ano => ano.Bimestres);
    }
}

internal sealed class SemestreLetivoConfiguration : IEntityTypeConfiguration<SemestreLetivo>
{
    public void Configure(EntityTypeBuilder<SemestreLetivo> builder)
    {
        builder.ToTable("semestre_letivo", table =>
            table.HasCheckConstraint("ck_semestre_letivo_numero", "numero IN (1, 2)"));
        builder.HasKey(semestre => semestre.Id);
        builder.Property(semestre => semestre.Id).ValueGeneratedNever();
        builder.HasIndex(semestre => new { semestre.Ano, semestre.Numero }).IsUnique();

        builder.HasMany(semestre => semestre.Bimestres)
            .WithOne()
            .HasForeignKey("SemestreLetivoId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(semestre => semestre.Bimestres).HasField("_bimestres");
    }
}

internal sealed class BimestreConfiguration : IEntityTypeConfiguration<Bimestre>
{
    public void Configure(EntityTypeBuilder<Bimestre> builder)
    {
        builder.ToTable("bimestre", table =>
        {
            table.HasCheckConstraint("ck_bimestre_numero", "numero BETWEEN 1 AND 4");
            table.HasCheckConstraint("ck_bimestre_fim_depois_do_inicio", "fim >= inicio");
        });
        builder.HasKey(bimestre => bimestre.Id);
        builder.Property(bimestre => bimestre.Id).ValueGeneratedNever();
        builder.HasIndex("SemestreLetivoId", nameof(Bimestre.Numero)).IsUnique();
    }
}
