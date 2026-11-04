using System.Linq.Expressions;
using Maxx.Application.Autenticacao;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.Autenticacao;

internal sealed class CadastroElegivelRepository(MaxxDbContext db) : ICadastroElegivelRepository
{
    // e-mail em citext: a comparação no banco não diferencia maiúsculas.
    public async Task<(CadastroElegivel Cadastro, Curso Curso)?> BuscarAlunoAsync(
        string ra, string email, CancellationToken cancellationToken)
    {
        var encontrado = await db.Set<CadastroElegivel>()
            .Where(cadastro => cadastro.Papeis == Papeis.Aluno && cadastro.Ra == ra && cadastro.Email == email)
            .Join(db.Set<Curso>(), cadastro => cadastro.CursoId, curso => curso.Id, (cadastro, curso) => new { cadastro, curso })
            .SingleOrDefaultAsync(cancellationToken);

        return encontrado is null ? null : (encontrado.cadastro, encontrado.curso);
    }
}

internal sealed class UsuarioRepository(MaxxDbContext db) : IUsuarioRepository
{
    public Task<bool> ExisteParaCadastroAsync(Guid cadastroElegivelId, CancellationToken cancellationToken) =>
        db.Set<Usuario>().AnyAsync(usuario => usuario.CadastroElegivelId == cadastroElegivelId, cancellationToken);

    public async Task<IReadOnlySet<string>> PrefixosOcupadosAsync(
        IReadOnlyCollection<string> candidatos, CancellationToken cancellationToken)
    {
        // O banco compara em citext; a volta é conferida de novo sem diferenciar
        // maiúsculas, para o resultado responder pelo candidato como foi escrito.
        var noBanco = await db.Set<PrefixoReservado>()
            .Where(reservado => candidatos.Contains(reservado.Prefixo))
            .Select(reservado => reservado.Prefixo)
            .ToListAsync(cancellationToken);

        var ocupados = new HashSet<string>(noBanco, StringComparer.OrdinalIgnoreCase);
        return candidatos.Where(ocupados.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public void Adicionar(Usuario usuario) => db.Add(usuario);

    public Task<Usuario?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<Usuario>().SingleOrDefaultAsync(usuario => usuario.Id == id, cancellationToken);

    // e-mail em citext: a comparação no banco não diferencia maiúsculas.
    public Task<Usuario?> BuscarPorEmailAsync(string email, CancellationToken cancellationToken) =>
        PorCadastro(cadastro => cadastro.Email == email).SingleOrDefaultAsync(cancellationToken);

    public Task<Usuario?> BuscarAlunoPorRaAsync(string ra, CancellationToken cancellationToken) =>
        PorCadastro(cadastro => cadastro.Papeis == Papeis.Aluno && cadastro.Ra == ra).SingleOrDefaultAsync(cancellationToken);

    private IQueryable<Usuario> PorCadastro(Expression<Func<CadastroElegivel, bool>> filtro) =>
        db.Set<CadastroElegivel>()
            .Where(filtro)
            .Join(db.Set<Usuario>(), cadastro => cadastro.Id, usuario => usuario.CadastroElegivelId, (_, usuario) => usuario);
}

internal sealed class FamiliaDeSessaoRepository(MaxxDbContext db) : IFamiliaDeSessaoRepository
{
    public void Adicionar(FamiliaDeSessao familia) => db.Add(familia);

    public Task<FamiliaDeSessao?> BuscarPorRefreshAsync(string hashDoRefresh, CancellationToken cancellationToken) =>
        db.Set<FamiliaDeSessao>()
            .Include(familia => familia.Tokens.Where(token => token.Hash == hashDoRefresh))
            .Where(familia => familia.Tokens.Any(token => token.Hash == hashDoRefresh))
            .SingleOrDefaultAsync(cancellationToken);
}
