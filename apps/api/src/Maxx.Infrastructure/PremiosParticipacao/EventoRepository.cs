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

    public Task<Guid?> CursoDoAlunoAsync(Guid alunoId, CancellationToken cancellationToken) =>
        (from usuario in db.Set<Usuario>()
         join cadastro in db.Set<CadastroElegivel>() on usuario.CadastroElegivelId equals cadastro.Id
         where usuario.Id == alunoId
         select cadastro.CursoId)
        .SingleOrDefaultAsync(cancellationToken);
}
