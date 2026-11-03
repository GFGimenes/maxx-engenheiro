namespace Maxx.Domain.Academico;

// Aluno nunca acumula papel. O único acúmulo possível é professor e instituição na
// mesma conta.
[Flags]
public enum Papeis
{
    Nenhum = 0,
    Aluno = 1,
    Professor = 2,
    Instituicao = 4,
}
