using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PostDeConquista : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_post_nao_vazio",
                table: "post");

            migrationBuilder.DropCheckConstraint(
                name: "ck_post_um_premio",
                table: "post");

            migrationBuilder.AddColumn<Guid>(
                name: "conquista_id",
                table: "post",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_post_conquista_ativa",
                table: "post",
                column: "conquista_id",
                unique: true,
                filter: "conquista_id IS NOT NULL AND removido_em IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_post_nao_vazio",
                table: "post",
                sql: "legenda IS NOT NULL OR chave_da_foto_do_feed IS NOT NULL OR medalha_id IS NOT NULL OR premio_participacao_id IS NOT NULL OR conquista_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_post_um_premio",
                table: "post",
                sql: "num_nonnulls(medalha_id, premio_participacao_id, conquista_id) <= 1");

            migrationBuilder.AddForeignKey(
                name: "fk_post_conquista_conquista_id",
                table: "post",
                column: "conquista_id",
                principalTable: "conquista",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_post_conquista_conquista_id",
                table: "post");

            migrationBuilder.DropIndex(
                name: "ix_post_conquista_ativa",
                table: "post");

            migrationBuilder.DropCheckConstraint(
                name: "ck_post_nao_vazio",
                table: "post");

            migrationBuilder.DropCheckConstraint(
                name: "ck_post_um_premio",
                table: "post");

            migrationBuilder.DropColumn(
                name: "conquista_id",
                table: "post");

            migrationBuilder.AddCheckConstraint(
                name: "ck_post_nao_vazio",
                table: "post",
                sql: "legenda IS NOT NULL OR chave_da_foto_do_feed IS NOT NULL OR medalha_id IS NOT NULL OR premio_participacao_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_post_um_premio",
                table: "post",
                sql: "medalha_id IS NULL OR premio_participacao_id IS NULL");
        }
    }
}
