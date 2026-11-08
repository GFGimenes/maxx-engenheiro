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
    public async Task<PostLido?> PostAsync(Guid id, CancellationToken cancellationToken)
    {
        var linha = await (
                from post in db.Set<Post>()
                join autor in db.Set<Usuario>() on post.AutorId equals autor.Id
                join cadastro in db.Set<CadastroElegivel>() on autor.CadastroElegivelId equals cadastro.Id
                join curso in db.Set<Curso>() on cadastro.CursoId equals curso.Id into cursos
                from curso in cursos.DefaultIfEmpty()
                where post.Id == id && post.RemovidoEm == null
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
                })
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (linha is null)
        {
            return null;
        }

        return new PostLido(
            linha.Id,
            new AutorDoPost(linha.AutorId, linha.NomeDeExibicao, new Arroba(linha.Prefixo, linha.Sufixo).ToString(), linha.Curso, linha.Semestre),
            linha.Legenda,
            linha.ChaveDaFotoDoFeed,
            linha.ChaveDaFotoDaGrade,
            linha.MedalhaId is { } medalhaId ? await MedalhaAsync(medalhaId, cancellationToken) : null,
            linha.CriadoEm);
    }

    private async Task<PremioDoPost?> MedalhaAsync(Guid medalhaId, CancellationToken cancellationToken)
    {
        var medalha = await (
                from linha in db.Set<Medalha>()
                join item in db.Set<ItemCatalogoRow>() on linha.ItemCodigo equals item.Codigo
                join materia in db.Set<Materia>() on linha.MateriaId equals materia.Id
                join bimestre in db.Set<Bimestre>() on linha.BimestreId equals bimestre.Id
                join raridade in db.Set<RaridadeDoItemRow>() on linha.ItemCodigo equals raridade.ItemCodigo into raridades
                from raridade in raridades.DefaultIfEmpty()
                where linha.Id == medalhaId
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
            .SingleOrDefaultAsync(cancellationToken);

        return medalha is null
            ? null
            : new PremioDoPost(
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
                medalha.Numero);
    }
}
