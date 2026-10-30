using System.Collections.ObjectModel;
using System.Globalization;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Presentation.PageModels;

public sealed class ImageModelGroupViewModel : ModelGroupViewModel
{
    public ImageModelGroupViewModel(
        string key,
        string name,
        IReadOnlyList<ModelOptionViewModel> matches,
        bool isExpanded,
        Action<ModelGroupViewModel> expandedChanged)
        : base(key, name, isExpanded, expandedChanged)
    {
        Matches = matches;
        Rows = [];
        FillRows();
    }

    public IReadOnlyList<ModelOptionViewModel> Matches { get; }

    public ObservableCollection<ModelOptionViewModel> Rows { get; }

    public int SelectedCount => Matches.Count(model => model.IsSelected);

    public override string HeaderText => string.Format(
        CultureInfo.CurrentCulture,
        AppResources.ModelGroupImageHeaderFormat,
        Name,
        Matches.Count,
        SelectedCount);

    public void NotifySelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HeaderText));
        OnPropertyChanged(nameof(AutomationName));
    }

    protected override void FillRows()
    {
        Rows.Clear();
        if (IsExpanded)
        {
            foreach (var model in Matches)
            {
                Rows.Add(model);
            }
        }
    }
}
