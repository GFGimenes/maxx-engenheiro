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
}

public interface IFamiliaDeSessaoRepository
{
    void Adicionar(FamiliaDeSessao familia);
}
