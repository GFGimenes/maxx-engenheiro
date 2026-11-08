using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PremioParticipacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_post_nao_vazio",
                table: "post");

            migrationBuilder.AddColumn<Guid>(
                name: "premio_participacao_id",
                table: "post",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "premio_participacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aluno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_codigo = table.Column<string>(type: "text", nullable: false),
                    versao_do_item = table.Column<int>(type: "integer", nullable: false),
                    tier = table.Column<string>(type: "text", nullable: false),
                    pontos = table.Column<int>(type: "integer", nullable: false),
                    evento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    materia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    chave_de_deduplicacao = table.Column<string>(type: "text", nullable: false),
                    estado = table.Column<string>(type: "text", nullable: false),
                    reivindicado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_premio_participacao", x => x.id);
                    table.CheckConstraint("ck_premio_participacao_pontos", "pontos >= 1");
                    table.CheckConstraint("ck_premio_participacao_versao_do_item", "versao_do_item >= 1");
                    table.ForeignKey(
                        name: "fk_premio_participacao_evento_evento_id",
                        column: x => x.evento_id,
                        principalTable: "evento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_premio_participacao_item_catalogo_item_codigo",
                        column: x => x.item_codigo,
                        principalTable: "item_catalogo",
                        principalColumn: "codigo",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_premio_participacao_materia_materia_id",
                        column: x => x.materia_id,
                        principalTable: "materia",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_premio_participacao_usuario_aluno_id",
                        column: x => x.aluno_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "comprovacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    premio_participacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aluno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    post_id = table.Column<Guid>(type: "uuid", nullable: true),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comprovacao", x => x.id);
                    table.ForeignKey(
                        name: "fk_comprovacao_evento_evento_id",
                        column: x => x.evento_id,
                        principalTable: "evento",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_comprovacao_post_post_id",
                        column: x => x.post_id,
                        principalTable: "post",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_comprovacao_premio_participacao_premio_participacao_id",
                        column: x => x.premio_participacao_id,
                        principalTable: "premio_participacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_comprovacao_usuario_aluno_id",
                        column: x => x.aluno_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_post_premio_participacao_ativo",
                table: "post",
                column: "premio_participacao_id",
                unique: true,
                filter: "premio_participacao_id IS NOT NULL AND removido_em IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_post_nao_vazio",
                table: "post",
                sql: "legenda IS NOT NULL OR chave_da_foto_do_feed IS NOT NULL OR medalha_id IS NOT NULL OR premio_participacao_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_post_um_premio",
                table: "post",
                sql: "medalha_id IS NULL OR premio_participacao_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_comprovacao_aluno_id",
                table: "comprovacao",
                column: "aluno_id");

            migrationBuilder.CreateIndex(
                name: "ix_comprovacao_criada_em_id",
                table: "comprovacao",
                columns: new[] { "criada_em", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_comprovacao_evento_id_criada_em_id",
                table: "comprovacao",
                columns: new[] { "evento_id", "criada_em", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_comprovacao_post_id",
                table: "comprovacao",
                column: "post_id");

            migrationBuilder.CreateIndex(
                name: "ix_comprovacao_premio_participacao_id",
                table: "comprovacao",
                column: "premio_participacao_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_premio_participacao_aluno_id_item_codigo",
                table: "premio_participacao",
                columns: new[] { "aluno_id", "item_codigo" });

            migrationBuilder.CreateIndex(
                name: "ix_premio_participacao_chave_ativa",
                table: "premio_participacao",
                column: "chave_de_deduplicacao",
                unique: true,
                filter: "estado <> 'reprovado'");

            migrationBuilder.CreateIndex(
                name: "ix_premio_participacao_evento_id",
                table: "premio_participacao",
                column: "evento_id");

            migrationBuilder.CreateIndex(
                name: "ix_premio_participacao_item_codigo",
                table: "premio_participacao",
                column: "item_codigo");

            migrationBuilder.CreateIndex(
                name: "ix_premio_participacao_materia_id",
                table: "premio_participacao",
                column: "materia_id");

            migrationBuilder.AddForeignKey(
                name: "fk_post_premio_participacao_premio_participacao_id",
                table: "post",
                column: "premio_participacao_id",
                principalTable: "premio_participacao",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_post_premio_participacao_premio_participacao_id",
                table: "post");

            migrationBuilder.DropTable(
                name: "comprovacao");

            migrationBuilder.DropTable(
                name: "premio_participacao");

            migrationBuilder.DropIndex(
                name: "ix_post_premio_participacao_ativo",
                table: "post");

            migrationBuilder.DropCheckConstraint(
                name: "ck_post_nao_vazio",
                table: "post");

            migrationBuilder.DropCheckConstraint(
                name: "ck_post_um_premio",
                table: "post");

            migrationBuilder.DropColumn(
                name: "premio_participacao_id",
                table: "post");

            migrationBuilder.AddCheckConstraint(
                name: "ck_post_nao_vazio",
                table: "post",
                sql: "legenda IS NOT NULL OR chave_da_foto_do_feed IS NOT NULL OR medalha_id IS NOT NULL");
        }
    }
}
