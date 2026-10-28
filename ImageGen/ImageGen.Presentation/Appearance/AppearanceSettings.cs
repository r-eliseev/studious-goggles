namespace ImageGen.Presentation.Appearance;

public sealed record AppearanceSettings(AppearanceStyle Style, AppearanceTheme Theme)
{
    public static AppearanceSettings Default { get; } = new(AppearanceStyle.Quiet, AppearanceTheme.System);
}
