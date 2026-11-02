using ImageGen.Core.Domain.Expenses;

namespace ImageGen.Core.Storage;

public interface IExpenseRepository
{
    Task<IReadOnlyList<Expense>> ListAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);

    Task<decimal> SumAsync(CancellationToken cancellationToken);

    Task<int> CountAsync(CancellationToken cancellationToken);
}
