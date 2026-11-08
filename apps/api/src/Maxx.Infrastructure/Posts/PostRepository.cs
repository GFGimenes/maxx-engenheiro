using Maxx.Application.Posts;
using Maxx.Application.Premios;
using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Domain.Social;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.RaridadeDosItens;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.Posts;

internal sealed class PostRepository(MaxxDbContext db) : IPostRepository
{
    public void Adicionar(Post post) => db.Add(post);

    public async Task<Post?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Set<Post>().FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<Post>> DoPremioAsync(Guid medalhaId, CancellationToken cancellationToken) =>
        await db.Set<Post>().Where(post => post.MedalhaId == medalhaId).ToListAsync(cancellationToken);

    public Task<bool> PremioTemPostAsync(Guid medalhaId, CancellationToken cancellationToken) =>
        db.Set<Post>().AnyAsync(post => post.MedalhaId == medalhaId && post.RemovidoEm == null, cancellationToken);

    public void Apagar(Post post) => db.Remove(post);
}

internal sealed class ConsultaDePosts(MaxxDbContext db) : IConsultaDePosts
{
    public async Task<PostLido?> PostAsync(Guid id, Guid leitorId, CancellationToken cancellationToken) =>
        (await PostsAsync([id], leitorId, cancellationToken)).GetValueOrDefault(id);

    // Duas consultas para a página inteira, não duas por post: os posts com autor e
    // contagens, depois as medalhas que eles citam.
    public async Task<IReadOnlyDictionary<Guid, PostLido>> PostsAsync(
        IReadOnlyCollection<Guid> ids, Guid leitorId, CancellationToken cancellationToken)
    {
        var linhas = await (
                from post in db.Set<Post>()
                join autor in db.Set<Usuario>() on post.AutorId equals autor.Id
                join cadastro in db.Set<CadastroElegivel>() on autor.CadastroElegivelId equals cadastro.Id
                join curso in db.Set<Curso>() on cadastro.CursoId equals curso.Id into cursos
                from curso in cursos.DefaultIfEmpty()
                where ids.Contains(post.Id) && post.RemovidoEm == null
                select new
                {
                    post.Id,
                    post.Legenda,
                    post.ChaveDaFotoDoFeed,
                    post.ChaveDaFotoDaGrade,
                    post.MedalhaId,
                    post.CriadoEm,
                    AutorId = autor.Id,
                    autor.NomeDeExibicao,
                    autor.Prefixo,
                    autor.Sufixo,
                    Curso = curso == null ? null : curso.Sigla,
                    cadastro.Semestre,
                    Curtidas = db.Set<Curtida>().Count(curtida => curtida.PostId == post.Id),
                    Comentarios = db.Set<Comentario>().Count(comentario => comentario.PostId == post.Id && comentario.RemovidoEm == null),
                    Curtiu = db.Set<Curtida>().Any(curtida => curtida.PostId == post.Id && curtida.UsuarioId == leitorId),
                })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var medalhas = await MedalhasAsync([.. linhas.Where(linha => linha.MedalhaId is not null).Select(linha => linha.MedalhaId!.Value)], cancellationToken);

        return linhas.ToDictionary(
            linha => linha.Id,
            linha => new PostLido(
                linha.Id,
                new AutorDoPost(linha.AutorId, linha.NomeDeExibicao, new Arroba(linha.Prefixo, linha.Sufixo).ToString(), linha.Curso, linha.Semestre),
                linha.Legenda,
                linha.ChaveDaFotoDoFeed,
                linha.ChaveDaFotoDaGrade,
                linha.MedalhaId is { } medalhaId ? medalhas.GetValueOrDefault(medalhaId) : null,
                linha.CriadoEm,
                linha.Curtidas,
                linha.Comentarios,
                linha.Curtiu));
    }

    private async Task<Dictionary<Guid, PremioDoPost>> MedalhasAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var medalhas = await (
                from linha in db.Set<Medalha>()
                join item in db.Set<ItemCatalogoRow>() on linha.ItemCodigo equals item.Codigo
                join materia in db.Set<Materia>() on linha.MateriaId equals materia.Id
                join bimestre in db.Set<Bimestre>() on linha.BimestreId equals bimestre.Id
                join raridade in db.Set<RaridadeDoItemRow>() on linha.ItemCodigo equals raridade.ItemCodigo into raridades
                from raridade in raridades.DefaultIfEmpty()
                where ids.Contains(linha.Id)
                select new
                {
                    linha.Id,
                    linha.ItemCodigo,
                    item.Nome,
                    linha.Tier,
                    linha.Pontos,
                    Materia = materia.Nome,
                    bimestre.Numero,
                    Raridade = raridade == null ? null : raridade.Raridade,
                    AlunosComItem = raridade == null ? 0 : raridade.AlunosComItem,
                    AlunosElegiveis = raridade == null ? 0 : raridade.AlunosElegiveis,
                })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return medalhas.ToDictionary(
            medalha => medalha.Id,
            medalha => new PremioDoPost(
                TipoDeItem.Medalha,
                medalha.Id,
                medalha.ItemCodigo,
                medalha.Nome,
                medalha.Tier,
                medalha.Pontos,
                medalha.Raridade is { } nivel && NomeJson.TentarLer<Raridade>(nivel, out var lida)
                    ? new RaridadeAtual(lida, medalha.AlunosComItem, medalha.AlunosElegiveis)
                    : null,
                medalha.Materia,
                medalha.Numero));
    }
}
