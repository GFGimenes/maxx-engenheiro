using Maxx.Domain.Academico;

namespace Maxx.Domain.Identidade;

// A parte do arroba depois do hífen. Nunca escolhida pelo usuário: segue o papel e,
// para o aluno, o curso atual ou a marcação de ex-aluno (produto.md 2.3).
public static class Sufixo
{
    public const string ExAluno = "EX";

    public const string Professor = "PROF";

    public const string Instituicao = "UNIMAX";

    public static string Para(Papeis papeis, Curso? curso, bool exAluno)
    {
        if (papeis == Papeis.Aluno)
        {
            if (exAluno)
            {
                return ExAluno;
            }

            return curso?.Sigla ?? throw new DomainException("O sufixo do aluno exige o curso dele.");
        }

        // Professor e instituição na mesma conta usam o do professor.
        if (papeis.HasFlag(Papeis.Professor))
        {
            return Professor;
        }

        return papeis == Papeis.Instituicao
            ? Instituicao
            : throw new DomainException($"Sem sufixo de arroba para o papel {papeis}.");
    }
}

public sealed record Arroba(string Prefixo, string Sufixo)
{
    public override string ToString() => $"@{Prefixo}-{Sufixo}";
}
