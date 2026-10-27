using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Sessions;

namespace ImageGen.Maui.PageModels;

/// <summary>Row of the sessions list. Commands call back into the owning page model.</summary>
public sealed class SessionItemViewModel
{
    public SessionItemViewModel(
        Session session,
        Func<SessionItemViewModel, Task> open,
        Func<SessionItemViewModel, Task> delete)
    {
        Id = session.Id;
        Title = session.Title;
        UpdatedText = session.UpdatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        OpenCommand = new AsyncRelayCommand(() => open(this));
        DeleteCommand = new AsyncRelayCommand(() => delete(this));
    }

    public Guid Id { get; }

    public string Title { get; }

    public string UpdatedText { get; }

    public IAsyncRelayCommand OpenCommand { get; }

    public IAsyncRelayCommand DeleteCommand { get; }
}
