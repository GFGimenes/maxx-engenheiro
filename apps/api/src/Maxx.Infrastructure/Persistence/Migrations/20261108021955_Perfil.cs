using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Perfil : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "chave_da_foto_da_grade",
                table: "usuario",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "chave_da_foto_do_feed",
                table: "usuario",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_usuario_foto_completa",
                table: "usuario",
                sql: "(chave_da_foto_do_feed IS NULL) = (chave_da_foto_da_grade IS NULL)");

            // A busca de alunos (produto.md 6.4): nome sem acento e sem maiúsculas, por trecho.
            // O unaccent() não é IMMUTABLE, porque depende do dicionário configurado; a função
            // fixa o dicionário pelo nome e pode, então, entrar num índice. Os dois índices de
            // trigramas atendem LIKE '%termo%' no nome e no prefixo do arroba.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql("""
                CREATE FUNCTION maxx_sem_acento(texto text) RETURNS text
                LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT
                RETURN public.unaccent('public.unaccent'::regdictionary, texto);
                """);
            migrationBuilder.Sql(
                "CREATE INDEX ix_usuario_nome_sem_acento ON usuario USING gin (maxx_sem_acento(lower(nome_de_exibicao)) gin_trgm_ops);");
            migrationBuilder.Sql("CREATE INDEX ix_usuario_prefixo_trigrama ON usuario USING gin (lower(prefixo::text) gin_trgm_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX ix_usuario_prefixo_trigrama;");
            migrationBuilder.Sql("DROP INDEX ix_usuario_nome_sem_acento;");
            migrationBuilder.Sql("DROP FUNCTION maxx_sem_acento(text);");

            migrationBuilder.DropCheckConstraint(
                name: "ck_usuario_foto_completa",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "chave_da_foto_da_grade",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "chave_da_foto_do_feed",
                table: "usuario");
        }
    }
}
