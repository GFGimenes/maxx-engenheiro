using Maxx.Application.Fila;
using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;

namespace Maxx.Application.Medalhas;

public interface IMedalhaRepository
{
    void Adicionar(Medalha medalha);

    Task<Medalha?> BuscarAsync(Guid id, CancellationToken cancellationToken);

    // Todas as medalhas do aluno nesse item, ativas ou anuladas: o domínio separa as que
    // ocupam vaga no período.
    Task<IReadOnlyList<Medalha>> DoAlunoNoItemAsync(Guid alunoId, string itemCodigo, CancellationToken cancellationToken);

    // O item de medalha do catálogo, com a versão gravada pelo seeder. Nulo se o código
    // não existe ou não é medalha.
    Task<ItemDeMedalha?> ItemAsync(string codigo, CancellationToken cancellationToken);

    Task<TurmaDaConcessao?> TurmaAsync(Guid turmaId, CancellationToken cancellationToken);

    // O usuário do aluno, se ele tem conta e está matriculado na turma, com a sigla do curso
    // atual dele. Aluno sem conta não existe para a medalha (produto.md 4.8).
    Task<AlunoDaConcessao?> AlunoMatriculadoAsync(Guid turmaId, Guid alunoId, CancellationToken cancellationToken);
}

public sealed record ItemDeMedalha(ItemMedalha Item, int Versao);

public sealed record TurmaDaConcessao(Turma Turma, SemestreLetivo Semestre);

public sealed record AlunoDaConcessao(Usuario Aluno, string SiglaDoCurso);

// O que a tela do professor lê: só consulta, sem regra.
public interface IConsultaDoProfessor
{
    // As turmas do semestre vigente hoje em America/Sao_Paulo a que o professor está
    // vinculado. Fora de semestre vigente, nenhuma.
    Task<IReadOnlyList<TurmaDoProfessor>> TurmasVigentesAsync(Guid professorId, CancellationToken cancellationToken);

    // Os alunos matriculados na turma que têm conta e não são ex-alunos, por nome, com as
    // medalhas ativas que já receberam nela.
    Task<IReadOnlyList<AlunoDaTurma>> AlunosAsync(Guid turmaId, CancellationToken cancellationToken);

    // As medalhas do catálogo vigentes hoje, na ordem do código.
    Task<IReadOnlyList<ItemDeMedalha>> MedalhasDoCatalogoAsync(CancellationToken cancellationToken);
}

public sealed record TurmaDoProfessor(Guid Id, string Materia, string Curso, string Semestre, int Alunos);

public sealed record AlunoDaTurma(Guid Id, string NomeDeExibicao, string Arroba, IReadOnlyList<MedalhaNaTurma> Medalhas);

public sealed record MedalhaNaTurma(Guid Id, string Item, int Bimestre, DateTimeOffset ConcedidaEm);

// Os trabalhos que a concessão e a anulação enfileiram, na mesma transação.
public sealed record NotificarMedalhaRecebida(Guid MedalhaId) : ICargaDeTrabalho
{
    public static string Tipo => "notificar-medalha-recebida";
}

public sealed record RecalcularRaridade : ICargaDeTrabalho
{
    public static string Tipo => "recalcular-raridade";
}
