namespace Maxx.Application.Medalhas;

// Os códigos de recusa da medalha, como o cliente os recebe em ProblemDetails.
public static class CodigosDaMedalha
{
    // A turma não é do professor, ou não é do semestre vigente.
    public const string ForaDoEscopo = "fora-do-escopo";

    // O aluno não tem conta, não está matriculado na turma ou não existe.
    public const string AlunoForaDaTurma = "aluno-fora-da-turma";

    public const string ItemNaoEncontrado = "item-nao-encontrado";

    public const string ExAluno = "ex-aluno";

    // Hoje está fora do semestre da turma ou entre dois bimestres: a medalha não teria
    // bimestre.
    public const string ForaDoPeriodo = "fora-do-periodo";

    public const string ItemForaDeVigencia = "item-fora-de-vigencia";

    public const string ItemDeOutroCurso = "item-de-outro-curso";

    // O aluno já tem, nessa matéria e nesse período, tantas medalhas desse item quanto o
    // limite do catálogo.
    public const string LimiteAtingido = "limite-atingido";

    // Anulação: o id não é de medalha nenhuma.
    public const string MedalhaNaoEncontrada = "medalha-nao-encontrada";

    // Anulação: o e-mail informado não é de uma conta com o papel de instituição.
    public const string SemContaDeInstituicao = "sem-conta-de-instituicao";
}
