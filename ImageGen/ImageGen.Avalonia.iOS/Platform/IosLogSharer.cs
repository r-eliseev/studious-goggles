using Foundation;
using ImageGen.Presentation.Services;
using UIKit;

namespace ImageGen.Avalonia.Platform;

/// <summary>Shares log files through the iOS system activity sheet.</summary>
public sealed class IosLogSharer : ILogSharer
{
    public bool IsSupported => true;

    public Task ShareAsync(IReadOnlyList<string> files, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        cancellationToken.ThrowIfCancellationRequested();
        var urls = files.Where(File.Exists).Select(path => NSUrl.FromFilename(path)).ToArray();
        if (urls.Length == 0)
        {
            return Task.CompletedTask;
        }

        var controller = new UIActivityViewController(urls, null);
        var activeScene = UIApplication.SharedApplication.ConnectedScenes
            .OfType<UIWindowScene>()
            .FirstOrDefault(scene => scene.ActivationState == UISceneActivationState.ForegroundActive);
        var window = activeScene?.Windows.FirstOrDefault(candidate => candidate.IsKeyWindow)
            ?? activeScene?.Windows.FirstOrDefault();
        var presenter = window?.RootViewController;
        while (presenter?.PresentedViewController is { } presented)
        {
            presenter = presented;
        }

        if (presenter is null)
        {
            throw new InvalidOperationException("The iOS view controller is not available.");
        }

        if (controller.PopoverPresentationController is { } popover)
        {
            var presenterView = presenter.View
                ?? throw new InvalidOperationException("The iOS presenter view is not available.");
            popover.SourceView = presenterView;
            popover.SourceRect = new CoreGraphics.CGRect(
                presenterView.Bounds.GetMidX(), presenterView.Bounds.GetMidY(), 0, 0);
            popover.PermittedArrowDirections = 0;
        }

        presenter.PresentViewController(controller, animated: true, completionHandler: null);
        return Task.CompletedTask;
    }
}
