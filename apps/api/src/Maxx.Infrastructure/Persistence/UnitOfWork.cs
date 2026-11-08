using Maxx.Application;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Maxx.Infrastructure.Persistence;

// Grava o que o escopo acumulou. Unicidade violada vira ConflitoDeUnicidadeException com
// o nome da regra, para a Application responder com o código certo sem conhecer o banco.
internal sealed class UnitOfWork(MaxxDbContext db) : IUnitOfWork
{
    private static readonly Dictionary<string, Unicidade> _porIndice = new(StringComparer.Ordinal)
    {
        ["ix_usuario_cadastro_elegivel_id"] = Unicidade.ContaPorCadastro,
        ["pk_prefixo_reservado"] = Unicidade.Prefixo,
        ["ix_medalha_chave_ativa"] = Unicidade.MedalhaAtiva,
        ["ix_post_medalha_ativa"] = Unicidade.PostPorPremio,
        ["ix_post_premio_participacao_ativo"] = Unicidade.PostPorPremio,
        ["ix_premio_participacao_chave_ativa"] = Unicidade.PremioParticipacaoAtivo,
        ["ix_contestacao_premio_participacao_id"] = Unicidade.ContestacaoPorPremio,
        ["pk_curtida"] = Unicidade.Curtida,
        ["pk_seguimento"] = Unicidade.Seguimento,
    };

    public async Task SalvarAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException falha) when (falha.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        } postgres)
        {
            throw new ConflitoDeUnicidadeException(
                _porIndice.GetValueOrDefault(postgres.ConstraintName ?? string.Empty, Unicidade.Outra), falha);
        }
        catch (DbUpdateConcurrencyException falha)
        {
            throw new ConflitoDeConcorrenciaException("A linha mudou desde a leitura.", falha);
        }
    }

    public void Descartar() => db.ChangeTracker.Clear();
}
