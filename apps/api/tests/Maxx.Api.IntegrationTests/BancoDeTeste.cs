using Maxx.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Maxx.Api.IntegrationTests;

// O que os testes de persistência têm em comum: banco migrado, gravação e a falha que o
// Postgres devolve, conferida pelo nome da constraint.
internal static class BancoDeTeste
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<MaxxApiFactory> MigradoAsync(PostgresFixture postgres, string? diretorioDeDados = null)
    {
        var factory = new MaxxApiFactory(await postgres.CreateDatabaseAsync(), diretorioDeDados: diretorioDeDados);
        await factory.MigrateAsync(Ct);
        return factory;
    }

    public static async Task SalvarAsync(MaxxApiFactory factory, params object[] entidades)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        db.AddRange(entidades);
        await db.SaveChangesAsync(Ct);
    }

    // Grava só a entidade nova, num banco que já tem o que ela cita.
    public static async Task AdicionarAsync(MaxxApiFactory factory, object entidade)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        AnexarComoNova(db, entidade);
        await db.SaveChangesAsync(Ct);
    }

    public static async Task<PostgresException> FalhaAoSalvarAsync(MaxxApiFactory factory, object entidade)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MaxxDbContext>();
        AnexarComoNova(db, entidade);

        var falha = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct));
        return Assert.IsType<PostgresException>(falha.InnerException);
    }

    public static async Task<PostgresException> FalhaNoSqlAsync(MaxxApiFactory factory, string sql)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
        await using var command = dataSource.CreateCommand(sql);

        return await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(Ct));
    }

    public static void AssertUnicidade(PostgresException falha, string indice) =>
        AssertViolacao(falha, PostgresErrorCodes.UniqueViolation, indice);

    public static void AssertCheck(PostgresException falha, string constraint) =>
        AssertViolacao(falha, PostgresErrorCodes.CheckViolation, constraint);

    public static void AssertChaveEstrangeira(PostgresException falha, string constraint) =>
        AssertViolacao(falha, PostgresErrorCodes.ForeignKeyViolation, constraint);

    private static void AssertViolacao(PostgresException falha, string estado, string constraint)
    {
        Assert.Equal(estado, falha.SqlState);
        Assert.Equal(constraint, falha.ConstraintName);
    }

    // O que a entidade nova cita (a matéria da turma, por exemplo) já está no banco:
    // entra como inalterado, para a gravação, ou a falha, vir só da entidade nova.
    private static void AnexarComoNova(MaxxDbContext db, object entidade)
    {
        db.Attach(entidade);
        db.Entry(entidade).State = EntityState.Added;
    }
}

internal static class Repositorio
{
    public static string Raiz { get; } = Encontrar();

    public static string DiretorioDeDados => Path.Combine(Raiz, "data");

    public static string DiretorioDoCatalogo => Path.Combine(DiretorioDeDados, "catalogo");

    private static string Encontrar()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null && !File.Exists(Path.Combine(diretorio.FullName, "Maxx.sln")))
        {
            diretorio = diretorio.Parent;
        }

        return diretorio?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
