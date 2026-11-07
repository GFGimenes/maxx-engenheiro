using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TermoDeUso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "termo_de_uso",
                columns: table => new
                {
                    versao = table.Column<int>(type: "integer", nullable: false),
                    conteudo = table.Column<string>(type: "text", nullable: false),
                    publicado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_termo_de_uso", x => x.versao);
                    table.CheckConstraint("ck_termo_de_uso_versao", "versao >= 1");
                });

            migrationBuilder.CreateTable(
                name: "aceite_de_termo",
                columns: table => new
                {
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    aceito_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_aceite_de_termo", x => new { x.usuario_id, x.versao });
                    table.ForeignKey(
                        name: "fk_aceite_de_termo_termo_de_uso_versao",
                        column: x => x.versao,
                        principalTable: "termo_de_uso",
                        principalColumn: "versao",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_aceite_de_termo_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_aceite_de_termo_versao",
                table: "aceite_de_termo",
                column: "versao");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aceite_de_termo");

            migrationBuilder.DropTable(
                name: "termo_de_uso");
        }
    }
}
