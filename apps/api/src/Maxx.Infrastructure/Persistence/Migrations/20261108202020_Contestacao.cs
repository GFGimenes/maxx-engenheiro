using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Contestacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notificacao_tipo",
                table: "notificacao");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "contestado_em",
                table: "premio_participacao",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_da_revogacao",
                table: "premio_participacao",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "revogado_em",
                table: "premio_participacao",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "revogado_por_id",
                table: "premio_participacao",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "contestacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    premio_participacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contestada_por_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contestada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    motivo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    decidida_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decidida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decisao = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contestacao", x => x.id);
                    table.CheckConstraint("ck_contestacao_decisao", "(decidida_em IS NULL) = (decidida_por_id IS NULL) AND (decidida_em IS NULL) = (decisao IS NULL)");
                    table.ForeignKey(
                        name: "fk_contestacao_premio_participacao_premio_participacao_id",
                        column: x => x.premio_participacao_id,
                        principalTable: "premio_participacao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_contestacao_usuario_contestada_por_id",
                        column: x => x.contestada_por_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_contestacao_usuario_decidida_por_id",
                        column: x => x.decidida_por_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_premio_participacao_revogado_por_id",
                table: "premio_participacao",
                column: "revogado_por_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_premio_participacao_averiguacao",
                table: "premio_participacao",
                sql: "estado <> 'emAveriguacao' OR contestado_em IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_premio_participacao_revogacao",
                table: "premio_participacao",
                sql: "(revogado_em IS NULL) = (revogado_por_id IS NULL) AND (revogado_em IS NULL) = (motivo_da_revogacao IS NULL) AND (revogado_em IS NULL) = (estado <> 'revogado')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notificacao_tipo",
                table: "notificacao",
                sql: "tipo IN ('medalhaRecebida', 'comprovacaoAprovada', 'comprovacaoReprovada', 'contestacaoAberta', 'decisaoDeContestacao', 'premioRevogado')");

            migrationBuilder.CreateIndex(
                name: "ix_contestacao_contestada_em_id",
                table: "contestacao",
                columns: new[] { "contestada_em", "id" },
                filter: "decisao IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_contestacao_contestada_por_id",
                table: "contestacao",
                column: "contestada_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_contestacao_decidida_por_id",
                table: "contestacao",
                column: "decidida_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_contestacao_premio_participacao_id",
                table: "contestacao",
                column: "premio_participacao_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_premio_participacao_usuario_revogado_por_id",
                table: "premio_participacao",
                column: "revogado_por_id",
                principalTable: "usuario",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_premio_participacao_usuario_revogado_por_id",
                table: "premio_participacao");

            migrationBuilder.DropTable(
                name: "contestacao");

            migrationBuilder.DropIndex(
                name: "ix_premio_participacao_revogado_por_id",
                table: "premio_participacao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_premio_participacao_averiguacao",
                table: "premio_participacao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_premio_participacao_revogacao",
                table: "premio_participacao");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notificacao_tipo",
                table: "notificacao");

            migrationBuilder.DropColumn(
                name: "contestado_em",
                table: "premio_participacao");

            migrationBuilder.DropColumn(
                name: "motivo_da_revogacao",
                table: "premio_participacao");

            migrationBuilder.DropColumn(
                name: "revogado_em",
                table: "premio_participacao");

            migrationBuilder.DropColumn(
                name: "revogado_por_id",
                table: "premio_participacao");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notificacao_tipo",
                table: "notificacao",
                sql: "tipo IN ('medalhaRecebida', 'comprovacaoAprovada', 'comprovacaoReprovada')");
        }
    }
}
