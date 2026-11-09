using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Ranking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "posicao_no_ranking",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    escopo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    periodo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    semestre_letivo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    curso_id = table.Column<Guid>(type: "uuid", nullable: true),
                    aluno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    posicao = table.Column<int>(type: "integer", nullable: false),
                    pontos = table.Column<int>(type: "integer", nullable: false),
                    ultima_concessao_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_posicao_no_ranking", x => x.id);
                    table.CheckConstraint("ck_posicao_no_ranking_curso", "(escopo = 'curso') = (curso_id IS NOT NULL)");
                    table.CheckConstraint("ck_posicao_no_ranking_escopo", "escopo IN ('geral', 'curso')");
                    table.CheckConstraint("ck_posicao_no_ranking_periodo", "periodo IN ('semestre', 'historico')");
                    table.CheckConstraint("ck_posicao_no_ranking_semestre", "(periodo = 'semestre') = (semestre_letivo_id IS NOT NULL)");
                    table.CheckConstraint("ck_posicao_no_ranking_valores", "posicao >= 1 AND pontos > 0");
                    table.ForeignKey(
                        name: "fk_posicao_no_ranking_curso_curso_id",
                        column: x => x.curso_id,
                        principalTable: "curso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_posicao_no_ranking_semestre_letivo_semestre_letivo_id",
                        column: x => x.semestre_letivo_id,
                        principalTable: "semestre_letivo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_posicao_no_ranking_usuario_aluno_id",
                        column: x => x.aluno_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_posicao_no_ranking_aluno_visao",
                table: "posicao_no_ranking",
                columns: new[] { "aluno_id", "escopo", "periodo", "semestre_letivo_id", "curso_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_posicao_no_ranking_curso_id",
                table: "posicao_no_ranking",
                column: "curso_id");

            migrationBuilder.CreateIndex(
                name: "ix_posicao_no_ranking_semestre_letivo_id",
                table: "posicao_no_ranking",
                column: "semestre_letivo_id");

            migrationBuilder.CreateIndex(
                name: "ix_posicao_no_ranking_visao_posicao",
                table: "posicao_no_ranking",
                columns: new[] { "escopo", "periodo", "semestre_letivo_id", "curso_id", "posicao" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "posicao_no_ranking");
        }
    }
}
