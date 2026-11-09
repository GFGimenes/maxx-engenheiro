using Maxx.Application.Ranking;
using Maxx.Domain.Academico;
using Maxx.Domain.Identidade;
using Maxx.Domain.Ranking;
using Maxx.Domain.Social;
using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.Ranking;

// A leitura nunca calcula nada (ADR 0005): a página é um SELECT pelo índice da visão e da
// posição, ix_posicao_no_ranking_visao_posicao, e a linha do aluno, pelo índice do aluno.
// Nome, arroba, curso e foto vêm pela chave primária de cada linha da página.
internal sealed class ConsultaDoRanking(MaxxDbContext db) : IConsultaDoRanking
{
    public async Task<LeitorDoRanking?> LeitorAsync(Guid leitorId, CancellationToken cancellationToken)
    {
        var linha = await (
                from usuario in db.Set<Usuario>()
                join cadastro in db.Set<CadastroElegivel>() on usuario.CadastroElegivelId equals cadastro.Id
                join curso in db.Set<Curso>() on cadastro.CursoId equals curso.Id into cursos
                from curso in cursos.DefaultIfEmpty()
                where usuario.Id == leitorId
                select new
                {
                    usuario.Id,
                    usuario.Papeis,
                    CursoId = curso == null ? (Guid?)null : curso.Id,
                    Curso = curso == null ? null : curso.Sigla,
                    NomeDoCurso = curso == null ? null : curso.Nome,
                    usuario.ExAlunoDesde,
                })
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        return linha is null
            ? null
            : new LeitorDoRanking(linha.Id, linha.Papeis, linha.CursoId, linha.Curso, linha.NomeDoCurso, linha.ExAlunoDesde is not null);
    }

    public async Task<SemestreDoRanking?> SemestreAtualAsync(DateOnly hoje, CancellationToken cancellationToken)
    {
        var calendario = new Calendario(await db.Set<AnoLetivo>().AsNoTracking()
            .Include(ano => ano.Semestres)
            .ThenInclude(semestre => semestre.Bimestres)
            .ToListAsync(cancellationToken));
        return SemestresDoRanking.Atual(calendario, hoje) is { } semestre ? new SemestreDoRanking(semestre.Id, semestre.Codigo) : null;
    }

    public async Task<IReadOnlyList<LinhaLida>> PaginaAsync(
        VisaoDoRanking visao, Guid leitorId, bool soSeguidos, int depoisDaPosicao, int limite, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(visao);

        var linhas = DaVisao(visao).Where(linha => linha.Posicao > depoisDaPosicao);
        if (soSeguidos)
        {
            linhas = linhas.Where(linha => linha.AlunoId == leitorId
                || db.Set<Seguimento>().Any(seguimento => seguimento.SeguidorId == leitorId && seguimento.SeguidoId == linha.AlunoId));
        }

        return await LerAsync(linhas.OrderBy(linha => linha.Posicao).Take(limite), cancellationToken);
    }

    public async Task<LinhaLida?> LinhaDoAlunoAsync(VisaoDoRanking visao, Guid alunoId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(visao);

        return (await LerAsync(DaVisao(visao).Where(linha => linha.AlunoId == alunoId), cancellationToken)).SingleOrDefault();
    }

    // O nulo vira IS NULL literal, e o valor, igualdade simples: a comparação com parâmetro
    // nulável viraria um OR que o índice da visão não atende.
    private IQueryable<PosicaoNoRankingRow> DaVisao(VisaoDoRanking visao)
    {
        var linhas = db.Set<PosicaoNoRankingRow>().Where(linha => linha.Escopo == visao.Escopo && linha.Periodo == visao.Periodo);
        linhas = visao.SemestreLetivoId is { } semestre
            ? linhas.Where(linha => linha.SemestreLetivoId == semestre)
            : linhas.Where(linha => linha.SemestreLetivoId == null);
        return visao.CursoId is { } curso
            ? linhas.Where(linha => linha.CursoId == curso)
            : linhas.Where(linha => linha.CursoId == null);
    }

    private async Task<List<LinhaLida>> LerAsync(IQueryable<PosicaoNoRankingRow> linhas, CancellationToken cancellationToken)
    {
        var lidas = await (
                from linha in linhas
                join usuario in db.Set<Usuario>() on linha.AlunoId equals usuario.Id
                join cadastro in db.Set<CadastroElegivel>() on usuario.CadastroElegivelId equals cadastro.Id
                join curso in db.Set<Curso>() on cadastro.CursoId equals curso.Id into cursos
                from curso in cursos.DefaultIfEmpty()
                orderby linha.Posicao
                select new
                {
                    linha.AlunoId,
                    linha.Posicao,
                    linha.Pontos,
                    usuario.NomeDeExibicao,
                    usuario.Prefixo,
                    usuario.Sufixo,
                    Curso = curso == null ? null : curso.Sigla,
                    NomeDoCurso = curso == null ? null : curso.Nome,
                    cadastro.Semestre,
                    usuario.ExAlunoDesde,
                    usuario.ChaveDaFotoDaGrade,
                })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return
        [
            .. lidas.Select(linha => new LinhaLida(
                linha.AlunoId,
                linha.Posicao,
                linha.Pontos,
                linha.NomeDeExibicao,
                new Arroba(linha.Prefixo, linha.Sufixo).ToString(),
                linha.Curso,
                linha.NomeDoCurso,
                linha.Semestre,
                linha.ExAlunoDesde is not null,
                linha.ChaveDaFotoDaGrade)),
        ];
    }
}
