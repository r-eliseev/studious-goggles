using ImageGen.Core.Domain.Descriptions;
using ImageGen.Core.Domain.Generations;
using ImageGen.Core.Providers;
using ImageGen.Core.Services;
using ImageGen.Presentation.Resources.Strings;
using Microsoft.Extensions.Logging;

namespace ImageGen.Presentation.Services;

/// <summary>Shows a safe localized error and logs its details at Debug.</summary>
public sealed class ErrorAlert(IDialogService dialogs, ILogger<ErrorAlert> logger)
{
    public async Task ShowAsync(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var (code, message) = exception switch
        {
            ProviderException provider => (provider.Kind.ToString(), ErrorKindText.Of(provider.Kind)),
            ImageDescriptionException description => (description.Error.ToString(), DescriptionErrorText.Of(description.Error)),
            _ => (null, AppResources.ErrorKindUnknown),
        };

        logger.LogWarning("UI operation failed with {ExceptionType} and error code {ErrorCode}.", exception.GetType().Name, code);
        logger.LogDebug(exception, "UI operation failure details.");
        await dialogs.AlertAsync(AppResources.ErrorTitle, message);
    }
}
