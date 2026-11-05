using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Identidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "usuario",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cadastro_elegivel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    papeis = table.Column<int>(type: "integer", nullable: false),
                    nome_de_exibicao = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    prefixo = table.Column<string>(type: "citext", nullable: false),
                    sufixo = table.Column<string>(type: "text", nullable: false),
                    hash_da_senha = table.Column<string>(type: "text", nullable: true),
                    ex_aluno_desde = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario", x => x.id);
                    table.CheckConstraint("ck_usuario_ex_aluno", "(ex_aluno_desde IS NULL AND sufixo <> 'EX') OR (ex_aluno_desde IS NOT NULL AND papeis = 1 AND sufixo = 'EX')");
                    table.CheckConstraint("ck_usuario_papeis", "papeis IN (1, 2, 4, 6)");
                    table.CheckConstraint("ck_usuario_prefixo", "prefixo ~ '^[A-Za-z][A-Za-z0-9]{2,29}$'");
                    table.ForeignKey(
                        name: "fk_usuario_cadastro_elegivel_cadastro_elegivel_id",
                        column: x => x.cadastro_elegivel_id,
                        principalTable: "cadastro_elegivel",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "familia_sessao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revogada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    motivo_da_revogacao = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_familia_sessao", x => x.id);
                    table.CheckConstraint("ck_familia_sessao_revogacao", "(revogada_em IS NULL AND motivo_da_revogacao IS NULL) OR (revogada_em IS NOT NULL AND motivo_da_revogacao IN (1, 2, 3))");
                    table.ForeignKey(
                        name: "fk_familia_sessao_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prefixo_reservado",
                columns: table => new
                {
                    prefixo = table.Column<string>(type: "citext", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reservado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prefixo_reservado", x => x.prefixo);
                    table.CheckConstraint("ck_prefixo_reservado_prefixo", "prefixo ~ '^[A-Za-z][A-Za-z0-9]{2,29}$'");
                    table.ForeignKey(
                        name: "fk_prefixo_reservado_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recuperacao_senha",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hash = table.Column<string>(type: "text", nullable: false),
                    solicitada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    usada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    invalidada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recuperacao_senha", x => x.id);
                    table.CheckConstraint("ck_recuperacao_senha_encerramento", "usada_em IS NULL OR invalidada_em IS NULL");
                    table.CheckConstraint("ck_recuperacao_senha_validade", "expira_em > solicitada_em");
                    table.ForeignKey(
                        name: "fk_recuperacao_senha_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_token",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    familia_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hash = table.Column<string>(type: "text", nullable: false),
                    emitido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    rotacionado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_token", x => x.id);
                    table.CheckConstraint("ck_refresh_token_validade", "expira_em > emitido_em");
                    table.ForeignKey(
                        name: "fk_refresh_token_familia_sessao_familia_id",
                        column: x => x.familia_id,
                        principalTable: "familia_sessao",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_familia_sessao_usuario_id",
                table: "familia_sessao",
                column: "usuario_id",
                filter: "revogada_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_prefixo_reservado_usuario_id",
                table: "prefixo_reservado",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_recuperacao_senha_hash",
                table: "recuperacao_senha",
                column: "hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recuperacao_senha_usuario_id",
                table: "recuperacao_senha",
                column: "usuario_id",
                filter: "usada_em IS NULL AND invalidada_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_familia_id",
                table: "refresh_token",
                column: "familia_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_token_hash",
                table: "refresh_token",
                column: "hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuario_cadastro_elegivel_id",
                table: "usuario",
                column: "cadastro_elegivel_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prefixo_reservado");

            migrationBuilder.DropTable(
                name: "recuperacao_senha");

            migrationBuilder.DropTable(
                name: "refresh_token");

            migrationBuilder.DropTable(
                name: "familia_sessao");

            migrationBuilder.DropTable(
                name: "usuario");
        }
    }
}
