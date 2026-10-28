using ImageGen.Maui.Resources.Strings;

namespace ImageGen.Maui.Services;

public sealed class ShellDialogService : IDialogService
{
    public Task AlertAsync(string title, string message) =>
        Shell.Current.DisplayAlertAsync(title, message, AppResources.Ok);

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        Shell.Current.DisplayAlertAsync(title, message, accept, cancel);

    public Task<string?> PromptAsync(string title, string message, string accept, string cancel, string initialValue) =>
        Shell.Current.DisplayPromptAsync(title, message, accept, cancel, initialValue: initialValue, maxLength: 4000);
}
