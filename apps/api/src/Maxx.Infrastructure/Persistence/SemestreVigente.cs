using Maxx.Domain.Academico;
using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.Persistence;

internal static class SemestreVigente
{
    // Os semestres cujos bimestres cobrem `hoje`: o semestre vai do início do primeiro
    // bimestre ao fim do último (SemestreLetivo). Consulta para compor, não para executar.
    public static IQueryable<Guid> Ids(MaxxDbContext db, DateOnly hoje) =>
        db.Set<Bimestre>()
            .GroupBy(bimestre => EF.Property<Guid>(bimestre, "SemestreLetivoId"))
            .Where(semestre => semestre.Min(bimestre => bimestre.Inicio) <= hoje && semestre.Max(bimestre => bimestre.Fim) >= hoje)
            .Select(semestre => semestre.Key);
}
