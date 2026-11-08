namespace Maxx.Application.PremiosParticipacao;

// Os códigos de recusa do prêmio de participação e do evento, como o cliente os recebe em
// ProblemDetails. Os que repetem o texto da medalha (item, ex-aluno, vigência, curso) têm a
// mesma mensagem no app.
public static class CodigosDoPremioParticipacao
{
    // O código não é de um prêmio de participação do catálogo.
    public const string ItemNaoEncontrado = "item-nao-encontrado";

    public const string ExAluno = "ex-aluno";

    public const string ItemForaDeVigencia = "item-fora-de-vigencia";

    public const string ItemDeOutroCurso = "item-de-outro-curso";

    // Nem o id de um evento semeado nem o evento digitado.
    public const string EventoObrigatorio = "evento-obrigatorio";

    // Os dois ao mesmo tempo.
    public const string EventoAmbiguo = "evento-ambiguo";

    // O evento digitado tem nome fora do tamanho ou termina antes de começar.
    public const string EventoInvalido = "evento-invalido";

    // O id não é de um evento semeado, ou o evento é de outro curso.
    public const string EventoNaoEncontrado = "evento-nao-encontrado";

    public const string EventoNaoAceitaOItem = "evento-nao-aceita-o-item";

    public const string EventoNaoComecou = "evento-nao-comecou";

    // O evento começou até o fim de um semestre letivo que já terminou (produto.md 4.6).
    public const string EventoDeSemestreEncerrado = "evento-de-semestre-encerrado";

    // Apresentação em sala sem a matéria.
    public const string MateriaObrigatoria = "materia-obrigatoria";

    // Matéria informada para um item que não deduplica por ela.
    public const string MateriaNaoSeAplica = "materia-nao-se-aplica";

    // O aluno não estava matriculado numa turma da matéria no semestre da data do evento.
    public const string MateriaNaoEncontrada = "materia-nao-encontrada";

    // Um prêmio por evento: o aluno já tem uma reivindicação que não foi reprovada com a
    // mesma chave.
    public const string PremioJaReivindicado = "premio-ja-reivindicado";
}
