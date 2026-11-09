using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AtosDaInstituicao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ex_aluno_marcado_por_id",
                table: "usuario",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "correcao_de_email",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cadastro_elegivel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email_anterior = table.Column<string>(type: "citext", nullable: false),
                    email_novo = table.Column<string>(type: "citext", nullable: false),
                    corrigido_por_id = table.Column<Guid>(type: "uuid", nullable: false),
                    corrigido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_correcao_de_email", x => x.id);
                    table.ForeignKey(
                        name: "fk_correcao_de_email_cadastro_elegivel_cadastro_elegivel_id",
                        column: x => x.cadastro_elegivel_id,
                        principalTable: "cadastro_elegivel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_correcao_de_email_usuario_corrigido_por_id",
                        column: x => x.corrigido_por_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "troca_de_curso",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cadastro_elegivel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    curso_anterior_id = table.Column<Guid>(type: "uuid", nullable: false),
                    semestre_anterior = table.Column<int>(type: "integer", nullable: false),
                    curso_novo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    semestre_novo = table.Column<int>(type: "integer", nullable: false),
                    trocado_por_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trocado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_troca_de_curso", x => x.id);
                    table.CheckConstraint("ck_troca_de_curso_cursos", "curso_anterior_id <> curso_novo_id");
                    table.CheckConstraint("ck_troca_de_curso_semestres", "semestre_anterior >= 1 AND semestre_novo >= 1");
                    table.ForeignKey(
                        name: "fk_troca_de_curso_cadastro_elegivel_cadastro_elegivel_id",
                        column: x => x.cadastro_elegivel_id,
                        principalTable: "cadastro_elegivel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_troca_de_curso_curso_curso_anterior_id",
                        column: x => x.curso_anterior_id,
                        principalTable: "curso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_troca_de_curso_curso_curso_novo_id",
                        column: x => x.curso_novo_id,
                        principalTable: "curso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_troca_de_curso_usuario_trocado_por_id",
                        column: x => x.trocado_por_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_usuario_ex_aluno_marcado_por_id",
                table: "usuario",
                column: "ex_aluno_marcado_por_id");

            // Antes do passo 12, ex-aluno só nascia por escrita direta no banco, nos testes: sem
            // ato da instituição a registrar, a própria conta fica como quem marcou.
            migrationBuilder.Sql(
                "UPDATE usuario SET ex_aluno_marcado_por_id = id WHERE ex_aluno_desde IS NOT NULL AND ex_aluno_marcado_por_id IS NULL;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_usuario_ex_aluno_marcado_por",
                table: "usuario",
                sql: "(ex_aluno_desde IS NULL) = (ex_aluno_marcado_por_id IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_correcao_de_email_cadastro_elegivel_id",
                table: "correcao_de_email",
                column: "cadastro_elegivel_id");

            migrationBuilder.CreateIndex(
                name: "ix_correcao_de_email_corrigido_por_id",
                table: "correcao_de_email",
                column: "corrigido_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_troca_de_curso_cadastro_elegivel_id",
                table: "troca_de_curso",
                column: "cadastro_elegivel_id");

            migrationBuilder.CreateIndex(
                name: "ix_troca_de_curso_curso_anterior_id",
                table: "troca_de_curso",
                column: "curso_anterior_id");

            migrationBuilder.CreateIndex(
                name: "ix_troca_de_curso_curso_novo_id",
                table: "troca_de_curso",
                column: "curso_novo_id");

            migrationBuilder.CreateIndex(
                name: "ix_troca_de_curso_trocado_por_id",
                table: "troca_de_curso",
                column: "trocado_por_id");

            migrationBuilder.AddForeignKey(
                name: "fk_usuario_usuario_ex_aluno_marcado_por_id",
                table: "usuario",
                column: "ex_aluno_marcado_por_id",
                principalTable: "usuario",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_usuario_usuario_ex_aluno_marcado_por_id",
                table: "usuario");

            migrationBuilder.DropTable(
                name: "correcao_de_email");

            migrationBuilder.DropTable(
                name: "troca_de_curso");

            migrationBuilder.DropIndex(
                name: "ix_usuario_ex_aluno_marcado_por_id",
                table: "usuario");

            migrationBuilder.DropCheckConstraint(
                name: "ck_usuario_ex_aluno_marcado_por",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "ex_aluno_marcado_por_id",
                table: "usuario");
        }
    }
}
