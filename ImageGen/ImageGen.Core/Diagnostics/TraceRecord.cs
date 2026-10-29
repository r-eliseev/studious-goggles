namespace ImageGen.Core.Diagnostics;

/// <summary>
/// One trace record. <paramref name="Kind"/> is <c>user-input</c>, <c>http-request</c> or <c>http-response</c>. Field values
/// are plain text; the keys of the fields are part of the format (<c>docs/specs/debug-trace.md</c>).
/// </summary>
public sealed record TraceRecord(string Kind, IReadOnlyDictionary<string, string?> Fields, IReadOnlyList<TraceBlob> Blobs);
