namespace ImageGen.Maui.Controls;

/// <summary>
/// The session list with its «New session» button: the left column of the sessions page and, in the wide layout, of the
/// chat page. The binding context is the <c>SessionsPageModel</c>.
/// </summary>
public partial class SessionsListView : ContentView
{
    public SessionsListView()
    {
        InitializeComponent();
    }

    /// <summary>Moves the focus to the search field (the Find menu item, Ctrl/Cmd+F).</summary>
    public void FocusSearch() => SearchField.Focus();
}
