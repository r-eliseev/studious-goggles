using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ImageGen.Avalonia.Tests.TestDoubles;

/// <summary>Keeps every entry in memory so that a test can check what was logged.</summary>
public sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyCollection<LogEntry> Entries => _entries.ToArray();

    /// <summary>Text of every entry: the message with its exception, if any.</summary>
    public string AllText => string.Join(Environment.NewLine, Entries.Select(e => e.Message + (e.Exception is null ? string.Empty : " " + e.Exception)));

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => new NoopScope();

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        _entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception));

    private sealed class NoopScope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

public sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
