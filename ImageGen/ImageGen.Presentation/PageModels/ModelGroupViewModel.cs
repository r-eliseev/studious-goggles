using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Presentation.Appearance;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Presentation.PageModels;

public abstract partial class ModelGroupViewModel : ObservableObject
{
    private readonly Action<ModelGroupViewModel> _expandedChanged;

    protected ModelGroupViewModel(string key, string name, bool isExpanded, Action<ModelGroupViewModel> expandedChanged)
    {
        Key = key;
        Name = name;
        _isExpanded = isExpanded;
        _expandedChanged = expandedChanged;
    }

    public string Key { get; }

    public string Name { get; }

    [ObservableProperty]
    private bool _isExpanded;

    public abstract string HeaderText { get; }

    public string AutomationName =>
        $"{HeaderText}, {(IsExpanded ? AppResources.ModelGroupExpanded : AppResources.ModelGroupCollapsed)}";

    public string ChevronGlyph => IsExpanded ? IconGlyphs.Expand : IconGlyphs.Forward;

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;

    protected abstract void FillRows();

    partial void OnIsExpandedChanged(bool value)
    {
        FillRows();
        _expandedChanged(this);
        OnPropertyChanged(nameof(AutomationName));
        OnPropertyChanged(nameof(ChevronGlyph));
    }
}
