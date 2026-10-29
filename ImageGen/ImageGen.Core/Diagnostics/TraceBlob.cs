namespace ImageGen.Core.Diagnostics;

/// <summary>A binary part of a record, such as a picture. It is stored as a file next to the trace.</summary>
public sealed record TraceBlob(string Name, byte[] Content, string MediaType);
