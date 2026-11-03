namespace Maxx.Domain.Academico;

public sealed class Curso
{
    public Curso(string nome, string sigla, int duracaoEmSemestres)
    {
        sigla = Texto.Obrigatorio(sigla, "A sigla do curso");

        // A sigla vira o sufixo do arroba (@guilhermeGimenes-CC).
        if (!sigla.All(char.IsAsciiLetterUpper))
        {
            throw new DomainException($"A sigla do curso ({sigla}) só pode ter letras maiúsculas sem acento.");
        }

        if (duracaoEmSemestres < 1)
        {
            throw new DomainException($"O curso {sigla} precisa durar ao menos um semestre.");
        }

        Id = Guid.CreateVersion7();
        Nome = Texto.Obrigatorio(nome, "O nome do curso");
        Sigla = sigla;
        DuracaoEmSemestres = duracaoEmSemestres;
    }

    public Guid Id { get; private set; }

    public string Nome { get; private set; }

    public string Sigla { get; private set; }

    public int DuracaoEmSemestres { get; private set; }
}
