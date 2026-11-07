using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Medalha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "medalha",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aluno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_codigo = table.Column<string>(type: "text", nullable: false),
                    versao_do_item = table.Column<int>(type: "integer", nullable: false),
                    tier = table.Column<string>(type: "text", nullable: false),
                    pontos = table.Column<int>(type: "integer", nullable: false),
                    turma_id = table.Column<Guid>(type: "uuid", nullable: false),
                    materia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    semestre_letivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bimestre_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ocorrencia = table.Column<int>(type: "integer", nullable: false),
                    chave_de_deduplicacao = table.Column<string>(type: "text", nullable: false),
                    concedida_por_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concedida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    anulada_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    anulada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_medalha", x => x.id);
                    table.CheckConstraint("ck_medalha_anulacao", "(anulada_em IS NULL) = (anulada_por_id IS NULL)");
                    table.CheckConstraint("ck_medalha_ocorrencia", "ocorrencia >= 1");
                    table.CheckConstraint("ck_medalha_pontos", "pontos >= 1");
                    table.CheckConstraint("ck_medalha_versao_do_item", "versao_do_item >= 1");
                    table.ForeignKey(
                        name: "fk_medalha_bimestre_bimestre_id",
                        column: x => x.bimestre_id,
                        principalTable: "bimestre",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_medalha_item_catalogo_item_codigo",
                        column: x => x.item_codigo,
                        principalTable: "item_catalogo",
                        principalColumn: "codigo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_medalha_materia_materia_id",
                        column: x => x.materia_id,
                        principalTable: "materia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_medalha_semestre_letivo_semestre_letivo_id",
                        column: x => x.semestre_letivo_id,
                        principalTable: "semestre_letivo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_medalha_turma_turma_id",
                        column: x => x.turma_id,
                        principalTable: "turma",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_medalha_usuario_aluno_id",
                        column: x => x.aluno_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_medalha_usuario_anulada_por_id",
                        column: x => x.anulada_por_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_medalha_usuario_concedida_por_id",
                        column: x => x.concedida_por_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_medalha_aluno_id_item_codigo",
                table: "medalha",
                columns: new[] { "aluno_id", "item_codigo" });

            migrationBuilder.CreateIndex(
                name: "ix_medalha_anulada_por_id",
                table: "medalha",
                column: "anulada_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_medalha_bimestre_id",
                table: "medalha",
                column: "bimestre_id");

            migrationBuilder.CreateIndex(
                name: "ix_medalha_chave_ativa",
                table: "medalha",
                column: "chave_de_deduplicacao",
                unique: true,
                filter: "anulada_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_medalha_concedida_por_id",
                table: "medalha",
                column: "concedida_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_medalha_item_codigo",
                table: "medalha",
                column: "item_codigo");

            migrationBuilder.CreateIndex(
                name: "ix_medalha_materia_id",
                table: "medalha",
                column: "materia_id");

            migrationBuilder.CreateIndex(
                name: "ix_medalha_semestre_letivo_id",
                table: "medalha",
                column: "semestre_letivo_id");

            migrationBuilder.CreateIndex(
                name: "ix_medalha_turma_id",
                table: "medalha",
                column: "turma_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "medalha");
        }
    }
}
