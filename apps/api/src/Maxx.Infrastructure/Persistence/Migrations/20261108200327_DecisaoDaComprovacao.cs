using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DecisaoDaComprovacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notificacao_tipo",
                table: "notificacao");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "aprovado_em",
                table: "premio_participacao",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "aprovado_por_id",
                table: "premio_participacao",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "definitivo_em",
                table: "premio_participacao",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_da_reprovacao",
                table: "premio_participacao",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reprovado_em",
                table: "premio_participacao",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reprovado_por_id",
                table: "premio_participacao",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_premio_participacao_aprovado_por_id",
                table: "premio_participacao",
                column: "aprovado_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_premio_participacao_reprovado_por_id",
                table: "premio_participacao",
                column: "reprovado_por_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_premio_participacao_aprovacao",
                table: "premio_participacao",
                sql: "(aprovado_em IS NULL) = (aprovado_por_id IS NULL) AND (aprovado_em IS NULL) = (estado IN ('reivindicado', 'reprovado'))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_premio_participacao_definitivo",
                table: "premio_participacao",
                sql: "(definitivo_em IS NULL) = (estado <> 'definitivo')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_premio_participacao_reprovacao",
                table: "premio_participacao",
                sql: "(reprovado_em IS NULL) = (reprovado_por_id IS NULL) AND (reprovado_em IS NULL) = (motivo_da_reprovacao IS NULL) AND (reprovado_em IS NULL) = (estado <> 'reprovado')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notificacao_tipo",
                table: "notificacao",
                sql: "tipo IN ('medalhaRecebida', 'comprovacaoAprovada', 'comprovacaoReprovada')");

            migrationBuilder.AddForeignKey(
                name: "fk_premio_participacao_usuario_aprovado_por_id",
                table: "premio_participacao",
                column: "aprovado_por_id",
                principalTable: "usuario",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_premio_participacao_usuario_reprovado_por_id",
                table: "premio_participacao",
                column: "reprovado_por_id",
                principalTable: "usuario",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_premio_participacao_usuario_aprovado_por_id",
                table: "premio_participacao");

            migrationBuilder.DropForeignKey(
                name: "fk_premio_participacao_usuario_reprovado_por_id",
                table: "premio_participacao");

            migrationBuilder.DropIndex(
                name: "ix_premio_participacao_aprovado_por_id",
                table: "premio_participacao");

            migrationBuilder.DropIndex(
                name: "ix_premio_participacao_reprovado_por_id",
                table: "premio_participacao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_premio_participacao_aprovacao",
                table: "premio_participacao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_premio_participacao_definitivo",
                table: "premio_participacao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_premio_participacao_reprovacao",
                table: "premio_participacao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notificacao_tipo",
                table: "notificacao");

            migrationBuilder.DropColumn(
                name: "aprovado_em",
                table: "premio_participacao");

            migrationBuilder.DropColumn(
                name: "aprovado_por_id",
                table: "premio_participacao");

            migrationBuilder.DropColumn(
                name: "definitivo_em",
                table: "premio_participacao");

            migrationBuilder.DropColumn(
                name: "motivo_da_reprovacao",
                table: "premio_participacao");

            migrationBuilder.DropColumn(
                name: "reprovado_em",
                table: "premio_participacao");

            migrationBuilder.DropColumn(
                name: "reprovado_por_id",
                table: "premio_participacao");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notificacao_tipo",
                table: "notificacao",
                sql: "tipo IN ('medalhaRecebida')");
        }
    }
}
