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
}
