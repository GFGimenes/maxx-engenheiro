using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maxx.Infrastructure.Persistence.Configurations;

// Os registros dos atos da instituição sobre a linha do aluno (passo 12, P5). Saem em
// cascata com a linha, que só a purga da demonstração apaga; quem fez segura a conta.
internal sealed class CorrecaoDeEmailConfiguration : IEntityTypeConfiguration<CorrecaoDeEmail>
{
    public void Configure(EntityTypeBuilder<CorrecaoDeEmail> builder)
    {
        builder.ToTable("correcao_de_email");
        builder.HasKey(correcao => correcao.Id);
        builder.Property(correcao => correcao.Id).ValueGeneratedNever();
        builder.Property(correcao => correcao.EmailAnterior).HasColumnType("citext").IsRequired();
        builder.Property(correcao => correcao.EmailNovo).HasColumnType("citext").IsRequired();
        builder.HasIndex(correcao => correcao.CadastroElegivelId);

        builder.HasOne<CadastroElegivel>()
            .WithMany()
            .HasForeignKey(correcao => correcao.CadastroElegivelId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(correcao => correcao.CorrigidoPorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TrocaDeCursoConfiguration : IEntityTypeConfiguration<TrocaDeCurso>
{
    public void Configure(EntityTypeBuilder<TrocaDeCurso> builder)
    {
        builder.ToTable("troca_de_curso", table =>
        {
            table.HasCheckConstraint("ck_troca_de_curso_cursos", "curso_anterior_id <> curso_novo_id");
            table.HasCheckConstraint("ck_troca_de_curso_semestres", "semestre_anterior >= 1 AND semestre_novo >= 1");
        });
        builder.HasKey(troca => troca.Id);
        builder.Property(troca => troca.Id).ValueGeneratedNever();
        builder.HasIndex(troca => troca.CadastroElegivelId);

        builder.HasOne<CadastroElegivel>()
            .WithMany()
            .HasForeignKey(troca => troca.CadastroElegivelId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Curso>().WithMany().HasForeignKey(troca => troca.CursoAnteriorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Curso>().WithMany().HasForeignKey(troca => troca.CursoNovoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(troca => troca.TrocadoPorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
