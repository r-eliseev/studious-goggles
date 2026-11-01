using ImageGen.Presentation.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImageGen.Presentation.Tests.TestDoubles;

internal static class ErrorAlerts
{
    public static ErrorAlert Create(IDialogService dialogs) => new(dialogs, NullLogger<ErrorAlert>.Instance);
}
