using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Maui.Pages;

public partial class SessionParametersPage : ContentPage, IQueryAttributable
{
    private readonly SessionParametersPageModel _model;
    private bool _loaded;

    public SessionParametersPage(SessionParametersPageModel model)
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
