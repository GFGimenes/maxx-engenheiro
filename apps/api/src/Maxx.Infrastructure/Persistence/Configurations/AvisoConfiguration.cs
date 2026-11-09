using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Domain.Mural;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maxx.Infrastructure.Persistence.Configurations;

// O aviso do mural (produto.md 7). As regras de forma do domínio repetidas no banco, como
// última barreira: título e texto no tamanho, a foto sempre com as duas derivadas, e os
// alvos só nas tabelas do escopo deles. O char_length do Postgres conta como o domínio:
// um caractere por ponto de código.
internal sealed class AvisoConfiguration : IEntityTypeConfiguration<Aviso>
{
    public void Configure(EntityTypeBuilder<Aviso> builder)
    {
        builder.ToTable("aviso", table =>
        {
            table.HasCheckConstraint(
                "ck_aviso_escopo",
                $"escopo IN ({string.Join(", ", Enum.GetValues<EscopoDoAviso>().Select(valor => $"'{NomeJson.De(valor)}'"))})");
            table.HasCheckConstraint(
                "ck_aviso_titulo", $"char_length(titulo) BETWEEN {Aviso.TamanhoMinimoDoTitulo} AND {Aviso.TamanhoMaximoDoTitulo}");
            table.HasCheckConstraint("ck_aviso_texto", $"char_length(texto) BETWEEN 1 AND {Aviso.TamanhoMaximoDoTexto}");
            table.HasCheckConstraint(
                "ck_aviso_foto_completa", "(chave_da_foto_do_feed IS NULL) = (chave_da_foto_da_grade IS NULL)");
        });
        builder.HasKey(aviso => aviso.Id);
        builder.Property(aviso => aviso.Id).ValueGeneratedNever();
        builder.Property(aviso => aviso.Escopo)
            .HasConversion(escopo => NomeJson.De(escopo), texto => Enum.Parse<EscopoDoAviso>(texto, ignoreCase: true));

        // O mural anda do mais novo ao mais antigo, por cursor (publicado_em, id); os
        // avisos de um autor, no perfil do professor (passo 10, etapa 2).
        builder.HasIndex(aviso => new { aviso.PublicadoEm, aviso.Id }).IsDescending(true, true);
        builder.HasIndex(aviso => new { aviso.AutorId, aviso.PublicadoEm }).IsDescending(false, true);

        // Quem publicou fica; aviso não tem remoção na fase 1.
        builder.HasOne<Usuario>().WithMany().HasForeignKey(aviso => aviso.AutorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(aviso => aviso.Turmas).WithOne().HasForeignKey(alvo => alvo.AvisoId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(aviso => aviso.Turmas).HasField("_turmas");
        builder.HasMany(aviso => aviso.Cursos).WithOne().HasForeignKey(alvo => alvo.AvisoId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(aviso => aviso.Cursos).HasField("_cursos");

        builder.Ignore(aviso => aviso.TemFoto);
    }
}

internal sealed class AvisoTurmaConfiguration : IEntityTypeConfiguration<AvisoTurma>
{
    public void Configure(EntityTypeBuilder<AvisoTurma> builder)
    {
        builder.ToTable("aviso_turma");
        builder.HasKey(alvo => new { alvo.AvisoId, alvo.TurmaId });

        // "Avisos das turmas do aluno" é a leitura do mural.
        builder.HasIndex(alvo => alvo.TurmaId);
        builder.HasOne<Turma>().WithMany().HasForeignKey(alvo => alvo.TurmaId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AvisoCursoConfiguration : IEntityTypeConfiguration<AvisoCurso>
{
    public void Configure(EntityTypeBuilder<AvisoCurso> builder)
    {
        builder.ToTable("aviso_curso");
        builder.HasKey(alvo => new { alvo.AvisoId, alvo.CursoId });
        builder.HasIndex(alvo => alvo.CursoId);
        builder.HasOne<Curso>().WithMany().HasForeignKey(alvo => alvo.CursoId).OnDelete(DeleteBehavior.Restrict);
    }
}

// A leitura de aviso (produto.md 7). Uma por usuário e aviso, pela chave; a ordem da chave
// serve à pergunta do mural, "o que este usuário já leu".
internal sealed class LeituraDeAvisoConfiguration : IEntityTypeConfiguration<LeituraDeAviso>
{
    public void Configure(EntityTypeBuilder<LeituraDeAviso> builder)
    {
        builder.ToTable("leitura_de_aviso");
        builder.HasKey(leitura => new { leitura.UsuarioId, leitura.AvisoId });
        builder.HasIndex(leitura => leitura.AvisoId);

        // Sem cascata do usuário, como o resto do que ele grava: a purga da demonstração
        // apaga as leituras antes dele. O aviso não sai na fase 1; se sair, leva as leituras.
        builder.HasOne<Usuario>().WithMany().HasForeignKey(leitura => leitura.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Aviso>().WithMany().HasForeignKey(leitura => leitura.AvisoId).OnDelete(DeleteBehavior.Cascade);
    }
}
