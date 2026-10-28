using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Maui.Pages;

public partial class SessionModelsPage : ContentPage, IQueryAttributable
{
    private readonly SessionModelsPageModel _model;
    private bool _loaded;

    public SessionModelsPage(SessionModelsPageModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;
        ApplyButtonOrder();
    }

    /// <summary>Save first on Windows, Cancel first on macOS (docs/design/implementation.md, section 7).</summary>
    private void ApplyButtonOrder()
    {
        var mac = DeviceInfo.Platform == DevicePlatform.MacCatalyst;
        Grid.SetColumn(SaveButton, mac ? 1 : 0);
        Grid.SetColumn(CancelButton, mac ? 0 : 1);
    }

    /// <summary>A radio button was checked by the user: the row chooses the assistant model (the page model clears the others).</summary>
    private void OnAssistantCheckedChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (e.Value && sender is RadioButton { BindingContext: AssistantModelOptionViewModel row } && row.SelectCommand.CanExecute(null))
        {
            row.SelectCommand.Execute(null);
        }
    }

    /// <summary>Shell passes the route query (the session id) here before the page appears.</summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query) => _model.ApplyQueryAttributes(query);

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        _model.OnAppearingAsync().FireAndForgetSafeAsync();
    }
}
