using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Social : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "comentario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    autor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    texto = table.Column<string>(type: "text", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    removido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removido_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comentario", x => x.id);
                    table.CheckConstraint("ck_comentario_remocao", "(removido_em IS NULL) = (removido_por_id IS NULL)");
                    table.CheckConstraint("ck_comentario_texto", "length(texto) > 0");
                    table.ForeignKey(
                        name: "fk_comentario_post_post_id",
                        column: x => x.post_id,
                        principalTable: "post",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_comentario_usuario_autor_id",
                        column: x => x.autor_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_comentario_usuario_removido_por_id",
                        column: x => x.removido_por_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "curtida",
                columns: table => new
                {
                    post_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_curtida", x => new { x.post_id, x.usuario_id });
                    table.ForeignKey(
                        name: "fk_curtida_post_post_id",
                        column: x => x.post_id,
                        principalTable: "post",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_curtida_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "seguimento",
                columns: table => new
                {
                    seguidor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seguido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seguimento", x => new { x.seguidor_id, x.seguido_id });
                    table.CheckConstraint("ck_seguimento_outro", "seguidor_id <> seguido_id");
                    table.ForeignKey(
                        name: "fk_seguimento_usuario_seguido_id",
                        column: x => x.seguido_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_seguimento_usuario_seguidor_id",
                        column: x => x.seguidor_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_comentario_autor_id",
                table: "comentario",
                column: "autor_id");

            migrationBuilder.CreateIndex(
                name: "ix_comentario_post_id_criado_em_id",
                table: "comentario",
                columns: new[] { "post_id", "criado_em", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_comentario_removido_por_id",
                table: "comentario",
                column: "removido_por_id");

            migrationBuilder.CreateIndex(
                name: "ix_curtida_usuario_id",
                table: "curtida",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_seguimento_seguido_id",
                table: "seguimento",
                column: "seguido_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comentario");

            migrationBuilder.DropTable(
                name: "curtida");

            migrationBuilder.DropTable(
                name: "seguimento");
        }
    }
}
