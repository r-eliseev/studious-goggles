using System.Text.RegularExpressions;
using ImageGen.Core.Domain.Chat;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Storage;

namespace ImageGen.Core.Services;

public sealed class SessionService(
    ISessionRepository sessions,
    IChatMessageRepository messages,
    IRunRepository runs,
    IImageFileStore files,
    ChatModelCatalogService chatModels,
    TimeProvider timeProvider,
    IConversationSummaryRepository summaries,
    SessionChangeNotifier changes,
    ISessionGenerationCoordinator generationCoordinator)
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

    /// <summary>Runs per session in one query; sessions without runs are absent.</summary>
    public Task<IReadOnlyDictionary<Guid, int>> CountRunsAsync(CancellationToken cancellationToken) =>
        runs.CountRunsBySessionAsync(cancellationToken);

    /// <summary>The assistant's summary of the session's older history, or null when there is none (task 084).</summary>
    public Task<ConversationSummary?> GetSummaryAsync(Guid sessionId, CancellationToken cancellationToken) =>
        summaries.GetAsync(sessionId, cancellationToken);

    public Task<Session?> GetAsync(Guid sessionId, CancellationToken cancellationToken) =>
        sessions.GetAsync(sessionId, cancellationToken);

    /// <summary>Deletes the session, its records and its image files.</summary>
    public async Task DeleteAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await generationCoordinator.StopSessionAsync(sessionId, CancellationToken.None);
        await sessions.DeleteAsync(sessionId, CancellationToken.None);
        await files.DeleteSessionFilesAsync(sessionId, CancellationToken.None);
        changes.Notify(sessionId);
    }

    /// <summary>Validates and saves new settings. Duplicate image models are removed, keeping the order.</summary>
    public async Task<Session> UpdateSettingsAsync(Guid sessionId, SessionSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.RunsPerModel, SessionSettings.MinRunsPerModel);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(settings.RunsPerModel, SessionSettings.MaxRunsPerModel);
        ArgumentNullException.ThrowIfNull(settings.AssistantModel);

        await EnsureAssistantCanUseToolsAsync(settings.AssistantModel, cancellationToken);

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
            // The parameters are saved by UpdateGenerationParametersAsync; this screen does not change them.
            Parameters = session.Settings.Parameters,
        };
        session.UpdatedAt = timeProvider.GetUtcNow();
        await sessions.UpdateAsync(session, cancellationToken);
        changes.Notify(sessionId);
        return session;
    }

    /// <summary>
    /// The assistant starts runs through tools, so a model without them is refused. When the catalog cannot be
    /// loaded the check is skipped: the choice is saved and the next message shows the provider's answer.
    /// </summary>
    private async Task EnsureAssistantCanUseToolsAsync(ModelRef assistant, CancellationToken cancellationToken)
    {
        var models = await chatModels.FindModelsAsync(assistant.ProviderId, cancellationToken);
        if (models is not null && !models.Any(m => m.Id == assistant.ModelId && m.SupportsTools))
        {
            throw new ArgumentException(
                $"Model {assistant.ModelId} cannot be the assistant: the provider does not list tools for it.",
                nameof(assistant));
        }
    }

    /// <summary>
    /// Validates and saves the parameters of the session. Models and runs are not changed; the next runs use the new
    /// parameters, the existing ones keep their own (F4.2).
    /// </summary>
    public async Task<Session> UpdateGenerationParametersAsync(
        Guid sessionId,
        GenerationParameters parameters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ValidateParameters(parameters);

        var session = await sessions.GetAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");
        session.Settings.Parameters = parameters;
        session.UpdatedAt = timeProvider.GetUtcNow();
        await sessions.UpdateAsync(session, cancellationToken);
        changes.Notify(sessionId);
        return session;
    }

    private static readonly Regex AspectRatioPattern = new(
        @"^(auto|\d+(\.\d+)?:\d+(\.\d+)?)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly string[] OutputFormats = ["png", "jpeg", "webp"];

    private static void ValidateParameters(GenerationParameters parameters)
    {
        if (parameters.AspectRatio is not null && !IsValidAspectRatio(parameters.AspectRatio))
        {
            throw new ArgumentException("Aspect ratio must be \"W:H\" with positive numbers, or \"auto\".", nameof(parameters));
        }

        ValidateText(parameters.Resolution, 16, nameof(parameters.Resolution));
        ValidateText(parameters.Quality, 32, nameof(parameters.Quality));
        if (parameters.OutputFormat is not null && Array.IndexOf(OutputFormats, parameters.OutputFormat) < 0)
        {
            throw new ArgumentException("Output format must be png, jpeg or webp.", nameof(parameters));
        }
    }

    private static bool IsValidAspectRatio(string value)
    {
        if (!AspectRatioPattern.IsMatch(value))
        {
            return false;
        }

        if (string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var parts = value.Split(':');
        return decimal.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture) > 0
            && decimal.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    private static void ValidateText(string? value, int maxLength, string name)
    {
        if (value is null)
        {
            return;
        }

        if (value.Length == 0 || value.Length > maxLength || value.Trim().Length == 0)
        {
            throw new ArgumentException($"{name} must be 1 to {maxLength} characters.", name);
        }
    }

    /// <summary>
    /// Renames the session. The title is trimmed and cut to <see cref="Session.MaxTitleLength"/>. The change does not move
    /// the session up the list, so <see cref="Session.UpdatedAt"/> stays as it was.
    /// </summary>
    public async Task<Session> RenameAsync(Guid sessionId, string title, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var session = await sessions.GetAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");
        var trimmed = title.Trim();
        session.Title = trimmed.Length > Session.MaxTitleLength ? trimmed[..Session.MaxTitleLength] : trimmed;
        await sessions.UpdateAsync(session, cancellationToken);
        changes.Notify(sessionId);
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
        changes.Notify(sessionId);
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
