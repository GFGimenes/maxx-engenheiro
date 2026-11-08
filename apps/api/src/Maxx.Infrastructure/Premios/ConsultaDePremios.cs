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

namespace Maxx.Infrastructure.Premios;

internal sealed class ConsultaDePremios(MaxxDbContext db) : IConsultaDePremios
{
    public async Task<PremiosDoAluno?> DoAlunoAsync(Guid alunoId, CancellationToken cancellationToken)
    {
        var aluno = await (
                from usuario in db.Set<Usuario>()
                join cadastro in db.Set<CadastroElegivel>() on usuario.CadastroElegivelId equals cadastro.Id
                join curso in db.Set<Curso>() on cadastro.CursoId equals curso.Id
                where usuario.Id == alunoId && usuario.Papeis == Papeis.Aluno
                select new { curso.Sigla })
            .SingleOrDefaultAsync(cancellationToken);
        if (aluno is null)
        {
            return null;
        }

        var medalhas = await (
                from medalha in db.Set<Medalha>()
                join materia in db.Set<Materia>() on medalha.MateriaId equals materia.Id
                join bimestre in db.Set<Bimestre>() on medalha.BimestreId equals bimestre.Id
                where medalha.AlunoId == alunoId && medalha.AnuladaEm == null
                orderby medalha.ConcedidaEm descending
                select new
                {
                    medalha.Id,
                    medalha.ItemCodigo,
                    medalha.Tier,
                    medalha.Pontos,
                    Materia = materia.Nome,
                    bimestre.Numero,
                    medalha.ConcedidaEm,
                    PostId = db.Set<Post>().Where(post => post.MedalhaId == medalha.Id && post.RemovidoEm == null).Select(post => (Guid?)post.Id).FirstOrDefault(),
                })
            .ToListAsync(cancellationToken);
        var obtidos = medalhas.ToLookup(
            medalha => medalha.ItemCodigo,
            medalha => new PremioObtido(medalha.Id, medalha.Tier, medalha.Pontos, medalha.Materia, medalha.Numero, medalha.ConcedidaEm, medalha.PostId));

        var raridades = await db.Set<RaridadeDoItemRow>().AsNoTracking()
            .ToDictionaryAsync(linha => linha.ItemCodigo, cancellationToken);
        var itens = await db.Set<ItemCatalogoRow>().AsNoTracking().OrderBy(item => item.Codigo).ToListAsync(cancellationToken);
        var familias = await db.Set<FamiliaRow>().AsNoTracking().OrderBy(familia => familia.Codigo).ToListAsync(cancellationToken);

        return new PremiosDoAluno(
            medalhas.Sum(medalha => medalha.Pontos),
            [.. familias.Select(familia => new FamiliaDoCatalogo(familia.Codigo, familia.Nome, familia.Descricao, familia.Passos))],
            [.. itens
                .Where(item => item.RestricaoCurso is null
                    || string.Equals(item.RestricaoCurso, aluno.Sigla, StringComparison.OrdinalIgnoreCase)
                    || obtidos.Contains(item.Codigo))
                .Select(item => new ItemDoAluno(
                    item.Codigo,
                    Ler<TipoDeItem>(item.Tipo),
                    item.Nome,
                    item.Descricao,
                    Ler<Tier>(item.Tier),
                    item.Pontos,
                    item.FamiliaCodigo,
                    item.OrdemNaFamilia,
                    raridades.TryGetValue(item.Codigo, out var raridade)
                        ? new RaridadeAtual(Ler<Raridade>(raridade.Raridade), raridade.AlunosComItem, raridade.AlunosElegiveis)
                        : null,
                    [.. obtidos[item.Codigo]]))]);
    }

    private static T Ler<T>(string texto)
        where T : struct, Enum =>
        NomeJson.TentarLer<T>(texto, out var valor) ? valor : throw new InvalidOperationException($"{typeof(T).Name} desconhecido: {texto}.");
}
