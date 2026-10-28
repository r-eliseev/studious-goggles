namespace ImageGen.Presentation.Services;

public interface IDialogService
{
    Task AlertAsync(string title, string message);

    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

    /// <summary>Returns the entered text, or null when the user cancelled.</summary>
    Task<string?> PromptAsync(string title, string message, string accept, string cancel, string initialValue);
}
