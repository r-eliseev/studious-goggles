using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using ImageGen.Presentation.Appearance;

namespace ImageGen.Avalonia.Services;

/// <summary>
/// Applies the appearance settings to the running app: swaps the style dictionary and sets the theme variant. Screens
/// bind to the <c>Ig*</c> keys with <c>DynamicResource</c>, so the change shows without a restart.
/// </summary>
public sealed class AvaloniaAppearanceApplier
{
    private const string ThemesFolder = "avares://ImageGen.Avalonia/Themes/";

    private readonly AppearanceService _service;
    private readonly Application _application;

    public AvaloniaAppearanceApplier(AppearanceService service, Application application)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _application = application ?? throw new ArgumentNullException(nameof(application));
    }

    public static string ThemeUri(AppearanceStyle style) => $"{ThemesFolder}{style}.axaml";

    /// <summary>Applies the saved settings now and follows later changes.</summary>
    public void Start()
    {
        Apply(_service.Current);
        _service.Changed += settings =>
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                Apply(settings);
            }
            else
            {
                Dispatcher.UIThread.Post(() => Apply(settings));
            }
        };
    }

    public void Apply(AppearanceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var merged = _application.Resources.MergedDictionaries;
        var index = -1;
        for (var i = 0; i < merged.Count; i++)
        {
            if (merged[i] is ResourceInclude include && IsStyleInclude(include))
            {
                index = i;
                break;
            }
        }

        var style = new ResourceInclude(new Uri(ThemesFolder)) { Source = new Uri(ThemeUri(settings.Style)) };
        if (index >= 0)
        {
            merged[index] = style;
        }
        else
        {
            merged.Add(style);
        }

        _application.RequestedThemeVariant = settings.Theme switch
        {
            AppearanceTheme.Light => ThemeVariant.Light,
            AppearanceTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    private static bool IsStyleInclude(ResourceInclude include) =>
        include.Source is { } source && source.AbsoluteUri.StartsWith(ThemesFolder, StringComparison.Ordinal)
        && !source.AbsoluteUri.EndsWith("/Common.axaml", StringComparison.Ordinal);
}
