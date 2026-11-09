using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Identidade;

namespace Maxx.Application.Instituicao;

// A escrita dos atos da instituição sobre o aluno (passo 12).
public interface IAlunosDaInstituicaoRepository
{
    // Só a linha de aluno. Nula para outro papel e para o que não existe.
    Task<CadastroElegivel?> CadastroDeAlunoAsync(Guid cadastroId, CancellationToken cancellationToken);

    Task<Usuario?> ContaAsync(Guid cadastroId, CancellationToken cancellationToken);

    Task<Curso?> CursoAsync(Guid cursoId, CancellationToken cancellationToken);

    // Se outra linha da base já tem o e-mail, sem diferenciar maiúsculas.
    Task<bool> EmailDeOutraLinhaAsync(string email, Guid cadastroId, CancellationToken cancellationToken);

    void Adicionar(CorrecaoDeEmail correcao);

    void Adicionar(TrocaDeCurso troca);
}

// A leitura da área da instituição. Só consulta, sem regra.
public interface IConsultaDaInstituicao
{
    // Linhas de aluno da base, com conta ou sem, por trecho do nome completo ou do nome de
    // exibição (sem acento nem maiúsculas), do prefixo do arroba ou do e-mail, ou pelo RA
    // inteiro. Por nome completo, A a Z.
    Task<IReadOnlyList<AlunoDaBase>> BuscarAlunosAsync(string termo, int limite, CancellationToken cancellationToken);

    Task<FichaDoAluno?> FichaAsync(Guid cadastroId, CancellationToken cancellationToken);
}

// Conta nula: está na base e não se cadastrou.
public sealed record AlunoDaBase(
    Guid Id,
    string NomeCompleto,
    string Ra,
    string Email,
    Guid CursoId,
    string Curso,
    int Semestre,
    bool Demonstracao,
    ContaDoAluno? Conta);

public sealed record ContaDoAluno(Guid Id, string NomeDeExibicao, string Arroba, DateTimeOffset? ExAlunoDesde, string? ChaveDaFoto);

public sealed record FichaDoAluno(AlunoDaBase Aluno, IReadOnlyList<MedalhaDaFicha> Medalhas);

// As medalhas ativas, da mais nova para a mais antiga: o que a anulação alcança.
public sealed record MedalhaDaFicha(
    Guid Id,
    string Item,
    string NomeDoItem,
    Tier Tier,
    int Pontos,
    string Materia,
    int Ano,
    int Bimestre,
    string ConcedidaPor,
    DateTimeOffset ConcedidaEm);
