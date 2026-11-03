namespace Maxx.Domain.Academico;

// Vínculo aluno e turma. Única fonte de "quantas matérias o aluno tem no período".
public sealed class Matricula
{
    // Para o EF Core materializar do banco.
    private Matricula()
    {
    }

    public Matricula(CadastroElegivel aluno, Turma turma)
    {
        ArgumentNullException.ThrowIfNull(aluno);
        ArgumentNullException.ThrowIfNull(turma);

        if (!aluno.EhAluno)
        {
            throw new DomainException($"Só aluno se matricula em turma; {aluno.Email} é {aluno.Papeis}.");
        }

        if (turma.Materia.CursoId != aluno.CursoId)
        {
            throw new DomainException(
                $"{aluno.Email} não pode se matricular em {turma.Materia.Nome}: a matéria é de outro curso.");
        }

        Id = Guid.CreateVersion7();
        AlunoId = aluno.Id;
        TurmaId = turma.Id;
    }

    public Guid Id { get; private set; }

    // Aponta para o CadastroElegivel: a faculdade matricula antes de o aluno ter conta.
    public Guid AlunoId { get; private set; }

    public Guid TurmaId { get; private set; }
}
