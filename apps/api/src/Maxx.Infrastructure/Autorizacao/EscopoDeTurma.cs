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

        var semestresVigentes = SemestreVigente.Ids(db, hoje);

        return db.Set<VinculoProfessor>()
            .Where(vinculo => vinculo.TurmaId == turmaId)
            .Join(db.Set<Usuario>(), vinculo => vinculo.ProfessorId, usuario => usuario.CadastroElegivelId, (vinculo, usuario) => new { vinculo, usuario })
            .Where(par => par.usuario.Id == usuarioId)
            .Join(db.Set<Turma>(), par => par.vinculo.TurmaId, turma => turma.Id, (_, turma) => turma)
            .AnyAsync(turma => semestresVigentes.Contains(turma.SemestreLetivoId), cancellationToken);
    }

    public Task<bool> ProfessorEnsinaAlunoAsync(Guid professorId, Guid alunoId, CancellationToken cancellationToken)
    {
        var hoje = FusoHorario.DataLocal(relogio.GetUtcNow());
        var semestresVigentes = SemestreVigente.Ids(db, hoje);
        var cadastroDoProfessor = db.Set<Usuario>().Where(usuario => usuario.Id == professorId).Select(usuario => usuario.CadastroElegivelId);
        var cadastroDoAluno = db.Set<Usuario>().Where(usuario => usuario.Id == alunoId).Select(usuario => usuario.CadastroElegivelId);

        return db.Set<VinculoProfessor>()
            .Where(vinculo => cadastroDoProfessor.Contains(vinculo.ProfessorId))
            .Join(db.Set<Matricula>(), vinculo => vinculo.TurmaId, matricula => matricula.TurmaId, (vinculo, matricula) => matricula)
            .Where(matricula => cadastroDoAluno.Contains(matricula.AlunoId))
            .Join(db.Set<Turma>(), matricula => matricula.TurmaId, turma => turma.Id, (_, turma) => turma)
            .AnyAsync(turma => semestresVigentes.Contains(turma.SemestreLetivoId), cancellationToken);
    }
}
