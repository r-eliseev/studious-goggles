using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Presentation.Appearance;

namespace ImageGen.Presentation.PageModels;

/// <summary>One style card on the settings screen: its name, hint and whether it is the chosen style.</summary>
public sealed partial class StyleOptionViewModel(AppearanceStyle style, string name, string hint, Func<AppearanceStyle, Task> select) : ObservableObject
{
    public AppearanceStyle Style { get; } = style;

    public string Name { get; } = name;

    public string Hint { get; } = hint;

    [ObservableProperty]
    private bool _isSelected;

    [RelayCommand]
    private Task SelectAsync() => select(Style);
}
