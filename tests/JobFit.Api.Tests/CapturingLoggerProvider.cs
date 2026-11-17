using Microsoft.Extensions.Logging;

namespace JobFit.Api.Tests;

// Collects every formatted log message and scope, at any level.
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _messages = new();

    public IReadOnlyCollection<string> Messages => _messages;

    public ILogger CreateLogger(string categoryName) => new Logger(_messages);

    public void Dispose()
    {
    }

    private sealed class Logger(System.Collections.Concurrent.ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            messages.Enqueue($"scope: {Format(state)}");
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            messages.Enqueue($"{formatter(state, exception)} {Format(state)} {exception}");
        }

        private static string Format<TState>(TState state) => state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? string.Join(" ", pairs.Select(p => $"{p.Key}={p.Value}"))
            : $"{state}";
    }
}
