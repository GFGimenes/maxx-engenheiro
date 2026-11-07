using Maxx.Application.Medalhas;
using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.Medalhas;

internal sealed class MedalhaRepository(MaxxDbContext db) : IMedalhaRepository
{
    private static readonly string _tipoMedalha = NomeJson.De(TipoDeItem.Medalha);

    public void Adicionar(Medalha medalha) => db.Add(medalha);

    public Task<Medalha?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<Medalha>().SingleOrDefaultAsync(medalha => medalha.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Medalha>> DoAlunoNoItemAsync(Guid alunoId, string itemCodigo, CancellationToken cancellationToken) =>
        await db.Set<Medalha>()
            .Where(medalha => medalha.AlunoId == alunoId && medalha.ItemCodigo == itemCodigo)
            .ToListAsync(cancellationToken);

    public async Task<ItemDeMedalha?> ItemAsync(string codigo, CancellationToken cancellationToken)
    {
        var row = await db.Set<ItemCatalogoRow>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Codigo == codigo && item.Tipo == _tipoMedalha, cancellationToken);
        return row is null ? null : new ItemDeMedalha(CatalogoRowMapper.ParaMedalha(row), row.Versao);
    }

    public async Task<TurmaDaConcessao?> TurmaAsync(Guid turmaId, CancellationToken cancellationToken)
    {
        var turma = await db.Set<Turma>().Include(turma => turma.Materia)
            .SingleOrDefaultAsync(turma => turma.Id == turmaId, cancellationToken);
        if (turma is null)
        {
            return null;
        }

        var semestre = await db.Set<SemestreLetivo>().Include(semestre => semestre.Bimestres)
            .SingleAsync(semestre => semestre.Id == turma.SemestreLetivoId, cancellationToken);
        return new TurmaDaConcessao(turma, semestre);
    }

    public async Task<AlunoDaConcessao?> AlunoMatriculadoAsync(Guid turmaId, Guid alunoId, CancellationToken cancellationToken)
    {
        var encontrado = await (
                from usuario in db.Set<Usuario>()
                join cadastro in db.Set<CadastroElegivel>() on usuario.CadastroElegivelId equals cadastro.Id
                join curso in db.Set<Curso>() on cadastro.CursoId equals curso.Id
                where usuario.Id == alunoId
                    && usuario.Papeis == Papeis.Aluno
                    && db.Set<Matricula>().Any(matricula => matricula.AlunoId == cadastro.Id && matricula.TurmaId == turmaId)
                select new { usuario, curso.Sigla })
            .SingleOrDefaultAsync(cancellationToken);
        return encontrado is null ? null : new AlunoDaConcessao(encontrado.usuario, encontrado.Sigla);
    }
}
