using Maxx.Application.Notificacoes;
using Maxx.Domain.Notificacoes;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.Notificacoes;

internal sealed class NotificacaoRepository(MaxxDbContext db) : INotificacaoRepository
{
    public void Adicionar(Notificacao notificacao) => db.Add(notificacao);

    public Task<bool> ExisteAsync(Guid usuarioId, TipoDeNotificacao tipo, Guid referenciaId, CancellationToken cancellationToken) =>
        db.Set<Notificacao>().AnyAsync(
            notificacao => notificacao.UsuarioId == usuarioId && notificacao.Tipo == tipo && notificacao.ReferenciaId == referenciaId,
            cancellationToken);
}
