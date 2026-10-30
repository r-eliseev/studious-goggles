using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ImageGen.Core.Tests.TestDoubles;

/// <summary>Keeps every entry in memory so that a test can check what was logged.</summary>
public sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private readonly LogLevel _minimumLevel;

    public CapturingLogger(LogLevel minimumLevel = LogLevel.Trace)
    {
        _minimumLevel = minimumLevel;
    }

    public IReadOnlyCollection<LogEntry> Entries => _entries.ToArray();

    /// <summary>Text of every entry: the message with its exception, if any.</summary>
    public string AllText => string.Join(Environment.NewLine, Entries.Select(e => e.Message + (e.Exception is null ? string.Empty : " " + e.Exception)));

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => new NoopScope();

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= _minimumLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        _entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception)
        {
            State = StructuredState(state),
        });
    }

    /// <summary>The structured properties of the logged state, copied at the moment of the call.</summary>
    private static IReadOnlyDictionary<string, object?> StructuredState<TState>(TState state)
    {
        if (state is not IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            return new Dictionary<string, object?>();
        }

        // {OriginalFormat} is added by the logging extensions themselves; it is not a property of the caller.
        return pairs
            .Where(pair => pair.Key != "{OriginalFormat}")
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private sealed class NoopScope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

public sealed record LogEntry(LogLevel Level, string Message, Exception? Exception)
{
    /// <summary>The structured properties of the entry; empty when the call carried none.</summary>
    public IReadOnlyDictionary<string, object?> State { get; init; } = new Dictionary<string, object?>();
}
