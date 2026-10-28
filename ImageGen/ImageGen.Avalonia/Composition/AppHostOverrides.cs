using ImageGen.Avalonia.Services.Secrets;
using ImageGen.Core.Platform;

namespace ImageGen.Avalonia.Composition;

/// <summary>
/// The data folder and the key store that the app uses instead of the real ones. Only tests set it (before the app
/// starts); the real app leaves <see cref="App.HostOverrides"/> null.
/// </summary>
public sealed record AppHostOverrides(IAppPaths Paths, DesktopSecretStore Secrets);
