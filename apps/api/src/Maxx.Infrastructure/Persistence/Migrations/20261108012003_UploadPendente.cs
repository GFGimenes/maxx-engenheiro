using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UploadPendente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "upload_pendente",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    finalidade = table.Column<string>(type: "text", nullable: false),
                    chave_do_feed = table.Column<string>(type: "text", nullable: false),
                    chave_da_grade = table.Column<string>(type: "text", nullable: false),
                    tipo_de_conteudo = table.Column<string>(type: "text", nullable: false),
                    tamanho_do_feed = table.Column<long>(type: "bigint", nullable: false),
                    tamanho_da_grade = table.Column<long>(type: "bigint", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_upload_pendente", x => x.id);
                    table.CheckConstraint("ck_upload_pendente_finalidade", "finalidade IN ('post', 'fotoDePerfil')");
                    table.CheckConstraint("ck_upload_pendente_tamanho_da_grade", "tamanho_da_grade BETWEEN 1 AND 204800");
                    table.CheckConstraint("ck_upload_pendente_tamanho_do_feed", "tamanho_do_feed BETWEEN 1 AND 1048576");
                    table.CheckConstraint("ck_upload_pendente_tipo", "tipo_de_conteudo = 'image/jpeg'");
                    table.ForeignKey(
                        name: "fk_upload_pendente_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_upload_pendente_chave_da_grade",
                table: "upload_pendente",
                column: "chave_da_grade",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_upload_pendente_chave_do_feed",
                table: "upload_pendente",
                column: "chave_do_feed",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_upload_pendente_usuario_id",
                table: "upload_pendente",
                column: "usuario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "upload_pendente");
        }
    }
}
