namespace Maxx.Domain.Academico;

// Professor cadastrado por turma, a cada semestre. É a fonte do escopo de turma checado
// no banco a cada mutação de prêmio.
public sealed class VinculoProfessor
{
    public VinculoProfessor(CadastroElegivel professor, Turma turma)
    {
        ArgumentNullException.ThrowIfNull(professor);
        ArgumentNullException.ThrowIfNull(turma);

        if (!professor.EhProfessor)
        {
            throw new DomainException($"Só professor se vincula a turma; {professor.Email} é {professor.Papeis}.");
        }

        Id = Guid.CreateVersion7();
        ProfessorId = professor.Id;
        TurmaId = turma.Id;
    }

    public Guid Id { get; private set; }

    public Guid ProfessorId { get; private set; }

    public Guid TurmaId { get; private set; }
}
