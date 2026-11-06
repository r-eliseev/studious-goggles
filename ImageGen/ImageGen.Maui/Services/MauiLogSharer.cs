using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace ImageGen.Maui.Services;

/// <summary>Shares log files through the platform share sheet on phones.</summary>
public sealed class MauiLogSharer : ILogSharer
{
    public bool IsSupported => DeviceInfo.Idiom != DeviceIdiom.Desktop;

    public Task ShareAsync(IReadOnlyList<string> files, CancellationToken cancellationToken) =>
        Share.Default.RequestAsync(new ShareMultipleFilesRequest
        {
            Title = AppResources.SettingsShareLogsTitle,
            Files = files.Select(file => new ShareFile(file, "text/plain")).ToList(),
        });
}
