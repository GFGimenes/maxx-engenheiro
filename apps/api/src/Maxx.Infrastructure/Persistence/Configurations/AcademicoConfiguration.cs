using Maxx.Domain.Academico;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maxx.Infrastructure.Persistence.Configurations;

// Entre agregados, nada apaga em cascata: matrícula e vínculo seguram turma e cadastro.
internal sealed class CursoConfiguration : IEntityTypeConfiguration<Curso>
{
    public void Configure(EntityTypeBuilder<Curso> builder)
    {
        builder.ToTable("curso", table =>
            table.HasCheckConstraint("ck_curso_duracao_em_semestres", "duracao_em_semestres >= 1"));
        builder.HasKey(curso => curso.Id);
        builder.Property(curso => curso.Id).ValueGeneratedNever();
        builder.Property(curso => curso.Nome).IsRequired();
        builder.Property(curso => curso.Sigla).IsRequired();
        builder.HasIndex(curso => curso.Sigla).IsUnique();
    }
}

internal sealed class MateriaConfiguration : IEntityTypeConfiguration<Materia>
{
    public void Configure(EntityTypeBuilder<Materia> builder)
    {
        builder.ToTable("materia");
        builder.HasKey(materia => materia.Id);
        builder.Property(materia => materia.Id).ValueGeneratedNever();
        builder.Property(materia => materia.Nome).IsRequired();
        builder.HasIndex(materia => new { materia.CursoId, materia.Nome }).IsUnique();

        builder.HasOne<Curso>()
            .WithMany()
            .HasForeignKey(materia => materia.CursoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TurmaConfiguration : IEntityTypeConfiguration<Turma>
{
    public void Configure(EntityTypeBuilder<Turma> builder)
    {
        builder.ToTable("turma");
        builder.HasKey(turma => turma.Id);
        builder.Property(turma => turma.Id).ValueGeneratedNever();

        // Turma é matéria mais semestre letivo: uma só por par.
        builder.HasIndex(turma => new { turma.MateriaId, turma.SemestreLetivoId }).IsUnique();

        builder.HasOne(turma => turma.Materia)
            .WithMany()
            .HasForeignKey(turma => turma.MateriaId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SemestreLetivo>()
            .WithMany()
            .HasForeignKey(turma => turma.SemestreLetivoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CadastroElegivelConfiguration : IEntityTypeConfiguration<CadastroElegivel>
{
    public void Configure(EntityTypeBuilder<CadastroElegivel> builder)
    {
        // Os mesmos invariantes do domínio, de novo no banco: a carga por CSV da fase 2
        // pode não passar pelo domínio.
        builder.ToTable("cadastro_elegivel", table =>
        {
            table.HasCheckConstraint("ck_cadastro_elegivel_papeis", "papeis IN (1, 2, 4, 6)");
            table.HasCheckConstraint(
                "ck_cadastro_elegivel_aluno",
                "(papeis = 1 AND ra IS NOT NULL AND curso_id IS NOT NULL AND semestre >= 1) " +
                "OR (papeis <> 1 AND curso_id IS NULL AND semestre IS NULL)");
        });
        builder.HasKey(cadastro => cadastro.Id);
        builder.Property(cadastro => cadastro.Id).ValueGeneratedNever();
        builder.Property(cadastro => cadastro.Papeis).HasConversion<int>();
        builder.Property(cadastro => cadastro.NomeCompleto).IsRequired();

        // citext: unicidade de e-mail sem diferenciar maiúsculas, guardando como veio.
        builder.Property(cadastro => cadastro.Email).HasColumnType("citext").IsRequired();
        builder.HasIndex(cadastro => cadastro.Email).IsUnique();

        // Um RA, uma linha. Nulo (professor, instituição) não colide no Postgres.
        builder.HasIndex(cadastro => cadastro.Ra).IsUnique();

        builder.HasOne<Curso>()
            .WithMany()
            .HasForeignKey(cadastro => cadastro.CursoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(cadastro => cadastro.EhAluno);
        builder.Ignore(cadastro => cadastro.EhProfessor);
    }
}

internal sealed class MatriculaConfiguration : IEntityTypeConfiguration<Matricula>
{
    public void Configure(EntityTypeBuilder<Matricula> builder)
    {
        builder.ToTable("matricula");
        builder.HasKey(matricula => matricula.Id);
        builder.Property(matricula => matricula.Id).ValueGeneratedNever();
        builder.HasIndex(matricula => new { matricula.AlunoId, matricula.TurmaId }).IsUnique();

        // Índice de turma: "alunos desta turma" é a consulta do professor.
        builder.HasIndex(matricula => matricula.TurmaId);

        builder.HasOne<CadastroElegivel>()
            .WithMany()
            .HasForeignKey(matricula => matricula.AlunoId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Turma>()
            .WithMany()
            .HasForeignKey(matricula => matricula.TurmaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class VinculoProfessorConfiguration : IEntityTypeConfiguration<VinculoProfessor>
{
    public void Configure(EntityTypeBuilder<VinculoProfessor> builder)
    {
        builder.ToTable("vinculo_professor");
        builder.HasKey(vinculo => vinculo.Id);
        builder.Property(vinculo => vinculo.Id).ValueGeneratedNever();

        // O índice que o ADR 0006 pede: escopo de turma checado no banco a cada mutação.
        builder.HasIndex(vinculo => new { vinculo.ProfessorId, vinculo.TurmaId }).IsUnique();
        builder.HasIndex(vinculo => vinculo.TurmaId);

        builder.HasOne<CadastroElegivel>()
            .WithMany()
            .HasForeignKey(vinculo => vinculo.ProfessorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Turma>()
            .WithMany()
            .HasForeignKey(vinculo => vinculo.TurmaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
