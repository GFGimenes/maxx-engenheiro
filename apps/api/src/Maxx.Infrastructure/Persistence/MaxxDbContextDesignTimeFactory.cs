using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Maxx.Infrastructure.Persistence;

// Usada só pelo `dotnet ef migrations add`, que não abre conexão.
internal sealed class MaxxDbContextDesignTimeFactory : IDesignTimeDbContextFactory<MaxxDbContext>
{
    public MaxxDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MaxxDbContext>();
        MaxxDbContext.Configure(options, builder => builder.UseNpgsql(
            "Host=design-time",
            npgsql => npgsql.MigrationsHistoryTable(MaxxDbContext.MigrationsHistoryTable)));
        return new MaxxDbContext(options.Options);
    }
}
