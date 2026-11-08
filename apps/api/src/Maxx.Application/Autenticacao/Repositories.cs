using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;

namespace Maxx.Application.Autenticacao;

public interface ICadastroElegivelRepository
{
    // A linha de aluno em que RA e e-mail estão juntos, com o curso dela. E-mail sem
    // diferenciar maiúsculas.
    Task<(CadastroElegivel Cadastro, Curso Curso)?> BuscarAlunoAsync(string ra, string email, CancellationToken cancellationToken);

    // O e-mail como a faculdade o gravou, para onde vai a recuperação de senha.
    Task<string> EmailAsync(Guid cadastroElegivelId, CancellationToken cancellationToken);
}

public interface IUsuarioRepository
{
    Task<bool> ExisteParaCadastroAsync(Guid cadastroElegivelId, CancellationToken cancellationToken);

    // Dos candidatos, os que já são de alguém, comparados sem diferenciar maiúsculas.
    Task<IReadOnlySet<string>> PrefixosOcupadosAsync(IReadOnlyCollection<string> candidatos, CancellationToken cancellationToken);

    void Adicionar(Usuario usuario);

    Task<Usuario?> BuscarAsync(Guid id, CancellationToken cancellationToken);

    // Com todos os prefixos que ele já teve: é o que deixa editar o prefixo e voltar a um
    // antigo (reserva permanente, produto.md 2.3.1).
    Task<Usuario?> BuscarComPrefixosAsync(Guid id, CancellationToken cancellationToken);

    // Login por e-mail, de qualquer papel. E-mail sem diferenciar maiúsculas.
    Task<Usuario?> BuscarPorEmailAsync(string email, CancellationToken cancellationToken);

    // Login por RA, só de aluno (ex-aluno inclusive): professor e instituição entram só
    // por e-mail, mesmo quando a linha deles tem RA (produto.md 2.5).
    Task<Usuario?> BuscarAlunoPorRaAsync(string ra, CancellationToken cancellationToken);
}

public interface ITermoDeUsoRepository
{
    // A versão de número mais alto. Nula se nenhuma foi publicada.
    Task<TermoDeUso?> VigenteAsync(CancellationToken cancellationToken);
}

public interface IFamiliaDeSessaoRepository
{
    void Adicionar(FamiliaDeSessao familia);

    // A família dona do refresh com este hash, carregada só com esse token: é o que a
    // rotação e o logout precisam, e a família ganha um token a cada renovação.
    Task<FamiliaDeSessao?> BuscarPorRefreshAsync(string hashDoRefresh, CancellationToken cancellationToken);

    // As famílias ainda não revogadas do usuário, sem os tokens: a senha redefinida revoga
    // todas.
    Task<IReadOnlyList<FamiliaDeSessao>> AtivasDoUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken);
}

public interface IRecuperacaoDeSenhaRepository
{
    void Adicionar(RecuperacaoDeSenha recuperacao);

    Task<RecuperacaoDeSenha?> BuscarAsync(Guid id, CancellationToken cancellationToken);

    Task<RecuperacaoDeSenha?> BuscarPorHashAsync(string hash, CancellationToken cancellationToken);

    // Os pedidos do usuário ainda não usados nem invalidados, que a conclusão invalida.
    Task<IReadOnlyList<RecuperacaoDeSenha>> PendentesDoUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken);
}
