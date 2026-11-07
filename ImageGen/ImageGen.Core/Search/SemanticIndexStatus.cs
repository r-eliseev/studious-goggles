namespace ImageGen.Core.Search;

public sealed record SemanticIndexStatus(
    bool Enabled,
    int Indexed,
    int Pending,
    SemanticIndexState State,
    string? ErrorCode = null,
    string? WarningCode = null,
    string? ErrorDetail = null);
