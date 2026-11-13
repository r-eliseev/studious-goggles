using ImageGen.Presentation.Services;

namespace ImageGen.Acceptance.Tests.Support;

public sealed class FakePlatform : IClipboardService, IDialogService, IImageSaver, IFilePickerService
{
    private readonly Queue<bool> _confirmationAnswers = new();
    private readonly Queue<string?> _promptAnswers = new();

    public List<string> ClipboardWrites { get; } = [];

    public List<(string Title, string Message)> Alerts { get; } = [];

    public List<(string Title, string Message, string Accept, string Cancel, bool Destructive)> Confirmations { get; } = [];

    public List<(string SourcePath, string SuggestedFileName, string MediaType)> SaveRequests { get; } = [];

    public int FilePickerRequests { get; private set; }

    public string ActionLabel => "Save file";

    public void QueueConfirmation(bool answer) => _confirmationAnswers.Enqueue(answer);

    public void QueuePrompt(string? answer) => _promptAnswers.Enqueue(answer);

    public Task SetTextAsync(string text)
    {
        ClipboardWrites.Add(text);
        return Task.CompletedTask;
    }

    public Task AlertAsync(string title, string message)
    {
        Alerts.Add((title, message));
        return Task.CompletedTask;
    }

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        ConfirmAsync(title, message, accept, cancel, destructive: false);

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel, bool destructive)
    {
        Confirmations.Add((title, message, accept, cancel, destructive));
        return Task.FromResult(_confirmationAnswers.TryDequeue(out var answer) ? answer : true);
    }

    public Task<string?> PromptAsync(string title, string message, string accept, string cancel, string initialValue) =>
        Task.FromResult(_promptAnswers.TryDequeue(out var answer) ? answer : null);

    public Task<string?> ChooseAsync(string title, string cancel, IReadOnlyList<string> options) =>
        Task.FromResult<string?>(null);

    public Task<ImageSaveResult> SaveAsync(string sourcePath, string suggestedFileName, string mediaType, CancellationToken cancellationToken)
    {
        SaveRequests.Add((sourcePath, suggestedFileName, mediaType));
        return Task.FromResult(ImageSaveResult.Cancelled);
    }

    public Task<PickedImage?> PickImageAsync(CancellationToken cancellationToken)
    {
        FilePickerRequests++;
        return Task.FromResult<PickedImage?>(null);
    }
}
