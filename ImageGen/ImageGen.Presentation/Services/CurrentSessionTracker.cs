namespace ImageGen.Presentation.Services;

/// <summary>One instance for the app: the chat and the session list share it.</summary>
public sealed class CurrentSessionTracker : ICurrentSessionTracker
{
    private Guid? _currentId;

    public Guid? CurrentId
    {
        get => _currentId;
        set
        {
            if (_currentId == value)
            {
                return;
            }

            _currentId = value;
            Changed?.Invoke(value);
        }
    }

    public event Action<Guid?>? Changed;
}
