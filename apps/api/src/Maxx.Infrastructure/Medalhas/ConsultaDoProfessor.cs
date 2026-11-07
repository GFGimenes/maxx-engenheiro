using Maxx.Application.Medalhas;
using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.Medalhas;

internal sealed class ConsultaDoProfessor(MaxxDbContext db, TimeProvider relogio) : IConsultaDoProfessor
{
    private static readonly string _tipoMedalha = NomeJson.De(TipoDeItem.Medalha);

    public async Task<IReadOnlyList<TurmaDoProfessor>> TurmasVigentesAsync(Guid professorId, CancellationToken cancellationToken)
    {
        var vigentes = SemestreVigente.Ids(db, FusoHorario.DataLocal(relogio.GetUtcNow()));
        var turmas = await (
                from vinculo in db.Set<VinculoProfessor>()
                join professor in db.Set<Usuario>() on vinculo.ProfessorId equals professor.CadastroElegivelId
                join turma in db.Set<Turma>() on vinculo.TurmaId equals turma.Id
                join curso in db.Set<Curso>() on turma.Materia.CursoId equals curso.Id
                join semestre in db.Set<SemestreLetivo>() on turma.SemestreLetivoId equals semestre.Id
                where professor.Id == professorId && vigentes.Contains(turma.SemestreLetivoId)
                select new
                {
                    turma.Id,
                    Materia = turma.Materia.Nome,
                    Curso = curso.Sigla,
                    semestre.Ano,
                    semestre.Numero,
                    Alunos = db.Set<Matricula>().Count(matricula => matricula.TurmaId == turma.Id
                        && db.Set<Usuario>().Any(usuario => usuario.CadastroElegivelId == matricula.AlunoId
                            && usuario.Papeis == Papeis.Aluno && usuario.ExAlunoDesde == null)),
                })
            .ToListAsync(cancellationToken);

        return [.. turmas
            .OrderBy(turma => turma.Materia, StringComparer.CurrentCulture)
            .Select(turma => new TurmaDoProfessor(turma.Id, turma.Materia, turma.Curso, $"{turma.Ano}/{turma.Numero}", turma.Alunos))];
    }

    public async Task<IReadOnlyList<AlunoDaTurma>> AlunosAsync(Guid turmaId, CancellationToken cancellationToken)
    {
        var alunos = await AlunosComConta(turmaId).ToListAsync(cancellationToken);
        var ids = alunos.Select(aluno => aluno.Id).ToList();
        var medalhas = await (
                from medalha in db.Set<Medalha>()
                join bimestre in db.Set<Bimestre>() on medalha.BimestreId equals bimestre.Id
                where medalha.TurmaId == turmaId && medalha.AnuladaEm == null && ids.Contains(medalha.AlunoId)
                orderby medalha.ConcedidaEm
                select new { medalha.AlunoId, medalha.Id, medalha.ItemCodigo, bimestre.Numero, medalha.ConcedidaEm })
            .ToListAsync(cancellationToken);
        var porAluno = medalhas.ToLookup(medalha => medalha.AlunoId);

        return [.. alunos
            .OrderBy(aluno => aluno.NomeDeExibicao, StringComparer.CurrentCulture)
            .Select(aluno => new AlunoDaTurma(
                aluno.Id,
                aluno.NomeDeExibicao,
                aluno.Arroba.ToString(),
                [.. porAluno[aluno.Id].Select(medalha => new MedalhaNaTurma(medalha.Id, medalha.ItemCodigo, medalha.Numero, medalha.ConcedidaEm))]))];
    }

    public async Task<IReadOnlyList<ItemDeMedalha>> MedalhasDoCatalogoAsync(CancellationToken cancellationToken)
    {
        var hoje = FusoHorario.DataLocal(relogio.GetUtcNow());
        var rows = await db.Set<ItemCatalogoRow>().AsNoTracking()
            .Where(item => item.Tipo == _tipoMedalha)
            .Where(item => (item.VigenciaInicio == null || item.VigenciaInicio <= hoje) && (item.VigenciaFim == null || item.VigenciaFim >= hoje))
            .OrderBy(item => item.Codigo)
            .ToListAsync(cancellationToken);
        return [.. rows.Select(row => new ItemDeMedalha(CatalogoRowMapper.ParaMedalha(row), row.Versao))];
    }

    // Os alunos da turma que podem receber medalha: matriculados, com conta e não ex-alunos.
    // Aluno sem conta não aparece (produto.md 4.8).
    private IQueryable<Usuario> AlunosComConta(Guid turmaId) =>
        from matricula in db.Set<Matricula>()
        join usuario in db.Set<Usuario>() on matricula.AlunoId equals usuario.CadastroElegivelId
        where matricula.TurmaId == turmaId && usuario.Papeis == Papeis.Aluno && usuario.ExAlunoDesde == null
        select usuario;
}
