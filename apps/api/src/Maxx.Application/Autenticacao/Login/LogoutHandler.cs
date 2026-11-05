using Maxx.Domain.Identidade;

namespace Maxx.Application.Autenticacao.Login;

// O logout revoga a família do refresh apresentado: o refresh atual e os que já foram
// trocados deixam de valer. Idempotente, e calado sobre refresh desconhecido.
public sealed class LogoutHandler(
    IFamiliaDeSessaoRepository familias, ITokenGenerator tokens, IUnitOfWork unitOfWork, TimeProvider relogio)
{
    public async Task SairAsync(string refresh, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refresh);

        var familia = await familias.BuscarPorRefreshAsync(tokens.Hash(refresh), cancellationToken);
        if (familia is null)
        {
            return;
        }

        familia.Revogar(MotivoDaRevogacao.Logout, relogio.GetUtcNow());
        await unitOfWork.SalvarAsync(cancellationToken);
    }
}
