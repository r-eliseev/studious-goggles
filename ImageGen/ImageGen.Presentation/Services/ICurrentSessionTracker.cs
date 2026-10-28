namespace ImageGen.Presentation.Services;

/// <summary>Which session's chat is on screen. The chat sets it; the session list marks that row.</summary>
public interface ICurrentSessionTracker
{
    Guid? CurrentId { get; set; }

    /// <summary>Raised after <see cref="CurrentId"/> changes.</summary>
    event Action<Guid?>? Changed;
}
