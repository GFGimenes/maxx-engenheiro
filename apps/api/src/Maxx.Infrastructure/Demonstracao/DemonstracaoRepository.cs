using Maxx.Application.Demonstracao;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.Demonstracao;

internal sealed class DemonstracaoRepository(MaxxDbContext db) : IDemonstracaoRepository
{
    public async Task<IReadOnlyList<Curso>> CursosComTurmaVigenteAsync(DateOnly hoje, CancellationToken cancellationToken)
    {
        var vigentes = SemestreVigente.Ids(db, hoje);
        return await db.Set<Curso>()
            .Where(curso => db.Set<Turma>().Any(turma => turma.Materia.CursoId == curso.Id && vigentes.Contains(turma.SemestreLetivoId)))
            .OrderBy(curso => curso.Nome)
            .ToListAsync(cancellationToken);
    }

    public async Task<(Curso Curso, IReadOnlyList<Turma> Turmas)?> TurmasVigentesAsync(
        string sigla, DateOnly hoje, CancellationToken cancellationToken)
    {
        var curso = await db.Set<Curso>().SingleOrDefaultAsync(curso => curso.Sigla == sigla, cancellationToken);
        if (curso is null)
        {
            return null;
        }

        var vigentes = SemestreVigente.Ids(db, hoje);
        var turmas = await db.Set<Turma>()
            .Include(turma => turma.Materia)
            .Where(turma => turma.Materia.CursoId == curso.Id && vigentes.Contains(turma.SemestreLetivoId))
            .ToListAsync(cancellationToken);

        return turmas.Count == 0 ? null : (curso, turmas);
    }

    public Task<Usuario?> BuscarPorChaveAsync(string hashDaChave, CancellationToken cancellationToken) =>
        db.Set<Usuario>().SingleOrDefaultAsync(usuario => usuario.HashDaChaveDoDispositivo == hashDaChave, cancellationToken);

    public void Adicionar(CadastroElegivel cadastro) => db.Add(cadastro);

    public void Adicionar(Matricula matricula) => db.Add(matricula);
}
