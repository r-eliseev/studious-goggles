using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Presentation.PageModels;

/// <summary>One row of the session list. The commands call the functions of the owning page model.</summary>
public sealed partial class SessionItemViewModel : ObservableObject
{
    private readonly Func<SessionItemViewModel, Task> _open;
    private readonly Func<SessionItemViewModel, Task> _rename;
    private readonly Func<SessionItemViewModel, Task> _delete;

    public SessionItemViewModel(
        Session session,
        Func<SessionItemViewModel, Task> open,
        Func<SessionItemViewModel, Task> rename,
        Func<SessionItemViewModel, Task> delete)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(rename);
        ArgumentNullException.ThrowIfNull(delete);
        _open = open;
        _rename = rename;
        _delete = delete;
        Id = session.Id;
        _title = session.Title;
        UpdatedText = session.UpdatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    }

    public Guid Id { get; }

    [ObservableProperty]
    private string _title;

    public string UpdatedText { get; }

    /// <summary>Number of runs of the session; the list shows it next to the time of the last change.</summary>
    [ObservableProperty]
    private int _runCount;

    /// <summary>The second line of the row: «<time> · N runs».</summary>
    public string DetailsText => $"{UpdatedText} · {RunsText}";

    private string RunsText => RunCount == 1
        ? AppResources.RunsCountOne
        : string.Format(CultureInfo.CurrentCulture, AppResources.RunsCountFormat, RunCount);

    /// <summary>While a search is shown: «<where matched>: <fragment>», for example «prompt: red fox…». Empty otherwise.</summary>
    [ObservableProperty]
    private string _searchLine = "";

    /// <summary>True for the session whose chat is on screen; the list marks it.</summary>
    [ObservableProperty]
    private bool _isCurrent;

    partial void OnRunCountChanged(int value) => OnPropertyChanged(nameof(DetailsText));

    [RelayCommand]
    private Task OpenAsync() => _open(this);

    [RelayCommand]
    private Task RenameAsync() => _rename(this);

    [RelayCommand]
    private Task DeleteAsync() => _delete(this);
}
