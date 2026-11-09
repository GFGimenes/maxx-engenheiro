using Maxx.Application.Instituicao;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.Instituicao;

internal sealed class AlunosDaInstituicaoRepository(MaxxDbContext db) : IAlunosDaInstituicaoRepository
{
    public Task<CadastroElegivel?> CadastroDeAlunoAsync(Guid cadastroId, CancellationToken cancellationToken) =>
        db.Set<CadastroElegivel>().SingleOrDefaultAsync(
            cadastro => cadastro.Id == cadastroId && cadastro.Papeis == Papeis.Aluno, cancellationToken);

    public Task<Usuario?> ContaAsync(Guid cadastroId, CancellationToken cancellationToken) =>
        db.Set<Usuario>().SingleOrDefaultAsync(usuario => usuario.CadastroElegivelId == cadastroId, cancellationToken);

    public Task<Curso?> CursoAsync(Guid cursoId, CancellationToken cancellationToken) =>
        db.Set<Curso>().SingleOrDefaultAsync(curso => curso.Id == cursoId, cancellationToken);

    // e-mail em citext: a comparação no banco não diferencia maiúsculas.
    public Task<bool> EmailDeOutraLinhaAsync(string email, Guid cadastroId, CancellationToken cancellationToken) =>
        db.Set<CadastroElegivel>().AnyAsync(cadastro => cadastro.Email == email && cadastro.Id != cadastroId, cancellationToken);

    public void Adicionar(CorrecaoDeEmail correcao) => db.Add(correcao);

    public void Adicionar(TrocaDeCurso troca) => db.Add(troca);
}

internal sealed class ConsultaDaInstituicao(MaxxDbContext db) : IConsultaDaInstituicao
{
    public async Task<IReadOnlyList<AlunoDaBase>> BuscarAlunosAsync(string termo, int limite, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(termo);

        // O termo entra num LIKE: % e _ dele são texto, não curinga. Sem índice próprio: a
        // busca é só da instituição, sobre a base de alunos.
        var trecho = termo.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
        var aluno = (int)Papeis.Aluno;
        var ids = await db.Database.SqlQuery<Guid>($"""
                SELECT c.id AS "Value"
                FROM cadastro_elegivel c
                LEFT JOIN usuario u ON u.cadastro_elegivel_id = c.id
                WHERE c.papeis = {aluno}
                  AND (maxx_sem_acento(lower(c.nome_completo)) LIKE '%' || maxx_sem_acento(lower({trecho})) || '%'
                       OR maxx_sem_acento(lower(u.nome_de_exibicao)) LIKE '%' || maxx_sem_acento(lower({trecho})) || '%'
                       OR lower(u.prefixo::text) LIKE '%' || lower(ltrim({trecho}, '@')) || '%'
                       OR lower(c.email::text) LIKE '%' || lower({trecho}) || '%'
                       OR c.ra = {termo})
                ORDER BY maxx_sem_acento(lower(c.nome_completo)) COLLATE "C", c.id
                LIMIT {limite}
                """)
            .ToListAsync(cancellationToken);

        var achados = (await AlunosAsync(ids, cancellationToken)).ToDictionary(achado => achado.Id);
        return [.. ids.Select(id => achados[id])];
    }

    public async Task<FichaDoAluno?> FichaAsync(Guid cadastroId, CancellationToken cancellationToken)
    {
        if ((await AlunosAsync([cadastroId], cancellationToken)).SingleOrDefault() is not { } aluno)
        {
            return null;
        }

        var medalhas = aluno.Conta is not { } conta
            ? []
            : await (
                    from medalha in db.Set<Medalha>()
                    join item in db.Set<ItemCatalogoRow>() on medalha.ItemCodigo equals item.Codigo
                    join materia in db.Set<Materia>() on medalha.MateriaId equals materia.Id
                    join bimestre in db.Set<Bimestre>() on medalha.BimestreId equals bimestre.Id
                    join semestre in db.Set<SemestreLetivo>() on medalha.SemestreLetivoId equals semestre.Id
                    join professor in db.Set<Usuario>() on medalha.ConcedidaPorId equals professor.Id
                    where medalha.AlunoId == conta.Id && medalha.AnuladaEm == null
                    orderby medalha.ConcedidaEm descending, medalha.Id descending
                    select new MedalhaDaFicha(
                        medalha.Id,
                        medalha.ItemCodigo,
                        item.Nome,
                        medalha.Tier,
                        medalha.Pontos,
                        materia.Nome,
                        semestre.Ano,
                        bimestre.Numero,
                        professor.NomeDeExibicao,
                        medalha.ConcedidaEm))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

        return new FichaDoAluno(aluno, medalhas);
    }

    private async Task<List<AlunoDaBase>> AlunosAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var linhas = await (
                from cadastro in db.Set<CadastroElegivel>()
                join curso in db.Set<Curso>() on cadastro.CursoId equals curso.Id
                join u in db.Set<Usuario>() on cadastro.Id equals u.CadastroElegivelId into contas
                from usuario in contas.DefaultIfEmpty()
                where ids.Contains(cadastro.Id) && cadastro.Papeis == Papeis.Aluno
                select new
                {
                    cadastro.Id,
                    cadastro.NomeCompleto,
                    cadastro.Ra,
                    cadastro.Email,
                    CursoId = curso.Id,
                    curso.Sigla,
                    cadastro.Semestre,
                    cadastro.Demonstracao,
                    UsuarioId = usuario == null ? (Guid?)null : usuario.Id,
                    NomeDeExibicao = usuario == null ? null : usuario.NomeDeExibicao,
                    Prefixo = usuario == null ? null : usuario.Prefixo,
                    Sufixo = usuario == null ? null : usuario.Sufixo,
                    ExAlunoDesde = usuario == null ? null : usuario.ExAlunoDesde,
                    ChaveDaFoto = usuario == null ? null : usuario.ChaveDaFotoDaGrade,
                })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return [.. linhas.Select(linha => new AlunoDaBase(
            linha.Id,
            linha.NomeCompleto,
            linha.Ra!,
            linha.Email,
            linha.CursoId,
            linha.Sigla,
            linha.Semestre!.Value,
            linha.Demonstracao,
            linha.UsuarioId is { } usuarioId
                ? new ContaDoAluno(
                    usuarioId,
                    linha.NomeDeExibicao!,
                    new Arroba(linha.Prefixo!, linha.Sufixo!).ToString(),
                    linha.ExAlunoDesde,
                    linha.ChaveDaFoto)
                : null))];
    }
}
