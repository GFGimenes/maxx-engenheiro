using Microsoft.EntityFrameworkCore;

namespace Maxx.Infrastructure.Persistence;

// O esquema cresce por passo: cada passo traz a sua migration. Sem DbSet: as entidades
// entram pelas configurações, cada uma com nome de tabela explícito, e o acesso é por
// Set<T>(). Um DbSet no plural vazaria para o nome das chaves estrangeiras.
public sealed class MaxxDbContext(DbContextOptions<MaxxDbContext> options) : DbContext(options)
{
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    public static void Configure(DbContextOptionsBuilder options, Action<DbContextOptionsBuilder> useNpgsql)
    {
        useNpgsql(options);
        options.UseSnakeCaseNamingConvention();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MaxxDbContext).Assembly);
    }
}
