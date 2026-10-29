using ImageGen.Core.Domain.Chat;

namespace ImageGen.Core.Context;

/// <summary>The messages a new summary covers (C3.3): they are written in order, and the summary covers up to the last of them.</summary>
public sealed record SummaryPlan(IReadOnlyList<ChatMessage> ToSummarize, Guid CoversUpToMessageId);
