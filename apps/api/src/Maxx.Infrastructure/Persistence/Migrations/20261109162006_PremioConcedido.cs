using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maxx.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PremioConcedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notificacao_tipo",
                table: "notificacao");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notificacao_tipo",
                table: "notificacao",
                sql: "tipo IN ('medalhaRecebida', 'comprovacaoAprovada', 'comprovacaoReprovada', 'contestacaoAberta', 'decisaoDeContestacao', 'premioRevogado', 'conquistaObtida', 'conquistaRemovida', 'avisoNovo', 'premioConcedido')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notificacao_tipo",
                table: "notificacao");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notificacao_tipo",
                table: "notificacao",
                sql: "tipo IN ('medalhaRecebida', 'comprovacaoAprovada', 'comprovacaoReprovada', 'contestacaoAberta', 'decisaoDeContestacao', 'premioRevogado', 'conquistaObtida', 'conquistaRemovida', 'avisoNovo')");
        }
    }
}
