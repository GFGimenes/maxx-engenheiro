using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Maxx.Api.IntegrationTests;

// Guarda cada linha de log da aplicação, com a exceção, para o teste procurar o que não
// pode aparecer (senha, token).
public sealed class ColetorDeLog : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _linhas = new();

    public IReadOnlyCollection<string> Linhas => _linhas;

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _linhas);

    public void Dispose()
    {
    }

    private sealed class Logger(string categoria, ConcurrentQueue<string> linhas) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            linhas.Enqueue($"{logLevel} {categoria}: {formatter(state, exception)} {exception}");
        }
    }
}
