namespace ImageGen.Presentation.PageModels;

/// <summary>Status of a generation card for the view: one placeholder and one status label per value.</summary>
public enum GenerationStatusKind
{
    Done,
    Running,
    Queued,
    Failed,
    Filtered,
    Cancelled,
}
