namespace ImageGen.Core.Providers;

/// <summary>An image supplied to a video generation request.</summary>
public sealed record ImageReference(byte[] Content, string MediaType);
