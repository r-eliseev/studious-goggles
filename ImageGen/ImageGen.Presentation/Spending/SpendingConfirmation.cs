using System.Globalization;
using ImageGen.Core.Domain.Models;
using ImageGen.Core.Domain.Sessions;
using ImageGen.Core.Services;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;

namespace ImageGen.Presentation.Spending;

public static class SpendingConfirmation
{
    public static string FormatEstimate(CostEstimate estimate)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        var price = estimate.KnownUsd.ToString("0.####", CultureInfo.InvariantCulture);
        var amount = string.Format(CultureInfo.InvariantCulture, "${0}", price);
        if (estimate.IsApproximate)
        {
            amount = "≈" + amount;
        }

        return estimate.UnknownModels.Count == 0
            ? amount
            : string.Format(CultureInfo.CurrentCulture, AppResources.CostEstimateWithUnknownFormat, amount, estimate.UnknownModels.Count);
    }

    public static async Task<bool> ConfirmAsync(
        CostEstimator estimator,
        SpendingService spending,
        IDialogService dialogs,
        IReadOnlyList<ModelRef> models,
        GenerationParameters parameters,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(estimator);
        ArgumentNullException.ThrowIfNull(spending);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(parameters);

        var estimate = await estimator.EstimateAsync(models, 1, parameters, cancellationToken);
        if (await spending.CheckAsync(estimate, cancellationToken) == SpendingWarning.None)
        {
            return true;
        }

        var message = string.Format(
            CultureInfo.CurrentCulture,
            AppResources.SpendingConfirmationFormat,
            FormatEstimate(estimate));
        return await dialogs.ConfirmAsync(AppResources.SpendingWarningTitle, message, AppResources.Generate, AppResources.Cancel);
    }
}
