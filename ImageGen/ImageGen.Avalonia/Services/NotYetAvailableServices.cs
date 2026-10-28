using ImageGen.Presentation.Services;

namespace ImageGen.Avalonia.Services;

/// <summary>
/// Placeholders for navigation and dialogs until task 041 (the Avalonia shell has no pages yet).
/// Any call fails loudly instead of doing nothing.
/// </summary>
public sealed class NavigationNotAvailable : INavigationService
{
    public Task GoToAsync(string route) => throw new NotSupportedException("Навигация появится в задаче 041.");

    public Task GoBackAsync() => throw new NotSupportedException("Навигация появится в задаче 041.");
}

public sealed class DialogsNotAvailable : IDialogService
{
    public Task AlertAsync(string title, string message) => throw new NotSupportedException("Диалоги появятся в задаче 041.");

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        throw new NotSupportedException("Диалоги появятся в задаче 041.");

    public Task<string?> PromptAsync(string title, string message, string accept, string cancel, string initialValue) =>
        throw new NotSupportedException("Диалоги появятся в задаче 041.");
}
