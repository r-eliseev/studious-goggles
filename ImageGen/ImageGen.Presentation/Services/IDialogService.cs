namespace ImageGen.Presentation.Services;

public interface IDialogService
{
    Task AlertAsync(string title, string message);

    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

    /// <summary>Like <see cref="ConfirmAsync(string, string, string, string)"/>; <paramref name="destructive"/> marks the accept button as a dangerous action.</summary>
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel, bool destructive);

    /// <summary>Returns the entered text, or null when the user cancelled.</summary>
    Task<string?> PromptAsync(string title, string message, string accept, string cancel, string initialValue);

    /// <summary>Shows a list of options under <paramref name="title"/>; returns the chosen option, or null when the user cancelled.</summary>
    Task<string?> ChooseAsync(string title, string cancel, IReadOnlyList<string> options);
}
