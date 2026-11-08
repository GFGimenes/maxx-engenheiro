namespace Maxx.Application.Autorizacao;

// O escopo de turma do professor, perguntado ao banco a cada mutação e nunca lido do
// token (CLAUDE.md, Segurança; decisoes/README.md, Escopo de turma). Vínculo removido vale
// no pedido seguinte, mesmo com o access token antigo.
public interface IEscopoDeTurma
{
    // Verdadeiro só se o usuário está vinculado à turma e a turma é do semestre letivo
    // vigente hoje em America/Sao_Paulo. Fora de semestre vigente (recesso, férias), falso.
    Task<bool> ProfessorLecionaAsync(Guid usuarioId, Guid turmaId, CancellationToken cancellationToken);

    // Verdadeiro só se o aluno (pela conta) está matriculado numa turma do semestre vigente
    // em que o professor (pela conta) leciona. É o que deixa o professor seguir o aluno
    // (produto.md 6.3).
    Task<bool> ProfessorEnsinaAlunoAsync(Guid professorId, Guid alunoId, CancellationToken cancellationToken);
}
