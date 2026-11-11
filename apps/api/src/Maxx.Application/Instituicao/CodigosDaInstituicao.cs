namespace Maxx.Application.Instituicao;

// Os códigos de recusa da área da instituição (passo 12), como o cliente os recebe em
// ProblemDetails. Ex-aluno, curso inexistente e busca sem critério usam os que já existem.
public static class CodigosDaInstituicao
{
    // O id não é de uma linha de aluno da base.
    public const string AlunoNaoEncontrado = "aluno-nao-encontrado";

    // Marcar ex-aluno pede conta: quem só está na base não tem o que marcar.
    public const string AlunoSemConta = "aluno-sem-conta";

    public const string EmailInvalido = "email-invalido";

    // O e-mail novo não é de um domínio aceito (produto.md 2.4).
    public const string DominioNaoAceito = "dominio-nao-aceito";

    // A coordenação de demonstração corrige só para domínio reservado .invalid (passo 14, P5).
    public const string DominioNaoReservado = "dominio-nao-reservado";

    // Outra linha da base, de qualquer papel, já tem esse e-mail.
    public const string EmailEmUso = "email-em-uso";

    public const string MesmoCurso = "mesmo-curso";

    // O semestre do aluno não existe no curso novo.
    public const string SemestreInvalido = "semestre-invalido";

    // A purga pede o nome do lote digitado de novo, igual (P6).
    public const string ConfirmacaoDaPurgaInvalida = "confirmacao-invalida";

    // Nenhum aluno de demonstração com esse lote.
    public const string LoteNaoEncontrado = "lote-nao-encontrado";
}
