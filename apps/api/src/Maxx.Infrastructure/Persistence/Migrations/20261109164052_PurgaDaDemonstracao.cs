using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PurgaDaDemonstracao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "purga_da_demonstracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lote = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    alunos = table.Column<int>(type: "integer", nullable: false),
                    fotos = table.Column<int>(type: "integer", nullable: false),
                    purgado_por_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purgado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purga_da_demonstracao", x => x.id);
                    table.CheckConstraint("ck_purga_da_demonstracao_contagens", "alunos >= 1 AND fotos >= 0");
                    table.ForeignKey(
                        name: "fk_purga_da_demonstracao_usuario_purgado_por_id",
                        column: x => x.purgado_por_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_purga_da_demonstracao_purgado_por_id",
                table: "purga_da_demonstracao",
                column: "purgado_por_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purga_da_demonstracao");
        }
    }
}
