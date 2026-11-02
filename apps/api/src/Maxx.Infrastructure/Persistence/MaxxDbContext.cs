using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.Persistence;

// O esquema cresce por passo: cada passo traz a sua migration.
public sealed class MaxxDbContext(DbContextOptions<MaxxDbContext> options) : DbContext(options)
{
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    public static void Configure(DbContextOptionsBuilder options, Action<DbContextOptionsBuilder> useNpgsql)
    {
        useNpgsql(options);
        options.UseSnakeCaseNamingConvention();
    }
}
