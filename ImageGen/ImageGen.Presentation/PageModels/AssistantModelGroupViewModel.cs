using System.Collections.ObjectModel;
using System.Globalization;
using ImageGen.Presentation.Resources.Strings;

namespace ImageGen.Presentation.PageModels;

public sealed class AssistantModelGroupViewModel : ModelGroupViewModel
{
    public AssistantModelGroupViewModel(
        string key,
        string name,
        IReadOnlyList<AssistantModelOptionViewModel> matches,
        bool isExpanded,
        Action<ModelGroupViewModel> expandedChanged)
        : base(key, name, isExpanded, expandedChanged)
    {
        Matches = matches;
        Rows = [];
        FillRows();
    }

    public IReadOnlyList<AssistantModelOptionViewModel> Matches { get; }

    public ObservableCollection<AssistantModelOptionViewModel> Rows { get; }

    public override string HeaderText => string.Format(
        CultureInfo.CurrentCulture,
        AppResources.ModelGroupAssistantHeaderFormat,
        Name,
        Matches.Count);

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
