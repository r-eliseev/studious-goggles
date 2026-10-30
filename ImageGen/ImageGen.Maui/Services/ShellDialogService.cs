using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using Microsoft.Extensions.Logging;

namespace ImageGen.Maui.Services;

public sealed class ShellDialogService(ILogger<ShellDialogService> logger) : IDialogService
{
    public Task AlertAsync(string title, string message) =>
        Shell.Current.DisplayAlertAsync(title, message, AppResources.Ok);

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        Shell.Current.DisplayAlertAsync(title, message, accept, cancel);

    /// <summary>The system dialog has no destructive style, so the flag only documents the call.</summary>
    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel, bool destructive) =>
        ConfirmAsync(title, message, accept, cancel);

    public Task<string?> PromptAsync(string title, string message, string accept, string cancel, string initialValue) =>
        Shell.Current.DisplayPromptAsync(title, message, accept, cancel, initialValue: initialValue, maxLength: 4000);

    public async Task<string?> ChooseAsync(string title, string cancel, IReadOnlyList<string> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        logger.LogDebug("ChooseAsync started with {OptionCount} options.", options.Count);
        try
        {
            var chosen = await Shell.Current.DisplayActionSheetAsync(title, cancel, destruction: null, [.. options]);
            var result = chosen == cancel ? null : chosen;
            logger.LogDebug("ChooseAsync completed. Cancelled: {WasCancelled}.", result is null);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning("ChooseAsync failed.");
            logger.LogDebug(ex, "ChooseAsync exception.");
            throw;
        }
    }
}
