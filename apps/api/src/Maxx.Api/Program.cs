using System.Reflection;
using System.Text.Json.Serialization;
using Maxx.Api.Health;
using Maxx.Infrastructure;
using Maxx.Infrastructure.Persistence;
using Maxx.Infrastructure.Seed;

// `Maxx.Api migrate` roda as migrations e sai. É o comando do contêiner de migração.
var isMigrate = args is ["migrate", ..];

// `Maxx.Api seed <base|demo>` grava os dados de data/ e sai. Idempotente.
var isSeed = args is ["seed", ..];

// A geração do OpenAPI no build sobe a aplicação sem servidor e sem banco.
var isOpenApiGeneration = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

var builder = WebApplication.CreateBuilder(
    isMigrate ? args[1..] : isSeed ? args[Math.Min(2, args.Length)..] : args);

var connectionString = builder.Configuration.GetConnectionString("Maxx")
    ?? (isOpenApiGeneration ? "Host=openapi-generation" : null)
    ?? throw new InvalidOperationException("ConnectionStrings:Maxx não configurada.");

builder.Services.AddProblemDetails();

// Número só como número: o padrão web aceita "42" em texto, e o contrato viraria
// `number | string` em todo campo numérico do cliente TypeScript.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);

builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "Maxx API";
    document.Info.Version = "v1";
    return Task.CompletedTask;
}));
builder.Services.AddInfrastructure(connectionString);
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.Section));

var app = builder.Build();

if (isMigrate)
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(CancellationToken.None);
    return 0;
}

if (isSeed)
{
    if (args is not [_, var nomeDoPerfil, ..] || !Enum.TryParse<PerfilDeSeed>(nomeDoPerfil, ignoreCase: true, out var perfil)
        || !Enum.IsDefined(perfil))
    {
        await Console.Error.WriteLineAsync("Uso: Maxx.Api seed <base|demo>");
        return 2;
    }

    await using var scope = app.Services.CreateAsyncScope();
    try
    {
        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync(perfil, CancellationToken.None);
    }
    catch (SeedException ex)
    {
        // Dado recusado é resposta esperada, não falha do programa: sem pilha no log.
        await Console.Error.WriteLineAsync(ex.Message);
        return 1;
    }

    return 0;
}

// Um formato só de erro: ProblemDetails (RFC 9457).
app.UseExceptionHandler();
app.UseStatusCodePages();

// Origem única (ADR 0001): toda rota da API vive sob /api, e o Caddy não corta o prefixo.
var api = app.MapGroup("/api");
api.MapHealthEndpoints();

if (app.Environment.IsDevelopment())
{
    api.MapOpenApi("/openapi/{documentName}.json");
}

await app.RunAsync();
return 0;
