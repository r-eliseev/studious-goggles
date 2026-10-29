namespace ImageGen.Core.Diagnostics;

/// <summary>The trace of the default build: nothing is recorded.</summary>
public sealed class NullDebugTrace : IDebugTrace
{
    public static readonly NullDebugTrace Instance = new();

    public bool IsEnabled => false;

    public void Record(TraceRecord record)
    {
    }
}
