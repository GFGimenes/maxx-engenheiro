using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrofeusVistos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "trofeus_visto_em",
                table: "usuario",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "trofeus_visto_em",
                table: "usuario");
        }
    }
}
