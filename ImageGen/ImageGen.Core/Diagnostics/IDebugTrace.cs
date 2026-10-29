namespace ImageGen.Core.Diagnostics;

/// <summary>
/// The debug-mode record of user inputs and provider traffic (task 085, <c>docs/specs/debug-trace.md</c>). Off by default:
/// callers check <see cref="IsEnabled"/> before they build a record.
/// </summary>
public interface IDebugTrace
{
    bool IsEnabled { get; }

    /// <summary>Writes one record. Blobs (images) are stored as files; the record refers to them by name.</summary>
    void Record(TraceRecord record);
}
