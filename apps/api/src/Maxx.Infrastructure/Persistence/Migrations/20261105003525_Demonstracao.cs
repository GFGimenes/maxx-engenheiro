using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Demonstracao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "hash_da_chave_do_dispositivo",
                table: "usuario",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "lote_da_demonstracao",
                table: "usuario",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "demonstracao",
                table: "cadastro_elegivel",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_usuario_hash_da_chave_do_dispositivo",
                table: "usuario",
                column: "hash_da_chave_do_dispositivo",
                unique: true,
                filter: "hash_da_chave_do_dispositivo IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_lote_da_demonstracao",
                table: "usuario",
                column: "lote_da_demonstracao",
                filter: "lote_da_demonstracao IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_usuario_demonstracao",
                table: "usuario",
                sql: "(lote_da_demonstracao IS NULL AND hash_da_chave_do_dispositivo IS NULL) OR (lote_da_demonstracao IS NOT NULL AND hash_da_chave_do_dispositivo IS NOT NULL AND papeis = 1 AND hash_da_senha IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_usuario_hash_da_chave_do_dispositivo",
                table: "usuario");

            migrationBuilder.DropIndex(
                name: "ix_usuario_lote_da_demonstracao",
                table: "usuario");

            migrationBuilder.DropCheckConstraint(
                name: "ck_usuario_demonstracao",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "hash_da_chave_do_dispositivo",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "lote_da_demonstracao",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "demonstracao",
                table: "cadastro_elegivel");
        }
    }
}
