using Maxx.Application.PremiosParticipacao;
using Maxx.Domain.Academico;
using Maxx.Domain.Catalogo;
using Maxx.Domain.Identidade;
using Maxx.Domain.Premios;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.PremiosParticipacao;

internal sealed class EventoRepository(MaxxDbContext db) : IEventoRepository
{
    private static readonly string _tipoPremioParticipacao = NomeJson.De(TipoDeItem.PremioParticipacao);

    public async Task<IReadOnlyList<Evento>> SemeadosAsync(CancellationToken cancellationToken) =>
        await db.Set<Evento>().AsNoTracking()
            .Where(evento => evento.Origem == OrigemDoEvento.Semeado)
            .ToListAsync(cancellationToken);

    public async Task<ItemPremioParticipacao?> ItemAsync(string codigo, CancellationToken cancellationToken)
    {
        var row = await db.Set<ItemCatalogoRow>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Codigo == codigo && item.Tipo == _tipoPremioParticipacao, cancellationToken);
        return row is null ? null : CatalogoRowMapper.ParaPremioParticipacao(row);
    }

    // O fim do semestre é o do último bimestre, que não é coluna: a escolha é em memória,
    // sobre os poucos semestres do calendário.
    public async Task<SemestreLetivo?> UltimoSemestreEncerradoAsync(DateOnly data, CancellationToken cancellationToken) =>
        (await db.Set<SemestreLetivo>().AsNoTracking().Include(semestre => semestre.Bimestres).ToListAsync(cancellationToken))
            .Where(semestre => semestre.Fim < data)
            .MaxBy(semestre => semestre.Fim);

    public Task<Curso?> CursoDoAlunoAsync(Guid alunoId, CancellationToken cancellationToken) =>
        (from usuario in db.Set<Usuario>()
         join cadastro in db.Set<CadastroElegivel>() on usuario.CadastroElegivelId equals cadastro.Id
         join curso in db.Set<Curso>() on cadastro.CursoId equals curso.Id
         where usuario.Id == alunoId
         select curso)
        .AsNoTracking()
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Materia>> MateriasCursadasAsync(Guid alunoId, DateOnly data, CancellationToken cancellationToken)
    {
        var semestres = SemestreVigente.Ids(db, data);
        return await (
                from usuario in db.Set<Usuario>()
                join matricula in db.Set<Matricula>() on usuario.CadastroElegivelId equals matricula.AlunoId
                join turma in db.Set<Turma>() on matricula.TurmaId equals turma.Id
                join materia in db.Set<Materia>() on turma.MateriaId equals materia.Id
                where usuario.Id == alunoId && semestres.Contains(turma.SemestreLetivoId)
                select materia)
            .Distinct()
            .OrderBy(materia => materia.Nome)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<Evento?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<Evento>().SingleOrDefaultAsync(evento => evento.Id == id, cancellationToken);
}

internal sealed class PremioParticipacaoRepository(MaxxDbContext db) : IPremioParticipacaoRepository
{
    private static readonly string _tipoPremioParticipacao = NomeJson.De(TipoDeItem.PremioParticipacao);

    public void Adicionar(Evento evento) => db.Add(evento);

    public void Adicionar(PremioParticipacao premio) => db.Add(premio);

    public void Adicionar(Comprovacao comprovacao) => db.Add(comprovacao);

    public void Adicionar(Contestacao contestacao) => db.Add(contestacao);

    public Task<Contestacao?> ContestacaoAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<Contestacao>().SingleOrDefaultAsync(contestacao => contestacao.Id == id, cancellationToken);

    public Task<Contestacao?> ContestacaoDoPremioAsync(Guid premioId, CancellationToken cancellationToken) =>
        db.Set<Contestacao>().SingleOrDefaultAsync(contestacao => contestacao.PremioParticipacaoId == premioId, cancellationToken);

    public Task<Comprovacao?> ComprovacaoAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<Comprovacao>().SingleOrDefaultAsync(comprovacao => comprovacao.Id == id, cancellationToken);

    public Task<PremioParticipacao?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
        db.Set<PremioParticipacao>().SingleOrDefaultAsync(premio => premio.Id == id, cancellationToken);

    public async Task<ItemDePremioParticipacao?> ItemAsync(string codigo, CancellationToken cancellationToken)
    {
        var row = await db.Set<ItemCatalogoRow>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Codigo == codigo && item.Tipo == _tipoPremioParticipacao, cancellationToken);
        return row is null ? null : new ItemDePremioParticipacao(CatalogoRowMapper.ParaPremioParticipacao(row), row.Versao);
    }

    public Task<bool> ChaveOcupadaAsync(string chave, CancellationToken cancellationToken) =>
        db.Set<PremioParticipacao>().AnyAsync(
            premio => premio.ChaveDeDeduplicacao == chave && premio.Estado != EstadoDoPremioParticipacao.Reprovado, cancellationToken);

    public async Task<Materia?> MateriaCursadaAsync(Guid alunoId, Guid materiaId, DateOnly data, CancellationToken cancellationToken)
    {
        // O semestre que contém a data, pelos bimestres; e uma turma dele, da matéria, com
        // matrícula do cadastro do aluno.
        var semestres = SemestreVigente.Ids(db, data);
        return await (
                from usuario in db.Set<Usuario>()
                join matricula in db.Set<Matricula>() on usuario.CadastroElegivelId equals matricula.AlunoId
                join turma in db.Set<Turma>() on matricula.TurmaId equals turma.Id
                join materia in db.Set<Materia>() on turma.MateriaId equals materia.Id
                where usuario.Id == alunoId && materia.Id == materiaId && semestres.Contains(turma.SemestreLetivoId)
                select materia)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
    }
}
