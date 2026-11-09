using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FechamentoDePeriodo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fechamento_de_periodo",
                columns: table => new
                {
                    tipo_de_periodo = table.Column<string>(type: "text", nullable: false),
                    referencia_do_periodo = table.Column<Guid>(type: "uuid", nullable: false),
                    fechado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fechamento_de_periodo", x => new { x.tipo_de_periodo, x.referencia_do_periodo });
                    table.CheckConstraint("ck_fechamento_de_periodo_tipo_de_periodo", "tipo_de_periodo IN ('bimestre', 'semestre', 'ano')");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fechamento_de_periodo");
        }
    }
}
