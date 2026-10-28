using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Sessions;

namespace ImageGen.Maui.PageModels;

/// <summary>One row of the session list. The commands call the functions of the owning page model.</summary>
public sealed partial class SessionItemViewModel
{
    private readonly Func<SessionItemViewModel, Task> _open;
    private readonly Func<SessionItemViewModel, Task> _delete;

    public SessionItemViewModel(Session session, Func<SessionItemViewModel, Task> open, Func<SessionItemViewModel, Task> delete)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(delete);
        _open = open;
        _delete = delete;
        Id = session.Id;
        Title = session.Title;
        UpdatedText = session.UpdatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    }

    public Guid Id { get; }

    public string Title { get; }

    public string UpdatedText { get; }

    [RelayCommand]
    private Task OpenAsync() => _open(this);

    [RelayCommand]
    private Task DeleteAsync() => _delete(this);
}
