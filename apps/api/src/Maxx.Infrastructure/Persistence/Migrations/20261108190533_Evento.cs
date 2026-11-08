using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Evento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    nome = table.Column<string>(type: "text", nullable: false),
                    inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fim = table.Column<DateOnly>(type: "date", nullable: false),
                    origem = table.Column<string>(type: "text", nullable: false),
                    itens_aceitos = table.Column<string[]>(type: "text[]", nullable: false),
                    curso_id = table.Column<Guid>(type: "uuid", nullable: true),
                    criado_por_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evento", x => x.id);
                    table.CheckConstraint("ck_evento_fim_depois_do_inicio", "fim >= inicio");
                    table.CheckConstraint("ck_evento_origem", "(origem = 'semeado' AND codigo IS NOT NULL AND cardinality(itens_aceitos) > 0 AND criado_por_id IS NULL) OR (origem = 'textoLivre' AND codigo IS NULL AND cardinality(itens_aceitos) = 0 AND curso_id IS NULL AND criado_por_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_evento_curso_curso_id",
                        column: x => x.curso_id,
                        principalTable: "curso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_evento_usuario_criado_por_id",
                        column: x => x.criado_por_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_evento_codigo",
                table: "evento",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evento_criado_por_id",
                table: "evento",
                column: "criado_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_evento_curso_id",
                table: "evento",
                column: "curso_id");

            migrationBuilder.CreateIndex(
                name: "ix_evento_origem_inicio",
                table: "evento",
                columns: new[] { "origem", "inicio" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evento");
        }
    }
}
