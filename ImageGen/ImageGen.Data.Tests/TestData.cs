using ImageGen.Core.Domain;

namespace ImageGen.Data.Tests;

/// <summary>Factory methods for test entities.</summary>
public static class TestData
{
    public static readonly DateTimeOffset BaseTime = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    public static Session Session(DateTimeOffset? updatedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        Title = "Test session",
        CreatedAt = BaseTime,
        UpdatedAt = updatedAt ?? BaseTime,
        Settings = SessionSettings.CreateDefault(),
    };

    public static ChatMessage Message(Guid sessionId, DateTimeOffset createdAt, string text = "hello") => new()
    {
        Id = Guid.NewGuid(),
        SessionId = sessionId,
        Role = ChatRole.User,
        Text = text,
        CreatedAt = createdAt,
    };

    public static Run Run(Guid sessionId, DateTimeOffset createdAt) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = sessionId,
        Prompt = "a red fox",
        Origin = RunOrigin.Assistant,
        CreatedAt = createdAt,
    };

    public static Generation Generation(Run run, int sortOrder, GenerationStatus status = GenerationStatus.Queued) => new()
    {
        Id = Guid.NewGuid(),
        RunId = run.Id,
        SessionId = run.SessionId,
        ModelId = "black-forest-labs/flux.2-klein-4b",
        Prompt = run.Prompt,
        Seed = 42 + sortOrder,
        SeedApplied = true,
        Status = status,
        CreatedAt = run.CreatedAt,
        SortOrder = sortOrder,
    };
}
