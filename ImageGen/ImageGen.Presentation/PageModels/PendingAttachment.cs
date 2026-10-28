using CommunityToolkit.Mvvm.Input;

namespace ImageGen.Presentation.PageModels;

/// <summary>An image attached to the message being written; it is sent with the message.</summary>
/// <param name="Remove">Removes this attachment from the message; the view binds its button to it.</param>
public sealed record PendingAttachment(Guid Id, string FileName, IRelayCommand<PendingAttachment?> Remove);
