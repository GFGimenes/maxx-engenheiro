using Maxx.Application.Autorizacao;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.Autorizacao;

// Uma consulta só: vínculo do professor (pela linha de cadastro do usuário) com a turma, e
// a turma num semestre cujos bimestres cobrem a data de hoje em São Paulo.
internal sealed class EscopoDeTurma(MaxxDbContext db, TimeProvider relogio) : IEscopoDeTurma
{
    public Task<bool> ProfessorLecionaAsync(Guid usuarioId, Guid turmaId, CancellationToken cancellationToken)
    {
        var hoje = FusoHorario.DataLocal(relogio.GetUtcNow());

        // O semestre vai do início do primeiro bimestre ao fim do último (SemestreLetivo).
        var semestresVigentes = db.Set<Bimestre>()
            .GroupBy(bimestre => EF.Property<Guid>(bimestre, "SemestreLetivoId"))
            .Where(semestre => semestre.Min(bimestre => bimestre.Inicio) <= hoje && semestre.Max(bimestre => bimestre.Fim) >= hoje)
            .Select(semestre => semestre.Key);

        return db.Set<VinculoProfessor>()
            .Where(vinculo => vinculo.TurmaId == turmaId)
            .Join(db.Set<Usuario>(), vinculo => vinculo.ProfessorId, usuario => usuario.CadastroElegivelId, (vinculo, usuario) => new { vinculo, usuario })
            .Where(par => par.usuario.Id == usuarioId)
            .Join(db.Set<Turma>(), par => par.vinculo.TurmaId, turma => turma.Id, (_, turma) => turma)
            .AnyAsync(turma => semestresVigentes.Contains(turma.SemestreLetivoId), cancellationToken);
    }
}
