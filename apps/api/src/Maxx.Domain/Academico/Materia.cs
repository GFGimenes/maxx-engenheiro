namespace Maxx.Domain.Academico;

public sealed class Materia
{
    public Materia(Curso curso, string nome)
    {
        ArgumentNullException.ThrowIfNull(curso);

        Id = Guid.CreateVersion7();
        CursoId = curso.Id;
        Nome = Texto.Obrigatorio(nome, "O nome da matéria");
    }

    public Guid Id { get; private set; }

    public Guid CursoId { get; private set; }

    public string Nome { get; private set; }
}
