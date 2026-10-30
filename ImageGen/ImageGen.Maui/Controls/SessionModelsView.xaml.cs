using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Maui.Controls;

public partial class SessionModelsView : ContentView
{
    public SessionModelsView()
    {
        InitializeComponent();
        ApplyButtonOrder();
    }

    /// <summary>Save first on Windows, Cancel first on macOS (docs/design/implementation.md, section 7).</summary>
    private void ApplyButtonOrder()
    {
        var mac = DeviceInfo.Platform == DevicePlatform.MacCatalyst;
        Grid.SetColumn(SaveButton, mac ? 1 : 0);
        Grid.SetColumn(CancelButton, mac ? 0 : 1);
    }

    /// <summary>A checked radio button chooses the assistant model (task 070).</summary>
    private void OnAssistantCheckedChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (e.Value && sender is RadioButton { BindingContext: AssistantModelOptionViewModel row } && row.SelectCommand.CanExecute(null))
        {
            row.SelectCommand.Execute(null);
        }
    }
}
