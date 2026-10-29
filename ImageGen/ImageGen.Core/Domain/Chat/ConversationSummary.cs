using ImageGen.Core.Domain.Models;

namespace ImageGen.Core.Domain.Chat;

/// <summary>
/// The assistant's own short text about the older part of a session's history (C3.4, ADR 0015). One per session: a new
/// summary replaces the old one. It covers the messages up to <see cref="CoversUpToMessageId"/>, inclusive.
/// </summary>
public sealed class ConversationSummary
{
    public required Guid SessionId { get; set; }

    public required string Text { get; set; }

    public required Guid CoversUpToMessageId { get; set; }

    /// <summary>Assistant model that wrote the summary.</summary>
    public required ModelRef Model { get; set; }

    /// <summary>Price of the request, or null when the provider did not say.</summary>
    public decimal? CostUsd { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }
}
