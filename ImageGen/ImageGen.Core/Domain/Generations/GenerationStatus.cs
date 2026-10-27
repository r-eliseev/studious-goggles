namespace ImageGen.Core.Domain.Generations;

public enum GenerationStatus
{
    Queued = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Cancelled = 4,
    Filtered = 5,
}
