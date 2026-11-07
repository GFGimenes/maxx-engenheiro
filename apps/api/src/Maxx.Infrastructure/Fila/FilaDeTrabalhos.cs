using System.Text.Json;
using Maxx.Application.Fila;
using Maxx.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Maxx.Infrastructure.Fila;

// Só acrescenta a linha ao DbContext do escopo: grava junto com a escrita de quem chamou.
internal sealed class FilaDeTrabalhos(MaxxDbContext db, TimeProvider clock) : IFilaDeTrabalhos
{
    public void Enfileirar<TCarga>(TCarga carga, DateTimeOffset? disponivelEm = null)
        where TCarga : ICargaDeTrabalho
    {
        ArgumentNullException.ThrowIfNull(carga);
        var agora = clock.GetUtcNow();
        db.Add(new Trabalho
        {
            Tipo = TCarga.Tipo,
            Carga = JsonSerializer.Serialize(carga, JsonSerializerOptions.Web),
            CriadoEm = agora,
            DisponivelEm = disponivelEm ?? agora,
        });
    }
}

// Um tipo de trabalho que o worker sabe executar: o nome gravado na linha e como
// transformar a carga em JSON numa chamada ao handler.
public sealed class TipoDeTrabalho(string nome, Func<IServiceProvider, string, CancellationToken, Task> executar)
{
    public string Nome { get; } = nome;

    internal Task ExecutarAsync(IServiceProvider servicos, string carga, CancellationToken cancellationToken) =>
        executar(servicos, carga, cancellationToken);
}

public sealed class TiposDeTrabalho(IEnumerable<TipoDeTrabalho> tipos)
{
    // Dois handlers para o mesmo nome é erro de registro: falha ao montar, não no meio da fila.
    private readonly Dictionary<string, TipoDeTrabalho> _porNome = tipos.ToDictionary(tipo => tipo.Nome, StringComparer.Ordinal);

    public TipoDeTrabalho? Buscar(string nome) => _porNome.GetValueOrDefault(nome);
}

public sealed class FilaOptions
{
    // Espera entre duas consultas quando a fila está vazia.
    public TimeSpan IntervaloOcioso { get; set; } = TimeSpan.FromSeconds(1);

    // Passado disso, o trabalho é cancelado e conta como falha: um handler travado não pode
    // segurar o lock da linha para sempre.
    public TimeSpan TempoMaximoPorTrabalho { get; set; } = TimeSpan.FromSeconds(60);
}

public static class FilaServiceCollectionExtensions
{
    // Registra o handler de um tipo. API e worker registram os mesmos tipos: quem enfileira
    // e quem executa conhecem o mesmo nome.
    public static IServiceCollection AddTrabalho<TCarga, THandler>(this IServiceCollection services)
        where TCarga : ICargaDeTrabalho
        where THandler : class, ITrabalhoHandler<TCarga>
    {
        services.AddScoped<ITrabalhoHandler<TCarga>, THandler>();
        services.AddSingleton(new TipoDeTrabalho(TCarga.Tipo, async (servicos, json, cancellationToken) =>
        {
            var carga = JsonSerializer.Deserialize<TCarga>(json, JsonSerializerOptions.Web)
                ?? throw new InvalidOperationException($"Carga vazia para o trabalho {TCarga.Tipo}.");
            await servicos.GetRequiredService<ITrabalhoHandler<TCarga>>().ExecutarAsync(carga, cancellationToken);
        }));
        return services;
    }

    // O consumo contínuo, só no processo do worker (`Maxx.Api worker`). As réplicas da API
    // enfileiram e não consomem.
    public static IServiceCollection AddWorker(this IServiceCollection services)
    {
        services.AddHostedService<ServicoDoWorker>();
        return services;
    }
}
