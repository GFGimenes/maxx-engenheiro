using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Post : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "post",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    autor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legenda = table.Column<string>(type: "text", nullable: true),
                    chave_da_foto_do_feed = table.Column<string>(type: "text", nullable: true),
                    chave_da_foto_da_grade = table.Column<string>(type: "text", nullable: true),
                    medalha_id = table.Column<Guid>(type: "uuid", nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    removido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removido_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_post", x => x.id);
                    table.CheckConstraint("ck_post_foto_completa", "(chave_da_foto_do_feed IS NULL) = (chave_da_foto_da_grade IS NULL)");
                    table.CheckConstraint("ck_post_nao_vazio", "legenda IS NOT NULL OR chave_da_foto_do_feed IS NOT NULL OR medalha_id IS NOT NULL");
                    table.CheckConstraint("ck_post_remocao", "(removido_em IS NULL) = (removido_por_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_post_medalha_medalha_id",
                        column: x => x.medalha_id,
                        principalTable: "medalha",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_post_usuario_autor_id",
                        column: x => x.autor_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_post_usuario_removido_por_id",
                        column: x => x.removido_por_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_post_autor_id_criado_em_id",
                table: "post",
                columns: new[] { "autor_id", "criado_em", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_post_medalha_ativa",
                table: "post",
                column: "medalha_id",
                unique: true,
                filter: "medalha_id IS NOT NULL AND removido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_post_removido_por_id",
                table: "post",
                column: "removido_por_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "post");
        }
    }
}
