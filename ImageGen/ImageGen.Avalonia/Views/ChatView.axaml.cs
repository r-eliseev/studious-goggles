using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ImageGen.Avalonia.Services.Navigation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Utilities;

namespace ImageGen.Avalonia.Views;

/// <summary>Chat of one session. Ctrl+Enter sends the message; Enter starts a new line.</summary>
public partial class ChatView : UserControl, IAppearing, IDisappearing
{
    public ChatView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void OnAppearing()
    {
        if (DataContext is not ChatPageModel model)
        {
            return;
        }

        model.Items.CollectionChanged -= OnItemsChanged;
        model.Items.CollectionChanged += OnItemsChanged;
        model.OnAppearingAsync().FireAndForgetSafeAsync();
    }

    public void OnDisappearing()
    {
        if (DataContext is not ChatPageModel model)
        {
            return;
        }

        model.Items.CollectionChanged -= OnItemsChanged;
        model.OnDisappearing();
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        if (DataContext is ChatPageModel model && model.SendCommand.CanExecute(null))
        {
            model.SendCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>New rows go to the bottom of the transcript; the scroll waits until the layout has the new row.</summary>
    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.UIThread.Post(
            () => this.FindControl<ScrollViewer>("Transcript")?.ScrollToEnd(),
            DispatcherPriority.Background);
}
