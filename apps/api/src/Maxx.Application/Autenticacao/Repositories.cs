using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;

namespace Maxx.Application.Autenticacao;

public interface ICadastroElegivelRepository
{
    // A linha de aluno em que RA e e-mail estão juntos, com o curso dela. E-mail sem
    // diferenciar maiúsculas.
    Task<(CadastroElegivel Cadastro, Curso Curso)?> BuscarAlunoAsync(string ra, string email, CancellationToken cancellationToken);
}

public interface IUsuarioRepository
{
    Task<bool> ExisteParaCadastroAsync(Guid cadastroElegivelId, CancellationToken cancellationToken);

    // Dos candidatos, os que já são de alguém, comparados sem diferenciar maiúsculas.
    Task<IReadOnlySet<string>> PrefixosOcupadosAsync(IReadOnlyCollection<string> candidatos, CancellationToken cancellationToken);

    void Adicionar(Usuario usuario);

    Task<Usuario?> BuscarAsync(Guid id, CancellationToken cancellationToken);

    // Login por e-mail, de qualquer papel. E-mail sem diferenciar maiúsculas.
    Task<Usuario?> BuscarPorEmailAsync(string email, CancellationToken cancellationToken);

    // Login por RA, só de aluno (ex-aluno inclusive): professor e instituição entram só
    // por e-mail, mesmo quando a linha deles tem RA (produto.md 2.5).
    Task<Usuario?> BuscarAlunoPorRaAsync(string ra, CancellationToken cancellationToken);
}

public interface IFamiliaDeSessaoRepository
{
    void Adicionar(FamiliaDeSessao familia);

    // A família dona do refresh com este hash, carregada só com esse token: é o que a
    // rotação e o logout precisam, e a família ganha um token a cada renovação.
    Task<FamiliaDeSessao?> BuscarPorRefreshAsync(string hashDoRefresh, CancellationToken cancellationToken);
}
