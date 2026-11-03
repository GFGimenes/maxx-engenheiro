namespace Maxx.Domain.Academico;

// Matéria mais semestre letivo. Professores vinculados e alunos matriculados por turma.
public sealed class Turma
{
    public Turma(Materia materia, SemestreLetivo semestre)
    {
        ArgumentNullException.ThrowIfNull(materia);
        ArgumentNullException.ThrowIfNull(semestre);

        Id = Guid.CreateVersion7();
        Materia = materia;
        MateriaId = materia.Id;
        SemestreLetivoId = semestre.Id;
    }

    public Guid Id { get; private set; }

    public Guid MateriaId { get; private set; }

    public Materia Materia { get; private set; }

    public Guid SemestreLetivoId { get; private set; }
}
