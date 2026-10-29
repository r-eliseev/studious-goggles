using ImageGen.Core.Domain.Generations;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Presentation.Services;

/// <summary>
/// The text the interface shows for an error code (task 043). The core only sends codes; the words come from
/// <c>AppResources</c>. A code without a text falls back to the unknown-error text.
/// </summary>
public static class ErrorKindText
{
    public static string Of(ErrorKind kind) => kind switch
    {
        ErrorKind.InvalidKey => AppResources.ErrorKindInvalidKey,
        ErrorKind.InsufficientFunds => AppResources.ErrorKindInsufficientFunds,
        ErrorKind.Moderated => AppResources.ErrorKindModerated,
        ErrorKind.Forbidden => AppResources.ErrorKindForbidden,
        ErrorKind.BadRequest => AppResources.ErrorKindBadRequest,
        ErrorKind.Timeout => AppResources.ErrorKindTimeout,
        ErrorKind.RateLimited => AppResources.ErrorKindRateLimited,
        ErrorKind.ModelUnavailable => AppResources.ErrorKindModelUnavailable,
        ErrorKind.Network => AppResources.ErrorKindNetwork,
        ErrorKind.Interrupted => AppResources.ErrorKindInterrupted,
        ErrorKind.EmptyResponse => AppResources.ErrorKindEmptyResponse,
        ErrorKind.InvalidToolCall => AppResources.ErrorKindInvalidToolCall,
        ErrorKind.AttachmentUnreadable => AppResources.ErrorKindAttachmentUnreadable,
        ErrorKind.ReferenceUnreadable => AppResources.ErrorKindReferenceUnreadable,
        ErrorKind.ModelWithoutReferences => AppResources.ErrorKindModelWithoutReferences,
        _ => AppResources.ErrorKindUnknown,
    };
}
