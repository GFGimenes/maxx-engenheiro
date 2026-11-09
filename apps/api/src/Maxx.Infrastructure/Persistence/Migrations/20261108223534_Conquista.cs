using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Conquista : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notificacao_tipo",
                table: "notificacao");

            migrationBuilder.CreateTable(
                name: "conquista",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aluno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_codigo = table.Column<string>(type: "text", nullable: false),
                    versao_do_item = table.Column<int>(type: "integer", nullable: false),
                    tier = table.Column<string>(type: "text", nullable: false),
                    pontos = table.Column<int>(type: "integer", nullable: false),
                    tipo_de_periodo = table.Column<string>(type: "text", nullable: false),
                    referencia_do_periodo = table.Column<Guid>(type: "uuid", nullable: false),
                    concedida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    removida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conquista", x => x.id);
                    table.CheckConstraint("ck_conquista_pontos", "pontos >= 1");
                    table.CheckConstraint("ck_conquista_tipo_de_periodo", "tipo_de_periodo IN ('bimestre', 'semestre', 'ano', 'curso')");
                    table.CheckConstraint("ck_conquista_versao_do_item", "versao_do_item >= 1");
                    table.ForeignKey(
                        name: "fk_conquista_item_catalogo_item_codigo",
                        column: x => x.item_codigo,
                        principalTable: "item_catalogo",
                        principalColumn: "codigo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_conquista_usuario_aluno_id",
                        column: x => x.aluno_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "progresso_de_conquista",
                columns: table => new
                {
                    aluno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_codigo = table.Column<string>(type: "text", nullable: false),
                    tipo_de_periodo = table.Column<string>(type: "text", nullable: false),
                    referencia_do_periodo = table.Column<Guid>(type: "uuid", nullable: false),
                    contagem = table.Column<int>(type: "integer", nullable: false),
                    alvo = table.Column<int>(type: "integer", nullable: false),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_progresso_de_conquista", x => new { x.aluno_id, x.item_codigo, x.tipo_de_periodo, x.referencia_do_periodo });
                    table.CheckConstraint("ck_progresso_de_conquista_contagem", "contagem >= 0 AND alvo >= 0");
                    table.CheckConstraint("ck_progresso_de_conquista_tipo_de_periodo", "tipo_de_periodo IN ('bimestre', 'semestre', 'ano', 'curso')");
                    table.ForeignKey(
                        name: "fk_progresso_de_conquista_item_catalogo_item_codigo",
                        column: x => x.item_codigo,
                        principalTable: "item_catalogo",
                        principalColumn: "codigo",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_progresso_de_conquista_usuario_aluno_id",
                        column: x => x.aluno_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evidencia_da_conquista",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conquista_id = table.Column<Guid>(type: "uuid", nullable: false),
                    medalha_id = table.Column<Guid>(type: "uuid", nullable: true),
                    premio_participacao_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evidencia_da_conquista", x => x.id);
                    table.CheckConstraint("ck_evidencia_da_conquista_um_premio", "(medalha_id IS NULL) <> (premio_participacao_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_evidencia_da_conquista_conquista_conquista_id",
                        column: x => x.conquista_id,
                        principalTable: "conquista",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_evidencia_da_conquista_medalha_medalha_id",
                        column: x => x.medalha_id,
                        principalTable: "medalha",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_evidencia_da_conquista_premio_participacao_premio_participa",
                        column: x => x.premio_participacao_id,
                        principalTable: "premio_participacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_notificacao_tipo",
                table: "notificacao",
                sql: "tipo IN ('medalhaRecebida', 'comprovacaoAprovada', 'comprovacaoReprovada', 'contestacaoAberta', 'decisaoDeContestacao', 'premioRevogado', 'conquistaObtida', 'conquistaRemovida')");

            migrationBuilder.CreateIndex(
                name: "ix_conquista_instancia_ativa",
                table: "conquista",
                columns: new[] { "aluno_id", "item_codigo", "tipo_de_periodo", "referencia_do_periodo" },
                unique: true,
                filter: "removida_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_conquista_item_codigo",
                table: "conquista",
                column: "item_codigo");

            migrationBuilder.CreateIndex(
                name: "ix_evidencia_da_conquista_conquista_id",
                table: "evidencia_da_conquista",
                column: "conquista_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidencia_da_conquista_medalha_id",
                table: "evidencia_da_conquista",
                column: "medalha_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidencia_da_conquista_premio_participacao_id",
                table: "evidencia_da_conquista",
                column: "premio_participacao_id");

            migrationBuilder.CreateIndex(
                name: "ix_progresso_de_conquista_item_codigo",
                table: "progresso_de_conquista",
                column: "item_codigo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evidencia_da_conquista");

            migrationBuilder.DropTable(
                name: "progresso_de_conquista");

            migrationBuilder.DropTable(
                name: "conquista");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notificacao_tipo",
                table: "notificacao");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notificacao_tipo",
                table: "notificacao",
                sql: "tipo IN ('medalhaRecebida', 'comprovacaoAprovada', 'comprovacaoReprovada', 'contestacaoAberta', 'decisaoDeContestacao', 'premioRevogado')");
        }
    }
}
