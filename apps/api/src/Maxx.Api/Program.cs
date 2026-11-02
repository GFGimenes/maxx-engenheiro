using Maxx.Api.Health;
using Maxx.Infrastructure;
using Maxx.Infrastructure.Persistence;

// `Maxx.Api migrate` roda as migrations e sai. É o comando do contêiner de migração.
var isMigrate = args is ["migrate", ..];

var builder = WebApplication.CreateBuilder(isMigrate ? args[1..] : args);

var connectionString = builder.Configuration.GetConnectionString("Maxx")
    ?? throw new InvalidOperationException("ConnectionStrings:Maxx não configurada.");

builder.Services.AddProblemDetails();
builder.Services.AddInfrastructure(connectionString);

var app = builder.Build();

if (isMigrate)
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(CancellationToken.None);
    return;
}

// Um formato só de erro: ProblemDetails (RFC 9457).
app.UseExceptionHandler();
app.UseStatusCodePages();

// Origem única (ADR 0001): toda rota da API vive sob /api, e o Caddy não corta o prefixo.
var api = app.MapGroup("/api");
api.MapHealthEndpoints();

await app.RunAsync();
