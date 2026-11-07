using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotificacaoERaridade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notificacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    referencia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notificacao", x => x.id);
                    table.CheckConstraint("ck_notificacao_tipo", "tipo IN ('medalhaRecebida')");
                    table.ForeignKey(
                        name: "fk_notificacao_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "raridade_do_item",
                columns: table => new
                {
                    item_codigo = table.Column<string>(type: "text", nullable: false),
                    alunos_com_item = table.Column<int>(type: "integer", nullable: false),
                    alunos_elegiveis = table.Column<int>(type: "integer", nullable: false),
                    raridade = table.Column<string>(type: "text", nullable: false),
                    calculada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_raridade_do_item", x => x.item_codigo);
                    table.CheckConstraint("ck_raridade_do_item_contagem", "alunos_com_item >= 0 AND alunos_com_item <= alunos_elegiveis");
                    table.CheckConstraint("ck_raridade_do_item_raridade", "raridade IN ('comum', 'raro', 'epico', 'lendario')");
                    table.ForeignKey(
                        name: "fk_raridade_do_item_item_catalogo_item_codigo",
                        column: x => x.item_codigo,
                        principalTable: "item_catalogo",
                        principalColumn: "codigo",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recalculo",
                columns: table => new
                {
                    nome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ultima_execucao_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recalculo", x => x.nome);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notificacao_usuario_id_tipo_referencia_id",
                table: "notificacao",
                columns: new[] { "usuario_id", "tipo", "referencia_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notificacao");

            migrationBuilder.DropTable(
                name: "raridade_do_item");

            migrationBuilder.DropTable(
                name: "recalculo");
        }
    }
}
