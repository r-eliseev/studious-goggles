using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Input;
using ImageGen.Avalonia.Services;
using ImageGen.Presentation;
using ImageGen.Presentation.PageModels;
using ImageGen.Presentation.Resources.Strings;
using ImageGen.Presentation.Services;

namespace ImageGen.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        Title = AppResources.AppTitle;
    }

    /// <summary>Area where pages are shown: the navigation service puts the current page here.</summary>
    public ContentControl PageArea => this.FindControl<ContentControl>("PageHost")!;

    /// <summary>
    /// Shortcuts and the menu bar (task 073): New session, Settings and Find work from every page. The menu bar shows on
    /// macOS; the shortcuts work on every system.
    /// </summary>
    public void ConfigureShortcuts(SessionsPageModel sessions, INavigationService navigation)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(navigation);
        var find = new AsyncRelayCommand(() => FindSessionsAsync(navigation));
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.N, PlatformKeys.Primary), Command = sessions.NewSessionCommand });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.OemComma, PlatformKeys.Primary), Command = sessions.OpenSettingsCommand });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.F, PlatformKeys.Primary), Command = find });
        NativeMenu.SetMenu(this, new NativeMenu
        {
            Items =
            {
                new NativeMenuItem(AppResources.MenuFile)
                {
                    Menu = new NativeMenu
                    {
                        Items =
                        {
                            new NativeMenuItem(AppResources.NewSession)
                            {
                                Gesture = new KeyGesture(Key.N, PlatformKeys.Primary),
                                Command = sessions.NewSessionCommand,
                            },
                            new NativeMenuItem(AppResources.MenuSettings)
                            {
                                Gesture = new KeyGesture(Key.OemComma, PlatformKeys.Primary),
                                Command = sessions.OpenSettingsCommand,
                            },
                        },
                    },
                },
                new NativeMenuItem(AppResources.MenuFind)
                {
                    Gesture = new KeyGesture(Key.F, PlatformKeys.Primary),
                    Command = find,
                },
            },
        });
    }

    /// <summary>Opens the sessions page when another page is shown, then puts the cursor in the search field.</summary>
    private async Task FindSessionsAsync(INavigationService navigation)
    {
        if (PageArea.Content is not SessionsView)
        {
            await navigation.GoToAsync(Routes.Sessions);
        }

        (PageArea.Content as SessionsView)?.FocusSearch();
    }
}
