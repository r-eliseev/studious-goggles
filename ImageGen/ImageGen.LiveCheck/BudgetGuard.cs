namespace ImageGen.LiveCheck;

public sealed class BudgetGuard(decimal budgetUsd, int maxRequests)
{
    public decimal BudgetUsd { get; } = budgetUsd > 0 ? budgetUsd : throw new ArgumentOutOfRangeException(nameof(budgetUsd));

    public int MaxRequests { get; } = maxRequests > 0 ? maxRequests : throw new ArgumentOutOfRangeException(nameof(maxRequests));

    public decimal SpentUsd { get; private set; }

    public int Requests { get; private set; }

    public bool TryReserve(decimal estimatedCostUsd)
    {
        if (estimatedCostUsd < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedCostUsd));
        }

        if (Requests >= MaxRequests || SpentUsd + estimatedCostUsd > BudgetUsd)
        {
            return false;
        }

        Requests++;
        SpentUsd += estimatedCostUsd;
        return true;
    }
}
