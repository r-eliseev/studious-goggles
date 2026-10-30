namespace ImageGen.Core.Domain.Generations;

public enum ErrorKind
{
    Unknown = 0,
    InvalidKey = 1,
    InsufficientFunds = 2,
    Moderated = 3,
    Forbidden = 4,
    BadRequest = 5,
    Timeout = 6,
    RateLimited = 7,
    ModelUnavailable = 8,
    Network = 9,
    Interrupted = 10,
    EmptyResponse = 11,
    /// <summary>The assistant asked for a tool call without a valid prompt.</summary>
    InvalidToolCall = 12,
    /// <summary>An image attached to the user's message could not be read.</summary>
    AttachmentUnreadable = 13,
    /// <summary>A reference image of a generation is missing or unreadable.</summary>
    ReferenceUnreadable = 14,
    /// <summary>The model does not take reference images, so the assistant could not start the run.</summary>
    ModelWithoutReferences = 15,
    /// <summary>The generated image could not be saved locally.</summary>
    ImageSaveFailed = 16,
}
