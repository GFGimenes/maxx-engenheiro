using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Catalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "familia",
                columns: table => new
                {
                    codigo = table.Column<string>(type: "text", nullable: false),
                    nome = table.Column<string>(type: "text", nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: false),
                    passos = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_familia", x => x.codigo);
                    table.CheckConstraint("ck_familia_passos", "passos >= 1");
                });

            migrationBuilder.CreateTable(
                name: "item_catalogo",
                columns: table => new
                {
                    codigo = table.Column<string>(type: "text", nullable: false),
                    tipo = table.Column<string>(type: "text", nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    nome = table.Column<string>(type: "text", nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: false),
                    tier = table.Column<string>(type: "text", nullable: false),
                    pontos = table.Column<int>(type: "integer", nullable: false),
                    periodo = table.Column<string>(type: "text", nullable: false),
                    familia_codigo = table.Column<string>(type: "text", nullable: true),
                    ordem_na_familia = table.Column<int>(type: "integer", nullable: true),
                    restricao_curso = table.Column<string>(type: "text", nullable: true),
                    vigencia_inicio = table.Column<DateOnly>(type: "date", nullable: true),
                    vigencia_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    chave_de_deduplicacao = table.Column<string[]>(type: "text[]", nullable: true),
                    @base = table.Column<string>(name: "base", type: "text", nullable: true),
                    repetivel = table.Column<bool>(type: "boolean", nullable: true),
                    limite_por_periodo = table.Column<int>(type: "integer", nullable: true),
                    tipo_de_evento = table.Column<string>(type: "text", nullable: true),
                    forma_do_evento = table.Column<string>(type: "text", nullable: true),
                    exige_foto = table.Column<bool>(type: "boolean", nullable: true),
                    gatilho = table.Column<string>(type: "text", nullable: true),
                    regra = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_catalogo", x => x.codigo);
                    table.CheckConstraint("ck_item_catalogo_campos_por_tipo", "(tipo = 'medalha' AND base IS NOT NULL AND repetivel IS NOT NULL AND limite_por_periodo IS NOT NULL AND chave_de_deduplicacao IS NOT NULL AND tipo_de_evento IS NULL AND forma_do_evento IS NULL AND exige_foto IS NULL AND gatilho IS NULL AND regra IS NULL) OR (tipo = 'premioParticipacao' AND tipo_de_evento IS NOT NULL AND forma_do_evento IS NOT NULL AND exige_foto IS NOT NULL AND chave_de_deduplicacao IS NOT NULL AND base IS NULL AND repetivel IS NULL AND limite_por_periodo IS NULL AND gatilho IS NULL AND regra IS NULL) OR (tipo = 'conquista' AND gatilho IS NOT NULL AND regra IS NOT NULL AND base IS NULL AND repetivel IS NULL AND limite_por_periodo IS NULL AND tipo_de_evento IS NULL AND forma_do_evento IS NULL AND exige_foto IS NULL AND chave_de_deduplicacao IS NULL)");
                    table.CheckConstraint("ck_item_catalogo_familia", "(familia_codigo IS NULL) = (ordem_na_familia IS NULL)");
                    table.CheckConstraint("ck_item_catalogo_pontos_na_faixa", "(tier = 'bronze' AND pontos BETWEEN 1 AND 30) OR (tier = 'prata' AND pontos BETWEEN 31 AND 70) OR (tier = 'ouro' AND pontos BETWEEN 71 AND 100) OR (tier = 'diamante' AND pontos BETWEEN 101 AND 150)");
                    table.CheckConstraint("ck_item_catalogo_tier", "tier IN ('bronze', 'prata', 'ouro', 'diamante')");
                    table.CheckConstraint("ck_item_catalogo_tipo", "tipo IN ('medalha', 'premioParticipacao', 'conquista')");
                    table.CheckConstraint("ck_item_catalogo_versao", "versao >= 1");
                    table.CheckConstraint("ck_item_catalogo_vigencia", "vigencia_inicio IS NULL OR vigencia_fim IS NULL OR vigencia_fim >= vigencia_inicio");
                    table.ForeignKey(
                        name: "fk_item_catalogo_familia_familia_codigo",
                        column: x => x.familia_codigo,
                        principalTable: "familia",
                        principalColumn: "codigo",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_item_catalogo_familia_codigo_ordem_na_familia",
                table: "item_catalogo",
                columns: new[] { "familia_codigo", "ordem_na_familia" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "item_catalogo");

            migrationBuilder.DropTable(
                name: "familia");
        }
    }
}
