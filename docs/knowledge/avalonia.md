# Avalonia: non-obvious facts

Facts found while working on `ImageGen.Avalonia` (task 039). Checked 2026-10-28 in a cloud session (Linux), Avalonia 12.1.4, .NET 10.0.403. A general overview of environments: `docs/knowledge/dev-environment.md`.

## Avalonia MAUI Backend

Evaluation of 2026-10-28: `Avalonia.Controls.Maui` lets MAUI markup run inside Avalonia and could potentially give one head for Linux and the browser. On the check date the package was the pre-release `11.0.0-rc.3` and required .NET 11 and MAUI 11, while the project uses .NET 10. Limits were noted for `MainThread`, `SecureStorage`, accessibility and Shell. The decision of ADR 0006 does not change: two heads stay; it makes sense to revisit the approach after a stable release for the current stack.

## Android and iOS (research 094)

Checked 2026-10-30. Avalonia 12.1.4, .NET SDK 10.0.403, Android target `net10.0-android36.0`, `Avalonia.Android` and `Avalonia.Themes.Fluent` restored and built the probe on Linux. The `Avalonia.Templates@12.1.4` template creates Android/iOS/Desktop/Browser heads; Android uses `Application : AvaloniaAndroidApplication<App>`, `MainActivity : AvaloniaMainActivity` and `MainViewFactory`, because Android may create several Activities.

The probe linked the current `Views/*.axaml`, code-behind, Presentation, themes, fonts, navigation and platform services into the mobile head. A Debug APK built without warnings. A live launch was not checked (`adb devices` was empty); KeyStore, backup/restore, keyboard, Back, clipboard, picker, gallery, overlay and TalkBack were not checked. The scenarios were handed to task 140. The `android-arm64` Release APK was 29,152,195 bytes (27.8 MiB), 6,968,679 bytes (6.65 MiB) more than the MAUI APK of 22,183,516 bytes (21.2 MiB); the actual install size was not measured.

iOS was checked only against the official Avalonia documentation as of 2026-10-30: the build needs a Mac and Xcode (or a remote Mac build host). The probe contained the template iOS head, but the workload and the build were not checked; there was no device and no Mac.

## Tests
- `Avalonia.Headless.XUnit` 12.1.4 does not work with `xunit.v3` 4.0.2: a `MissingMethodException` at test discovery. The decision and the workaround: `docs/decisions/0007-avalonia-tests-xunit-3-2.md` (`VersionOverride` 3.2.2 only in `ImageGen.Avalonia.Tests`).
- The headless test build runs without a screen and without X11 (checked on this machine).
- In a headless environment `Application.ApplicationLifetime` is `null`: a window created only through `IClassicDesktopStyleApplicationLifetime` does not appear in tests. So `App` always creates `MainWindow` and keeps it in `App.MainWindow`.
- `Dispatcher.UIThread.RunJobs()` runs deferred actions (`Post`) in tests; `Dispatcher.UIThread.CheckAccess()` confirms the thread.

## XAML
- A namespace from another assembly (`ImageGen.Presentation`) is written in Avalonia markup with a qualifier: `xmlns:pm="clr-namespace:ImageGen.Presentation.PageModels;assembly=ImageGen.Presentation"`. Without `;assembly=` the compiler gives `AVLN2000` ("Unable to resolve type … from namespace"), even when the assembly is referenced. The same applied to `x:Static` on `AppResources`.
- UI strings in markup: the `{markup:Res Key}` extension (`ImageGen.Avalonia/Markup/ResExtension.cs`), it reads an `AppResources` property. The window title is set in code: `Title = AppResources.AppTitle`.
- `x:Name="PageHost"` generates a `PageHost` member (internal or private). A public accessor of our own with the same name gives `CS0102`; so the `MainWindow` accessor is called `PageArea`, and the XAML has `Name="PageHost"` for `FindControl`.
- `TextBox.Watermark` is marked obsolete: `PlaceholderText` (warning AVLN5001 when building with `TreatWarningsAsErrors`).
- `AvaloniaUseCompiledBindingsByDefault=true` is on; bindings in markup must have `x:DataType`. We set `Mode=TwoWay` for `TextBox.Text` explicitly.
- Inside `namespace ImageGen.Avalonia` the full names `Avalonia.Controls.X` do not resolve (the namespace is shadowed): we use `using`.

## Chat and cards (task 042)
- `Bitmap(path)` for a file that is not a picture does not throw: it gives a 1×1 bitmap (checked on 12.1.4). The `FilePathToBitmapConverter` still returns `null` for an empty path and a missing file.
- On Windows `Bitmap(path)` may keep the source file open while the bitmap is alive; this prevents deleting the PNG from the temporary folder right after the UI closes. For cards, decode the bytes from memory (`Bitmap(Stream)`) so as not to lock the file on disk. Checked by Windows CI on 2026-10-30; the Avalonia viewer already reads the bytes and passes a `MemoryStream`.
- Elements of `ControlTemplate` templates (for example, the counter buttons of `NumericUpDown`) have a `TemplatedParent`; we skip them when checking accessibility names.
- The visual tree of a new page appears after a layout pass: `Layoutable.UpdateLayout()` in a test.
- A window snapshot in headless: `UseSkia()` and `UseHeadlessDrawing = false` in `AvaloniaHeadlessPlatformOptions`, then `AvaloniaHeadlessPlatform.ForceRenderTimerTick()` and `CaptureRenderedFrame()`. Not used in ordinary tests: rendering slows them down and logic checks do not need it.
- Feed scrolling: `ScrollViewer.ScrollToEnd()` through `Dispatcher.UIThread.Post` after the collection changes (otherwise the new row has no size yet).

## Navigation and dialogs (task 041)
- A page stack in a `ContentControl` of the main window (`AvaloniaNavigationService`); the routes are the same `Routes` strings as in MAUI. A shown page gets `OnAppearing` (the `IAppearing` interface), including when going back.
- Dialogs (`AvaloniaDialogService`) are built in code, not in XAML: a window with a text, a field and buttons, each control has `AutomationProperties.Name`. Waiting for the answer goes through a `TaskCompletionSource`; the window is non-modal (the owner is set, there is no modality). In headless tests the dialogs are substituted.

## Build and launch
- `Avalonia.Desktop` plugs in `UsePlatformDetect()`. Launching on Linux without a display: `xvfb-run -a <program>`; the window can be captured with `import -window root <file.png>` (ImageMagick). Checked 2026-10-28: a 960×640 window is created and shown, the process lives without exceptions.

## Desktop secrets (ADR 0008)
- The `gnome-keyring` package is installed through apt (Ubuntu 24.04). The `org.freedesktop.secrets` service starts by D-Bus activation inside a `dbus-run-session` session.
- The `default` collection (alias `login`) is not created in a container: the daemon asks for a password through `org.gnome.keyring.SystemPrompter`, and the prompt needs a display. The `session` collection is available without a prompt: the `SecretServiceBackendTests` tests use it. On a real desktop `default` exists.
- In `Tmds.DBus` 0.95.1 a variant (`v`) is passed as `object`, the dictionaries `a{ss}` and `a{sv}` as `IDictionary<string, string>` and `IDictionary<string, object>`. Connecting: `Connection.Session` and `ConnectAsync()`.
- The Secret Service tests are skipped without a D-Bus session (`Assert.Skip`); CI does not have them. Inside `dbus-run-session` with gnome-keyring all tests pass: 15 of 16, DPAPI skipped (needs Windows).
- The macOS backend and DPAPI on Windows are not checked: no machines.

## Enter in the chat field (task 050)
- A `KeyDown` handler declared in XAML on a `TextBox` with `AcceptsReturn` does not manage to intercept Enter: the field inserts a line break earlier. So the handler is added from the constructor code as a tunneling one (`AddHandler(KeyDownEvent, …, RoutingStrategies.Tunnel)`) and sets `Handled` on Enter without Shift.
- In headless tests the key is raised through `RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, … })`; a line break from the keyboard is not modeled in headless, `Handled` is checked.
- **Switching style and theme without a restart (task 061, checked headless):** the styles are the dictionaries `Themes/Quiet|Gallery|Notebook.axaml`, connected through `MergedDictionaries` in `App.axaml` (`ResourceInclude`); `AvaloniaAppearanceApplier` swaps an element of this list and sets `Application.RequestedThemeVariant`. The theme dictionary is `ResourceDictionary.ThemeDictionaries[ThemeVariant.Light|Dark]`, in XAML the keys `x:Key="Light"`/`"Dark"`. Enums (`HorizontalAlignment`, `FontStyle`) cannot be declared as resources in XAML, so they are stored as strings (`x:String`); the screens convert them themselves.
- **Base Avalonia styles (task 063):** `Themes/Controls.axaml` is connected in `App.axaml` through `StyleInclude` after `FluentTheme`; the classes of buttons, fields, text, icons and statuses take colors and sizes through `DynamicResource Ig*`. Keyboard focus is `:focus-visible` (checked in headless: the style loads, the outline is set by `BorderBrush`); for a checkbox and a toggle switch the background and color change through the `/template/` parts `PART_Border` and `OnContentBorder`.

## Keys and menus (task 073)
- The "primary key" modifier is `PlatformKeys.Primary` (`Meta` on macOS, `Control` otherwise). `KeyGesture` is compared through `Equals`; in tests `Assert.Same` on the binding's command works, because page commands are singletons.
- Window shortcuts are `Window.KeyBindings` (in `MainWindow.ConfigureShortcuts`), menu item captions are `MenuItem.InputGesture` (the card's context menu). Card shortcuts are a `KeyBinding` on the card's `Border` with `Focusable="True"`: they work only when the focus is inside the card; the input field is outside the card, so the letter `R` is typed there.
- `NativeMenu.SetMenu(window, …)` builds the app menu on macOS. **Not checked:** on macOS the menu and `KeyBindings` may fire twice for one shortcut; on Windows and Linux `NativeMenu` without `NativeMenuBar` is not shown (unknown whether this holds in the current Avalonia version).
- Focus on the search field: `SessionsListView.FocusSearch()` → `TextBox.Focus()`; in headless `IsFocused` is checked.
