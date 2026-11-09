using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Domain.Ranking;
using Maxx.Infrastructure.Ranking;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maxx.Infrastructure.Persistence.Configurations;

internal sealed class PosicaoNoRankingConfiguration : IEntityTypeConfiguration<PosicaoNoRankingRow>
{
    public void Configure(EntityTypeBuilder<PosicaoNoRankingRow> builder)
    {
        builder.ToTable("posicao_no_ranking", table =>
        {
            table.HasCheckConstraint("ck_posicao_no_ranking_escopo", $"escopo IN ({Lista<EscopoDoRanking>()})");
            table.HasCheckConstraint("ck_posicao_no_ranking_periodo", $"periodo IN ({Lista<PeriodoDoRanking>()})");

            // O semestre existe só no período semestre; o curso, só no escopo curso.
            table.HasCheckConstraint(
                "ck_posicao_no_ranking_semestre", "(periodo = 'semestre') = (semestre_letivo_id IS NOT NULL)");
            table.HasCheckConstraint("ck_posicao_no_ranking_curso", "(escopo = 'curso') = (curso_id IS NOT NULL)");

            // Aluno sem pontos na visão não tem linha nela.
            table.HasCheckConstraint("ck_posicao_no_ranking_valores", "posicao >= 1 AND pontos > 0");
        });
        builder.HasKey(linha => linha.Id);
        builder.Property(linha => linha.Id).UseIdentityAlwaysColumn();
        builder.Property(linha => linha.Escopo).HasConversion(Conversao<EscopoDoRanking>()).HasMaxLength(10);
        builder.Property(linha => linha.Periodo).HasConversion(Conversao<PeriodoDoRanking>()).HasMaxLength(10);

        // A leitura: a página de uma visão, pela posição. Uma posição por visão, e o nulo do
        // semestre e do curso conta como valor, para o histórico e o geral também.
        builder.HasIndex(linha => new { linha.Escopo, linha.Periodo, linha.SemestreLetivoId, linha.CursoId, linha.Posicao })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("ix_posicao_no_ranking_visao_posicao");

        // A linha do próprio aluno em cada visão: no perfil e no card da tela. Uma por visão.
        builder.HasIndex(linha => new { linha.AlunoId, linha.Escopo, linha.Periodo, linha.SemestreLetivoId, linha.CursoId })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("ix_posicao_no_ranking_aluno_visao");

        // Dado derivado: sai junto com o aluno (a purga da demonstração) e com o semestre. O
        // próximo recálculo reescreve o que sobrar.
        builder.HasOne<Usuario>().WithMany().HasForeignKey(linha => linha.AlunoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<SemestreLetivo>().WithMany().HasForeignKey(linha => linha.SemestreLetivoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Curso>().WithMany().HasForeignKey(linha => linha.CursoId).OnDelete(DeleteBehavior.Cascade);
    }

    private static Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<T, string> Conversao<T>()
        where T : struct, Enum => new(valor => NomeJson.De(valor), texto => Enum.Parse<T>(texto, true));

    private static string Lista<T>()
        where T : struct, Enum => string.Join(", ", Enum.GetValues<T>().Select(valor => $"'{NomeJson.De(valor)}'"));
}
