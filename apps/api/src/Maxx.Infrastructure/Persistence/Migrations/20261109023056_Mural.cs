using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Mural : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_upload_pendente_finalidade",
                table: "upload_pendente");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notificacao_tipo",
                table: "notificacao");

            migrationBuilder.CreateTable(
                name: "aviso",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    autor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    escopo = table.Column<string>(type: "text", nullable: false),
                    titulo = table.Column<string>(type: "text", nullable: false),
                    texto = table.Column<string>(type: "text", nullable: false),
                    chave_da_foto_do_feed = table.Column<string>(type: "text", nullable: true),
                    chave_da_foto_da_grade = table.Column<string>(type: "text", nullable: true),
                    publicado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_aviso", x => x.id);
                    table.CheckConstraint("ck_aviso_escopo", "escopo IN ('faculdade', 'curso', 'turma')");
                    table.CheckConstraint("ck_aviso_foto_completa", "(chave_da_foto_do_feed IS NULL) = (chave_da_foto_da_grade IS NULL)");
                    table.CheckConstraint("ck_aviso_texto", "char_length(texto) BETWEEN 1 AND 2000");
                    table.CheckConstraint("ck_aviso_titulo", "char_length(titulo) BETWEEN 3 AND 120");
                    table.ForeignKey(
                        name: "fk_aviso_usuario_autor_id",
                        column: x => x.autor_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "aviso_curso",
                columns: table => new
                {
                    aviso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    curso_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_aviso_curso", x => new { x.aviso_id, x.curso_id });
                    table.ForeignKey(
                        name: "fk_aviso_curso_aviso_aviso_id",
                        column: x => x.aviso_id,
                        principalTable: "aviso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_aviso_curso_curso_curso_id",
                        column: x => x.curso_id,
                        principalTable: "curso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "aviso_turma",
                columns: table => new
                {
                    aviso_id = table.Column<Guid>(type: "uuid", nullable: false),
                    turma_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_aviso_turma", x => new { x.aviso_id, x.turma_id });
                    table.ForeignKey(
                        name: "fk_aviso_turma_aviso_aviso_id",
                        column: x => x.aviso_id,
                        principalTable: "aviso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_aviso_turma_turma_turma_id",
                        column: x => x.turma_id,
                        principalTable: "turma",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_upload_pendente_finalidade",
                table: "upload_pendente",
                sql: "finalidade IN ('post', 'fotoDePerfil', 'aviso')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notificacao_tipo",
                table: "notificacao",
                sql: "tipo IN ('medalhaRecebida', 'comprovacaoAprovada', 'comprovacaoReprovada', 'contestacaoAberta', 'decisaoDeContestacao', 'premioRevogado', 'conquistaObtida', 'conquistaRemovida', 'avisoNovo')");

            migrationBuilder.CreateIndex(
                name: "ix_aviso_autor_id_publicado_em",
                table: "aviso",
                columns: new[] { "autor_id", "publicado_em" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_aviso_publicado_em_id",
                table: "aviso",
                columns: new[] { "publicado_em", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_aviso_curso_curso_id",
                table: "aviso_curso",
                column: "curso_id");

            migrationBuilder.CreateIndex(
                name: "ix_aviso_turma_turma_id",
                table: "aviso_turma",
                column: "turma_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aviso_curso");

            migrationBuilder.DropTable(
                name: "aviso_turma");

            migrationBuilder.DropTable(
                name: "aviso");

            migrationBuilder.DropCheckConstraint(
                name: "ck_upload_pendente_finalidade",
                table: "upload_pendente");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notificacao_tipo",
                table: "notificacao");

            migrationBuilder.AddCheckConstraint(
                name: "ck_upload_pendente_finalidade",
                table: "upload_pendente",
                sql: "finalidade IN ('post', 'fotoDePerfil')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notificacao_tipo",
                table: "notificacao",
                sql: "tipo IN ('medalhaRecebida', 'comprovacaoAprovada', 'comprovacaoReprovada', 'contestacaoAberta', 'decisaoDeContestacao', 'premioRevogado', 'conquistaObtida', 'conquistaRemovida')");
        }
    }
}
