using Maxx.Api.Health;
using Maxx.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Maxx")
    ?? throw new InvalidOperationException("ConnectionStrings:Maxx não configurada.");

builder.Services.AddProblemDetails();
builder.Services.AddInfrastructure(connectionString);

var app = builder.Build();

// Um formato só de erro: ProblemDetails (RFC 9457).
app.UseExceptionHandler();
app.UseStatusCodePages();

// Origem única (ADR 0001): toda rota da API vive sob /api, e o Caddy não corta o prefixo.
var api = app.MapGroup("/api");
api.MapHealthEndpoints();

await app.RunAsync();
