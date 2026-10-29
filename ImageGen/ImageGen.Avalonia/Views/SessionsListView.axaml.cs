using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ImageGen.Avalonia.Views;

/// <summary>
/// The session list with its «New session» button: the left column of the sessions page and, in the wide layout, of the
/// chat page. The data context is <see cref="ImageGen.Presentation.PageModels.SessionsPageModel"/>.
/// </summary>
public partial class SessionsListView : UserControl
{
    public SessionsListView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>Moves the focus to the search field.</summary>
    public void FocusSearch() => this.FindControl<TextBox>("SearchBox")?.Focus();
}
