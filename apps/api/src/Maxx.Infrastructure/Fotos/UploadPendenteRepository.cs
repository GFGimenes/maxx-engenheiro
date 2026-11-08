using Maxx.Application.Fotos;
using Maxx.Domain.Fotos;
using Maxx.Infrastructure.Persistence;

namespace Maxx.Infrastructure.Fotos;

internal sealed class UploadPendenteRepository(MaxxDbContext db) : IUploadPendenteRepository
{
    public void Adicionar(UploadPendente upload) => db.Add(upload);

    public async Task<UploadPendente?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Set<UploadPendente>().FindAsync([id], cancellationToken);

    public void Remover(UploadPendente upload) => db.Remove(upload);
}
