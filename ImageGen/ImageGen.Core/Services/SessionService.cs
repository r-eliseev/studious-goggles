using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Storage;

namespace ImageGen.Core.Services;

public sealed class SessionService(
    ISessionRepository sessions,
    IChatMessageRepository messages,
    IRunRepository runs,
    IImageFileStore files,
    TimeProvider timeProvider)
{
    public async Task<Session> CreateAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Title = Session.NewSessionTitle,
            CreatedAt = now,
            UpdatedAt = now,
            Settings = SessionSettings.CreateDefault(),
        };
        await sessions.AddAsync(session, cancellationToken);
        return session;
    }

    public Task<IReadOnlyList<Session>> ListAsync(CancellationToken cancellationToken) => sessions.ListAsync(cancellationToken);

    public Task<Session?> GetAsync(Guid sessionId, CancellationToken cancellationToken) =>
        sessions.GetAsync(sessionId, cancellationToken);

    /// <summary>Deletes the session, its records and its image files.</summary>
    public async Task DeleteAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await sessions.DeleteAsync(sessionId, cancellationToken);
        await files.DeleteSessionFilesAsync(sessionId, cancellationToken);
    }

    /// <summary>Validates and saves new settings. Duplicate image models are removed, keeping the order.</summary>
    public async Task<Session> UpdateSettingsAsync(Guid sessionId, SessionSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.RunsPerModel, SessionSettings.MinRunsPerModel);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(settings.RunsPerModel, SessionSettings.MaxRunsPerModel);
        ArgumentNullException.ThrowIfNull(settings.AssistantModel);

        var session = await sessions.GetAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");
        session.Settings = new SessionSettings
        {
            ImageModels = settings.ImageModels
                .Where(model => model is not null)
                .Distinct()
                .ToList(),
            RunsPerModel = settings.RunsPerModel,
            AssistantModel = settings.AssistantModel,
        };
        session.UpdatedAt = timeProvider.GetUtcNow();
        await sessions.UpdateAsync(session, cancellationToken);
        return session;
    }

    /// <summary>Sets the title from the first user message if the session still has the default title.</summary>
    public async Task SetTitleFromFirstMessageAsync(Guid sessionId, string text, CancellationToken cancellationToken)
    {
        var session = await sessions.GetAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");
        session.UpdatedAt = timeProvider.GetUtcNow();
        if (session.Title == Session.NewSessionTitle)
        {
            var title = text.Trim().ReplaceLineEndings(" ");
            session.Title = title.Length > Session.MaxTitleLength ? title[..Session.MaxTitleLength] : title;
        }

        await sessions.UpdateAsync(session, cancellationToken);
    }

    public async Task<SessionTimeline> GetTimelineAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await sessions.GetAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");
        var sessionMessages = await messages.ListBySessionAsync(sessionId, cancellationToken);
        var sessionRuns = await runs.ListRunsBySessionAsync(sessionId, cancellationToken);
        var generations = await runs.ListGenerationsBySessionAsync(sessionId, cancellationToken);
        return new SessionTimeline(session, sessionMessages, sessionRuns, generations);
    }
}
