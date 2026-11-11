using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PapeisNaDemonstracao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_usuario_demonstracao",
                table: "usuario");

            migrationBuilder.DropCheckConstraint(
                name: "ck_purga_da_demonstracao_contagens",
                table: "purga_da_demonstracao");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "encerrada_em",
                table: "usuario",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "coordenacoes",
                table: "purga_da_demonstracao",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "professores",
                table: "purga_da_demonstracao",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "ck_usuario_demonstracao",
                table: "usuario",
                sql: "(lote_da_demonstracao IS NULL AND hash_da_chave_do_dispositivo IS NULL AND encerrada_em IS NULL) OR (lote_da_demonstracao IS NOT NULL AND hash_da_senha IS NULL AND papeis IN (1, 2, 4) AND ((hash_da_chave_do_dispositivo IS NOT NULL AND encerrada_em IS NULL) OR (hash_da_chave_do_dispositivo IS NULL AND encerrada_em IS NOT NULL AND papeis <> 1)))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_purga_da_demonstracao_contagens",
                table: "purga_da_demonstracao",
                sql: "alunos >= 0 AND professores >= 0 AND coordenacoes >= 0 AND alunos + professores + coordenacoes >= 1 AND fotos >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_usuario_demonstracao",
                table: "usuario");

            migrationBuilder.DropCheckConstraint(
                name: "ck_purga_da_demonstracao_contagens",
                table: "purga_da_demonstracao");

            migrationBuilder.DropColumn(
                name: "encerrada_em",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "coordenacoes",
                table: "purga_da_demonstracao");

            migrationBuilder.DropColumn(
                name: "professores",
                table: "purga_da_demonstracao");

            migrationBuilder.AddCheckConstraint(
                name: "ck_usuario_demonstracao",
                table: "usuario",
                sql: "(lote_da_demonstracao IS NULL AND hash_da_chave_do_dispositivo IS NULL) OR (lote_da_demonstracao IS NOT NULL AND hash_da_chave_do_dispositivo IS NOT NULL AND papeis = 1 AND hash_da_senha IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_purga_da_demonstracao_contagens",
                table: "purga_da_demonstracao",
                sql: "alunos >= 1 AND fotos >= 0");
        }
    }
}
