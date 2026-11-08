using Maxx.Application.Paginacao;
using Maxx.Application.Social;
using Maxx.Domain.Identidade;
using Maxx.Domain.Social;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.Social;

internal sealed class CurtidaRepository(MaxxDbContext db) : ICurtidaRepository
{
    public Task<bool> ExisteAsync(Guid postId, Guid usuarioId, CancellationToken cancellationToken) =>
        db.Set<Curtida>().AnyAsync(curtida => curtida.PostId == postId && curtida.UsuarioId == usuarioId, cancellationToken);

    public void Adicionar(Curtida curtida) => db.Add(curtida);

    public Task RemoverAsync(Guid postId, Guid usuarioId, CancellationToken cancellationToken) =>
        db.Set<Curtida>().Where(curtida => curtida.PostId == postId && curtida.UsuarioId == usuarioId).ExecuteDeleteAsync(cancellationToken);
}

internal sealed class ComentarioRepository(MaxxDbContext db) : IComentarioRepository
{
    public void Adicionar(Comentario comentario) => db.Add(comentario);

    public async Task<Comentario?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Set<Comentario>().FindAsync([id], cancellationToken);

    public void Apagar(Comentario comentario) => db.Remove(comentario);
}

internal sealed class SeguimentoRepository(MaxxDbContext db) : ISeguimentoRepository
{
    public Task<bool> ExisteAsync(Guid seguidorId, Guid seguidoId, CancellationToken cancellationToken) =>
        db.Set<Seguimento>().AnyAsync(seguimento => seguimento.SeguidorId == seguidorId && seguimento.SeguidoId == seguidoId, cancellationToken);

    public void Adicionar(Seguimento seguimento) => db.Add(seguimento);

    public Task RemoverAsync(Guid seguidorId, Guid seguidoId, CancellationToken cancellationToken) =>
        db.Set<Seguimento>()
            .Where(seguimento => seguimento.SeguidorId == seguidorId && seguimento.SeguidoId == seguidoId)
            .ExecuteDeleteAsync(cancellationToken);
}

internal sealed class ConsultaSocial(MaxxDbContext db) : IConsultaSocial
{
    public async Task<EstadoDaCurtida> CurtidasAsync(Guid postId, Guid leitorId, CancellationToken cancellationToken)
    {
        var curtidas = db.Set<Curtida>().Where(curtida => curtida.PostId == postId);
        return new EstadoDaCurtida(
            await curtidas.CountAsync(cancellationToken),
            await curtidas.AnyAsync(curtida => curtida.UsuarioId == leitorId, cancellationToken));
    }

    public async Task<Pagina<ComentarioLido>> ComentariosAsync(
        Guid postId, PosicaoNoTempo? depoisDe, int limite, CancellationToken cancellationToken)
    {
        var comentarios = db.Set<Comentario>().Where(comentario => comentario.PostId == postId && comentario.RemovidoEm == null);
        if (depoisDe is { } posicao)
        {
            // Comparação de linha do Postgres, (criado_em, id) > (instante, id): anda pelo
            // índice e desempata pelo id na ordem do próprio banco, não na do Guid do .NET.
            comentarios = comentarios.Where(comentario =>
                EF.Functions.GreaterThan(ValueTuple.Create(comentario.CriadoEm, comentario.Id), ValueTuple.Create(posicao.Instante, posicao.Id)));
        }

        // Um a mais que o limite: se ele vier, existe página seguinte.
        var linhas = await comentarios
            .Join(db.Set<Usuario>(), comentario => comentario.AutorId, autor => autor.Id, (comentario, autor) => new
            {
                comentario.Id,
                comentario.Texto,
                comentario.CriadoEm,
                AutorId = autor.Id,
                autor.NomeDeExibicao,
                autor.Prefixo,
                autor.Sufixo,
            })
            .OrderBy(linha => linha.CriadoEm)
            .ThenBy(linha => linha.Id)
            .Take(limite + 1)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var pagina = linhas.Take(limite).ToList();
        return new Pagina<ComentarioLido>(
            [.. pagina.Select(linha => new ComentarioLido(
                linha.Id,
                new AutorDoComentario(linha.AutorId, linha.NomeDeExibicao, new Arroba(linha.Prefixo, linha.Sufixo).ToString()),
                linha.Texto,
                linha.CriadoEm))],
            linhas.Count > limite ? CursorDeTempo.Codificar(new PosicaoNoTempo(pagina[^1].CriadoEm, pagina[^1].Id)) : null);
    }
}
