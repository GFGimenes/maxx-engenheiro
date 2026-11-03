using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ModeloAcademico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.CreateTable(
                name: "ano_letivo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ano = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ano_letivo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "curso",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false),
                    sigla = table.Column<string>(type: "text", nullable: false),
                    duracao_em_semestres = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_curso", x => x.id);
                    table.CheckConstraint("ck_curso_duracao_em_semestres", "duracao_em_semestres >= 1");
                });

            migrationBuilder.CreateTable(
                name: "semestre_letivo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ano = table.Column<int>(type: "integer", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    ano_letivo_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_semestre_letivo", x => x.id);
                    table.CheckConstraint("ck_semestre_letivo_numero", "numero IN (1, 2)");
                    table.ForeignKey(
                        name: "fk_semestre_letivo_ano_letivo_ano_letivo_id",
                        column: x => x.ano_letivo_id,
                        principalTable: "ano_letivo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cadastro_elegivel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    papeis = table.Column<int>(type: "integer", nullable: false),
                    ra = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "citext", nullable: false),
                    nome_completo = table.Column<string>(type: "text", nullable: false),
                    curso_id = table.Column<Guid>(type: "uuid", nullable: true),
                    semestre = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cadastro_elegivel", x => x.id);
                    table.CheckConstraint("ck_cadastro_elegivel_aluno", "(papeis = 1 AND ra IS NOT NULL AND curso_id IS NOT NULL AND semestre >= 1) OR (papeis <> 1 AND curso_id IS NULL AND semestre IS NULL)");
                    table.CheckConstraint("ck_cadastro_elegivel_papeis", "papeis IN (1, 2, 4, 6)");
                    table.ForeignKey(
                        name: "fk_cadastro_elegivel_curso_curso_id",
                        column: x => x.curso_id,
                        principalTable: "curso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "materia",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    curso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_materia", x => x.id);
                    table.ForeignKey(
                        name: "fk_materia_curso_curso_id",
                        column: x => x.curso_id,
                        principalTable: "curso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bimestre",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fim = table.Column<DateOnly>(type: "date", nullable: false),
                    semestre_letivo_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bimestre", x => x.id);
                    table.CheckConstraint("ck_bimestre_fim_depois_do_inicio", "fim >= inicio");
                    table.CheckConstraint("ck_bimestre_numero", "numero BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "fk_bimestre_semestre_letivo_semestre_letivo_id",
                        column: x => x.semestre_letivo_id,
                        principalTable: "semestre_letivo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "turma",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    materia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    semestre_letivo_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_turma", x => x.id);
                    table.ForeignKey(
                        name: "fk_turma_materia_materia_id",
                        column: x => x.materia_id,
                        principalTable: "materia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_turma_semestre_letivo_semestre_letivo_id",
                        column: x => x.semestre_letivo_id,
                        principalTable: "semestre_letivo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "matricula",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aluno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    turma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_matricula", x => x.id);
                    table.ForeignKey(
                        name: "fk_matricula_cadastro_elegivel_aluno_id",
                        column: x => x.aluno_id,
                        principalTable: "cadastro_elegivel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_matricula_turma_turma_id",
                        column: x => x.turma_id,
                        principalTable: "turma",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vinculo_professor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    professor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    turma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vinculo_professor", x => x.id);
                    table.ForeignKey(
                        name: "fk_vinculo_professor_cadastro_elegivel_professor_id",
                        column: x => x.professor_id,
                        principalTable: "cadastro_elegivel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vinculo_professor_turma_turma_id",
                        column: x => x.turma_id,
                        principalTable: "turma",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ano_letivo_ano",
                table: "ano_letivo",
                column: "ano",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bimestre_semestre_letivo_id_numero",
                table: "bimestre",
                columns: new[] { "semestre_letivo_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cadastro_elegivel_curso_id",
                table: "cadastro_elegivel",
                column: "curso_id");

            migrationBuilder.CreateIndex(
                name: "ix_cadastro_elegivel_email",
                table: "cadastro_elegivel",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cadastro_elegivel_ra",
                table: "cadastro_elegivel",
                column: "ra",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_curso_sigla",
                table: "curso",
                column: "sigla",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_materia_curso_id_nome",
                table: "materia",
                columns: new[] { "curso_id", "nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_matricula_aluno_id_turma_id",
                table: "matricula",
                columns: new[] { "aluno_id", "turma_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_matricula_turma_id",
                table: "matricula",
                column: "turma_id");

            migrationBuilder.CreateIndex(
                name: "ix_semestre_letivo_ano_letivo_id",
                table: "semestre_letivo",
                column: "ano_letivo_id");

            migrationBuilder.CreateIndex(
                name: "ix_semestre_letivo_ano_numero",
                table: "semestre_letivo",
                columns: new[] { "ano", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_turma_materia_id_semestre_letivo_id",
                table: "turma",
                columns: new[] { "materia_id", "semestre_letivo_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_turma_semestre_letivo_id",
                table: "turma",
                column: "semestre_letivo_id");

            migrationBuilder.CreateIndex(
                name: "ix_vinculo_professor_professor_id_turma_id",
                table: "vinculo_professor",
                columns: new[] { "professor_id", "turma_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vinculo_professor_turma_id",
                table: "vinculo_professor",
                column: "turma_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bimestre");

            migrationBuilder.DropTable(
                name: "matricula");

            migrationBuilder.DropTable(
                name: "vinculo_professor");

            migrationBuilder.DropTable(
                name: "cadastro_elegivel");

            migrationBuilder.DropTable(
                name: "turma");

            migrationBuilder.DropTable(
                name: "materia");

            migrationBuilder.DropTable(
                name: "semestre_letivo");

            migrationBuilder.DropTable(
                name: "curso");

            migrationBuilder.DropTable(
                name: "ano_letivo");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:citext", ",,");
        }
    }
}
