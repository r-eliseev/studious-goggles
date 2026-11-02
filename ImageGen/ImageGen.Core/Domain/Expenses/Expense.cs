namespace ImageGen.Core.Domain.Expenses;

public sealed class Expense
{
    public required string OperationId { get; set; }

    public required ExpenseKind Kind { get; set; }

    public required string ProviderId { get; set; }

    public required string ModelId { get; set; }

    public required decimal CostUsd { get; set; }

    public required DateTimeOffset OccurredAt { get; set; }

    public Guid? SessionId { get; set; }
}
