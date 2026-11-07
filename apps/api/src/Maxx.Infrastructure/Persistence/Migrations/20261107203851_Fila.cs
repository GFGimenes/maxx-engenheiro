using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Fila : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trabalho",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    tipo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    carga = table.Column<string>(type: "jsonb", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tentativas = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    disponivel_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ultimo_erro = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trabalho", x => x.id);
                    table.CheckConstraint("ck_trabalho_estado", "estado IN ('pendente', 'falho')");
                    table.CheckConstraint("ck_trabalho_tentativas", "tentativas BETWEEN 0 AND 5");
                });

            migrationBuilder.CreateIndex(
                name: "ix_trabalho_pendente",
                table: "trabalho",
                columns: new[] { "disponivel_em", "id" },
                filter: "estado = 'pendente'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trabalho");
        }
    }
}
