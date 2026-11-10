# Reference: .NET MAUI for this project

Written from the model's knowledge, not by reconciling with live documentation in this session. Items marked "check" must be checked against the documentation before use. Versions in the sample: .NET 10, `$(MauiVersion)` from the SDK, `CommunityToolkit.Maui 12.3.0`, `CommunityToolkit.Mvvm 8.4.2`.

Sources to check against: https://learn.microsoft.com/dotnet/maui/ , https://learn.microsoft.com/dotnet/communitytoolkit/maui/ , https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/

## Platforms
- Supported: Android, iOS, Mac Catalyst (macOS), Windows (WinUI 3). MAUI has no official Linux; the sample's `ImageGen.csproj` has no such target either. Linux is covered another way (see `docs/status.md`).
- Building iOS and Mac Catalyst needs macOS with Xcode; in the sample's `TargetFrameworks` they are skipped only on Linux, so on Windows without a Mac host their build may fail (check; to work on Windows, build `-f net10.0-windows10.0.19041.0` or `-f net10.0-android`).
- Workloads: `dotnet workload install maui` (check that the command is current for .NET 10); `dotnet workload list`.

### CommunityToolkit MediaElement 10.0.0 (task 205)
- `CommunityToolkit.Maui.MediaElement` is a separate package. In `MauiProgram`, `using CommunityToolkit.Maui;` and `.UseMauiCommunityToolkitMediaElement(isAndroidForegroundServiceEnabled: false)` are enough; the parameter matches the README of package 10.0.0 from NuGet. For an app without background playback keep `false`.
- For the separate player page (219), closing calls `MediaElement.Stop()` and `Handler?.DisconnectHandler()` before removing the modal page. `Stop()` resets the position and stops the sound. The local `ReadMe.txt` of package 10.0.0 describes initialization but not release; the semantics of `Stop()` were reconciled with the [official MediaElement documentation](https://learn.microsoft.com/dotnet/communitytoolkit/maui/views/mediaelement), and the `DisconnectHandler()` call was added per the task's plan. A manual Back/close check stays an item of 140.
- The element is `CommunityToolkit.Maui.Views.MediaElement`; a local file is set with `MediaSource.FromFile(path)`. By default the source does not start playing by itself; the app explicitly sets `ShouldAutoPlay="False"` and `ShouldShowPlaybackControls="True"`. `Stop()` stops playback when leaving the page.
- Package 10.0.0 is compatible with .NET 10, but Android API 26+; the Android project's `SupportedOSPlatformVersion` must be at least 26. On Android the Activity requires `ResizeableActivity=true` and `LaunchMode=SingleTask` (see [MediaElement platform setup](https://learn.microsoft.com/dotnet/communitytoolkit/maui/views/mediaelement#android)). Formats depend on the platform's native decoders.
- Reconciled 2026-11-06 with the NuGet 10.0.0 README and the [MediaElement documentation](https://learn.microsoft.com/dotnet/communitytoolkit/maui/views/mediaelement); playback is checked by hand in tasks 016/140.

## Single-project structure
- One `.csproj` for all platforms, `SingleProject=true`. Platform code in `Platforms/<Android|iOS|MacCatalyst|Windows>/` is automatically built only for its platform.
- Conditional compilation: `#if ANDROID`, `#if IOS`, `#if MACCATALYST`, `#if WINDOWS`; for a check at run time — `OperatingSystem.IsAndroid()`, `DeviceInfo.Platform`.
- Resources: `Resources/AppIcon`, `Splash`, `Images`, `Fonts`, `Raw`, `Styles`; `MauiImage` is scaled by Resizetizer (SVG and PNG), names only in lowercase, digits and underscores.
- All platform entry points call `MauiProgram.CreateMauiApp()`.

### Android window and system bars (task 137)
- In .NET MAUI 10 `IWindowHandler.PlatformView` on Android is an `Android.App.Activity`; `Window.Handler` can be checked as `IWindowHandler`, then take `Activity.Window` of exactly the viewer's window. Do not search an arbitrary `Application.Current.Windows` and do not rely on the static `Platform.CurrentActivity`. Source: [IWindowHandler.cs](https://github.com/dotnet/maui/blob/43db9d77f2ff59999dea36ab8befb8541e919013/src/Core/src/Handlers/Window/IWindowHandler.cs); the local Android build also checks the type.
- `WindowInsetsControllerCompat` from the existing `Xamarin.AndroidX.Core` works with the status/navigation bars; the visibility and `SystemBarsBehavior` must be saved separately for each type, then restored. Before the first `RootWindowInsets` the original visibility must not be invented: the fullscreen lease stays pending and cancellable.

## App and lifecycle
- `App.CreateWindow(IActivationState?)` returns a `Window` with the start page or a `Shell` (`MainPage` of `Application` is not used in new projects).
- Page events: `Appearing`, `Disappearing`, `NavigatedTo`, `NavigatedFrom`; window events: `Created`, `Activated`, `Deactivated`, `Stopped`, `Resumed`, `Destroying` (to save state and stop subscriptions).
- On Android and iOS the app may be stopped by the system in the background: long generations cannot be kept only in UI memory; write the result to disk right away, keep the job status in the DB (requirement F7.2). Platform background services (Android foreground service, iOS background tasks) are separate work, not part of MAUI out of the box (check).

## XAML and bindings
- `x:DataType` is mandatory for compiled bindings; in a `DataTemplate` and with `Source={RelativeSource AncestorType=...}` set the type explicitly (see the examples in `Pages/`).
- Resources: `ResourceDictionary`, `StaticResource` (resolved at load), `DynamicResource` (tracks changes); later dictionaries override earlier ones.
- `AppThemeBinding Light=..., Dark=...` for the theme; switching: `Application.Current.UserAppTheme = AppTheme.Dark/Light/Unspecified`.
- `OnPlatform` and `OnIdiom` in XAML for differences of platforms and form factors (`Phone`, `Tablet`, `Desktop`, `Watch`, `TV`).
- `IValueConverter` converters; CommunityToolkit.Maui has ready ones (`InvertedBoolConverter` and others).
- Behaviors: `EventToCommandBehavior`, `TextValidationBehavior` etc. from CommunityToolkit.Maui; enabled by `UseMauiCommunityToolkit()` in `MauiProgram`.
- Default styles by `TargetType`, named ones by `x:Key`; `Style TargetType="Page" ApplyToDerivedTypes="True"` for a shared background.
- Do not nest a `CollectionView` in a `ScrollView` without a reason (virtualization is lost); the sample does so for short lists.

- **The Android keyboard in an `Editor` (task 050, built, not checked on a device):** the "Send" action is `EditorHandler.Mapper.AppendToMapping`, `PlatformView.ImeOptions = ImeAction.Send`, `EditorAction` in `Platforms/Android/ChatEditorImeHandling.cs`. For the keyboard to show "Send", the field is switched to single-line input (`SetRawInputType` without `TextFlagMultiLine`): in a single-line field Android scrolls long text horizontally instead of wrapping it. Task 016 checks this (item 11). `KeyPress` of the native field starts with `Handled = true`: a key for which the handler did not reset the flag does not reach the field. Gboard sends Backspace as `KEYCODE_DEL`, so the handler explicitly lets through all keys except Enter (the rule is `Presentation/Utilities/ChatInputKeys`, task 134; not checked on a device).
- **Scrolling the feed to the end on Android (task 159, not checked on a device):** `CollectionView.ScrollTo(index, ScrollToPosition.End)` for a row above the window shows its top, not its bottom (the owner's observation, Galaxy S25, Android 17). The chat feed is scrolled to the bottom of the content through `RecyclerView` (`Platforms/Android/TranscriptScroller`), and the user's gesture does not start scrolling by itself.
- **`SwipeView` and taps on Android (task 162, not checked on a device):** a `Button` inside a `SwipeView` lost the tap — the swipe actions opened instead. A session row is opened by a `TapGestureRecognizer` on the whole row, the `SwipeView` has a `Threshold`.
- **APK signing:** the release APK is signed with the build machine's debug key, and the certificate changes from release to release (v0.1.4 and v0.1.5 have different SHA-256, checked with `apksigner verify --print-certs` on 2026-10-31). Android does not install an APK with another signature over the installed one: an update requires uninstalling the app with its data. Task 163.
- **Keys in an `Editor` on Windows (checked 2026-10-28, task 056):** `Editor` has no key events. Interception is the `EditorHandler.Mapper.AppendToMapping` mapping and `PreviewKeyDown` of the native WinUI `TextBox`: the ordinary `KeyDown` comes after the line break is inserted. Shift — `InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)`. This is how the `ChatEditor` chat field is made (Enter — send, Shift+Enter — a new line).
- **Windows API key masking on paste (checked in UI E2E run [42806400823](https://github.com/r-eliseev/studious-goggles/actions/runs/42806400823), task 251):** the temporary test pasted an invalid key through the system clipboard and Ctrl+V. The key remained masked in every sampled video frame; pasting did not reproduce the brief plaintext display previously seen during rapid character-by-character typing.
- **The Android keyboard and the safe area (.NET 10, task 095):** a `ContentPage` has `SafeAreaEdges=None` by default, a layout `Container`; the keyboard is not part of the layout's safe area. The seven root pages have `SafeAreaEdges="All"` as an attribute, so that on-screen input is taken into account without an extra implicit `ContentPage` style that would override the shared `Page` style. Per the [safe area documentation](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/safe-area?view=net-maui-10.0), on Android `WindowSoftInputModeAdjust(Resize)` is applied together with `All` when needed; [the soft keyboard guide](https://learn.microsoft.com/en-us/dotnet/maui/android/platform-specifics/soft-keyboard-input-mode). The Android target SDK of the current build is 36. Without the mode the window panned and the input field stayed under the keyboard (Galaxy S25, Android 17, 2026-10-31); `App` enables `UseWindowSoftInputModeAdjust(Resize)` (task 160, not checked on a device after the fix).

## Layout and lists
- `Grid` (the main one), `VerticalStackLayout`/`HorizontalStackLayout`, `FlexLayout` (wrapping chips), `AbsoluteLayout`, `ScrollView`.
- For short dynamic lists `BindableLayout`; for long ones `CollectionView` with `LinearItemsLayout` or `GridItemsLayout` (the result grid: `GridItemsLayout` with `Span`, two columns on a narrow screen, more on a wide one; adjust through `OnIdiom` or the window size).
- **Wrapping chips (task 098):** `FlexLayout` has no `Spacing`, the gap is set by the children's margins and a negative bottom margin of the container. The width of a `Label` is limited by `MaximumWidthRequest` from the container's `SizeChanged`. Not checked on a device yet.
- **Scrolling the chat feed (task 097):** the `CollectionView` named `Transcript` is scrolled through `ScrollTo(index, ScrollToPosition.End, animate: false)` inside `Dispatcher.Dispatch` after `Items` change. The decision whether to follow the end is kept by `TranscriptFollower`: row changes always resume following, updates of card content and size only while following is on. The direction of the user's scroll comes from `Scrolled.VerticalDelta` after filtering out programmatic scrolling, a pending scroll and events without vertical movement; the last visible index while moving down is a signal to resume following, after which the feed is positioned at the end. Root message and run rows report size changes. The Android build is checked, not checked on a device.
- `SelectionMode`, `SelectionChangedCommand`; reset the selection after handling, otherwise a repeated tap does not work (the sample does not do this — a known quirk).
- `RefreshView` is the standard MAUI pull-to-refresh; the sample uses Syncfusion `SfPullToRefresh`.

## Shell and navigation
- `Shell` with `ShellContent`/`TabBar`/`FlyoutItem`; `Route` for the top level, `Routing.RegisterRoute` or `AddTransientWithShellRoute` for child pages.
- `Shell.Current.GoToAsync("route?id=1")` for a parameter string, `GoToAsync("route", new ShellNavigationQueryParameters { {"key", obj} })` for objects (a one-time hand-over), `".."` — back, `"//main"` — an absolute route.
- The receiver: `IQueryAttributable.ApplyQueryAttributes(IDictionary<string, object>)` or `[QueryProperty]`.
- The specification needs bottom navigation on a narrow screen and a side one on a wide screen: a `TabBar` for the phone, a `Flyout` for the desktop; the choice through `OnIdiom` or separate shells (check which is better).

## MVVM (CommunityToolkit.Mvvm)
- `ObservableObject`, `[ObservableProperty]`, `[RelayCommand]` (asynchronous commands `AsyncRelayCommand`, cancellation `IncludeCancelCommand = true`), `[NotifyCanExecuteChangedFor]`, `[NotifyPropertyChangedFor]`, `partial void OnXChanged(T value)`.
- An asynchronous command blocks a repeated call while it runs unless `AllowConcurrentExecutions` is on; this is convenient for the "Run" button.
- `WeakReferenceMessenger` for messages between PageModels without direct references (for example, "result ready" → Gallery).

## Platform APIs (MAUI Essentials)
- `Preferences` — non-secret settings; not encrypted. Not for keys.
- `SecureStorage` — secrets (Keychain/Keystore/DPAPI); asynchronous `GetAsync/SetAsync/Remove`. Quirks: on Android the data is bound to the app, with auto backup and reinstall the encryption key may be lost (for apps with `allowBackup="true"`, as in the sample's manifest, exclusion rules are needed; check the documentation); on iOS and Mac the right Keychain entitlements are needed for the simulator and distribution; in unpackaged Windows it works through DPAPI. Wrap every call in `try/catch`: on some devices it throws when the store is unavailable.
- `FileSystem.AppDataDirectory`, `CacheDirectory`, `OpenAppPackageFileAsync` (for `Resources/Raw`).
- `FilePicker`, `MediaPicker` (reference pictures: pick from the gallery, take a photo, paste from the clipboard), `Share`, `Clipboard`, `Launcher`, `Connectivity`, `DeviceInfo`, `DeviceDisplay`, `MainThread`, `SemanticScreenReader`, `Vibration`/`Haptic`.
- Permissions (`Permissions.RequestAsync<...>`) and their manifest entries are needed for the camera and for reading and writing photos.

- **A picture from a file on Windows (checked 2026-10-28, task 054):** `ImageSource.FromFile` with an absolute path that contains `/` (`C:\…\Data\images/…/x.jpg`) silently does not show the picture; the same path with `\` shows it (checked by a launch at the owner's). WinRT `StorageFile.GetFileFromPathAsync` behaves the same way: with `/` — 0x80070002 "file not found", with `\` — it opens (checked in PowerShell). Whether MAUI uses exactly this call was not checked in the sources. Conclusion: build the file path for the head with the OS separators (`FileImageStore.GetFullPath`), even if the DB stores it with `/`.

## Saving a picture to the gallery (from the notes: Android)
- Android 10 (API 29) and higher: insert through `MediaStore.Images.Media` with `ContentValues` (`DisplayName`, `MimeType`, `RelativePath = "Pictures/ImageGen"`, `IsPending`) and write to `ContentResolver.OpenOutputStream` — no write permission is needed. Android 9 and lower: the `WRITE_EXTERNAL_STORAGE` permission and writing to a public folder with a `MediaScanner` request are needed (check). Android 13 and higher need `READ_MEDIA_IMAGES` to read other apps' photos; saving our own does not require it.
- The code is in `Platforms/Android` behind a core interface (for example `IGallerySaver`), other platforms have their own implementations: iOS — `PHPhotoLibrary` (+ the `NSPhotoLibraryAddUsageDescription` key in `Info.plist`), Mac Catalyst — `NSSavePanel`/Photos, Windows — `FileSavePicker` or copying to `Pictures`.
- A copy is kept in our own result repository next to it; the file in the gallery is an export.

## Backup and data transfer on Android (task 115)
- The app does not take part in automatic backup: `Platforms/Android/AndroidManifest.xml` has `android:allowBackup="false"`, plus `android:fullBackupContent="@xml/backup_rules"` and `android:dataExtractionRules="@xml/data_extraction_rules"`.
- Explicit deny-all rules are defense in depth: the Android documentation does not promise that an OEM transfer tool respects the manifest flag alone. `backup_rules.xml` (Android 11 and lower) and `data_extraction_rules.xml` (Android 12+, the `cloud-backup` and `device-transfer` sections) exclude all nine domains of the schema with `path="."`: `root`, `file`, `database`, `sharedpref`, `external`, `device_root`, `device_file`, `device_database`, `device_sharedpref`. Not a single `include`.
- Checked: `ImageGen.Presentation.Tests/Design/AndroidBackupPolicyTests.cs` checks the manifest attributes, the absence of `include` and the exact set of domains in both sections; the Android build and parsing the final manifest from the APK (`aapt2 dump xmltree`) confirm that the rules got into the package.
- **Not checked on a device:** the behavior of data transfer (D2D) on a real phone. Source tests confirm the configuration, but not the behavior of the OEM tool.
- Our own export of user data (F7.6) is not implemented yet: the app cannot give out an encrypted copy, so there is nothing to include in the rules.

## Theming, fonts, icons
- Icons through a font and `FontImageSource` (as in the sample) or SVG through `MauiImage`.
- Fonts in `Resources/Fonts`, registered in `ConfigureFonts` with an alias.
- Colors of the platform parts (Android `colors.xml`) duplicate the palette and are not synchronized automatically.

## Localization
- `.resx` resources + `CultureInfo`; in XAML through a markup extension or a wrapper over `ResourceManager`. The UI is English only (F10.1, the owner's decision of 2026-10-31). The strings in the sample are hard-coded in English.

## Accessibility
- `SemanticProperties.Description`, `Hint`, `HeadingLevel`; `AutomationProperties.IsInAccessibleTree`; a minimum touch target of 44; `SemanticScreenReader.Announce` for dynamic messages; keyboard navigation on Windows (in the sample the handlers `SingleSelectionFollowsFocus=false`, `IsTabStop`).

## Performance
- Enable compiled bindings and XAML SourceGen (enabled in the sample).
- Large images: reduce previews (thumbnails in the grid), do not load originals; `Image.Source` from a file/stream, `ImageSource.FromFile`/`FromStream`; release large streams.
- Do not create thousands of `View`s in a `BindableLayout`: `CollectionView` for long feeds.

## Debugging and running
- Windows: `dotnet build -f net10.0-windows10.0.19041.0`, run from Visual Studio or `dotnet run -f net10.0-windows10.0.19041.0`; Android: an emulator and `dotnet build -t:Run -f net10.0-android` (check); iOS and Mac Catalyst only with a Mac.
- XAML Hot Reload works in Visual Studio/VS Code, in Debug.
- Logging: `builder.Logging.AddDebug()` (as in the sample, only in `DEBUG`).

## Known quirks from the sample
- The CommunityToolkit.Maui Toast does not work on Windows (the sample skips it) — use a Snackbar or our own notification.
- `Shell.Current.DisplayAlertAsync` is a .NET 10 API; earlier versions had `DisplayAlert`.
- Windows: `CollectionView` selects an item on focus by default; for keyboard accessibility this is disabled in `MauiProgram`.

## Checked by a trial build (2026-10-27, SDK 10.0.402, MAUI 10.0.110)
Checked in a separate folder outside the repository: the `dotnet new maui` template + central package versions + a build for Windows and Android + a launch on Windows.
- The `dotnet new maui` template creates an empty app (`MainPage` with a counter, `dotnet_bot.png`), the `ApplicationId` `com.companyname.<name>` and `$(MauiVersion)` in `PackageReference`. With `ManagePackageVersionsCentrally=true` the `Version` attribute in `PackageReference` is forbidden: the versions of `Microsoft.Maui.Controls` and `Microsoft.Extensions.Logging.Debug` move to `Directory.Packages.props`.
- The template files are formatted with tabs: `dotnet format --verify-no-changes` fails until `dotnet format` without `--verify-no-changes` is run once (with an `.editorconfig` with `indent_style = space`).
- With the `CommunityToolkit.Maui` package the build fails with error `MCT001` if `MauiProgram` has no `.UseMauiCommunityToolkit()` right after `.UseMauiApp<App>()` (and `using CommunityToolkit.Maui;`).
- A long path to the project folder breaks the Windows build: error `MSB3030` about `WindowsAppSdk.AppxDeploymentExtensions.Desktop-EventLog-Instrumentation.dll` in `MsixContent`. The path `C:\git\studious-goggles` works; a clone in a deep folder (for example a temporary one) does not.
- A strongly typed class from `.resx` is not generated without Visual Studio for the Windows target (it is generated for Android). A working way: `.resx` + a hand-written accessor class on `ResourceManager` and an explicit `LogicalName` on the `EmbeddedResource` (the resource name was checked in the built assembly).
- `SecureStorage.Default.SetAsync/GetAsync` works in an unpackaged Windows app (`WindowsPackageType=None`). `FileSystem.AppDataDirectory` there is `%LOCALAPPDATA%\<publisher>\<ApplicationId>\Data` (the template's publisher is `User Name`).
- `dotnet build <slnx>` on Windows builds the MAUI project for all targets, including iOS and Mac Catalyst; it is slow. To check the core, build and test individual projects.

## What MAUI does not give this project
- Linux, Docker and the web: hence Web API + Vue are a separate head (`docs/architecture.md`, ADR 0001).
- An encrypted DB and background services: separate solutions are needed.
- **MAUI themes without ThemeDictionaries (task 062, built on Android):** MAUI has no `ResourceDictionary` with `ThemeDictionaries`, so six dictionaries (`Resources/Themes/<Style><Theme>.xaml` with code-behind) are swapped in `Application.Resources.MergedDictionaries` (`Services/MauiAppearanceApplier.cs`). The app theme is `Application.UserAppTheme`; the system one is `Application.PlatformAppTheme` (not `RequestedTheme`: it shows the theme already chosen explicitly). Values per platform are `OnPlatform` (`WinUI`, `MacCatalyst`, `iOS`, `Android`); alignment and font style are `x:Static` (`LayoutOptions.End`, `FontAttributes.Italic`).
- **Copying text in MAUI (task 055):** a `Label` cannot be selected, so the assistant's answer and the run's prompt have a "Copy" button (`Command="{Binding Copy.CopyCommand}"`), and "Copied" is shown through `CopyTextAction.IsCopied`. Long press on a message is not done. In Avalonia the text is selectable with `SelectableTextBlock`.
- **MAUI styles on the Ig* resources (task 064, built on Android):** `Resources/Styles/Styles.xaml` without the template's keys (`Colors.xaml` removed). `IgControlHeight` is `OnPlatform` in `Themes/Common.xaml`, taken in the style through `StaticResource` (Common is merged first in `App.xaml`): `DynamicResource` on `OnPlatform` is not checked. `Button.CornerRadius` in MAUI is an `int`, so the theme dictionaries have `IgButtonCornerRadius` (`x:Int32`). A separate font size on iOS and Android (17 and 16) is not applied: `IgFontSizeBody` is an ordinary number (the style's desktop value). The classes `.primary`, `.ghost`, … are implicit styles (`Class="..."`); the button states are a `VisualStateManager` in the base `Button` style.
- **The session models screen (task 090):** the page and the chat panel use the shared `Controls/SessionModelsView`. `RadioButton` has no `Command`, so choosing the assistant model goes through `CheckedChanged` in the code-behind of this control; the group is set by `RadioButtonGroup.GroupName` on the list container. The order of the Save/Cancel buttons is set there too through `Grid.SetColumn`: on Mac Catalyst Cancel is on the left. Tabs and shared buttons separate the image and assistant choice; check keyboard navigation and the screen reader by hand.

## Menus and accelerators (task 073)
- The menu bar is `ContentPage.MenuBarItems` (`MenuBarItem` → `MenuFlyoutItem`). An accelerator is a `KeyboardAccelerator` in `MenuFlyoutItem.KeyboardAccelerators`; the modifier through `Modifiers="{OnPlatform WinUI=Ctrl, MacCatalyst=Cmd}"` (builds on Android, the XAML compiler accepts it).
- We do not set letter accelerators without a modifier: they intercept input in the chat field (hence R, E and Ctrl+C have no accelerator).
- The selected card is `ChatPageModel.SelectedCard`: a tap on a card (`TapGestureRecognizer` with `x:Reference Page`) calls `SelectCardCommand`; the "Card" menu items are bound to `SelectedCard.<Command>`.
- **Not checked:** the behavior of the menu bar and accelerators on Windows and Mac Catalyst (Windows and macOS are needed, an item in task 016); on phones `MenuBarItems` are not shown.
- **Windows: `TouchBehavior` and buttons inside a card** (task 088, checked on Windows 2026-11-01): the CommunityToolkit.Maui `TouchBehavior` on the parent `Border` captures the pointer, and a nested `Button` does not get `Click` on a mouse click; UI Automation (`InvokePattern`) and the right click still work. So the chat card's `TouchBehavior` is off on WinUI (`IsEnabled="{OnPlatform WinUI=False, Default=True}"`), and "…" opens the card's `ContextFlyout` from code (`CardActionsFlyout`).
- **`TouchBehavior` (CommunityToolkit.Maui 15.0.1):** `ShouldMakeChildrenInputTransparent` is `true` by default; for a `Border` with layout content the behavior makes the children transparent to input. If there are buttons inside, set `false`. A `Behavior` does not inherit the `BindingContext`: bind it explicitly to the host element's `BindingContext` through `x:Reference` and set `x:DataType` for a compiled command.

## Publishing MAUI for Windows with a RID
- For a multi-target MAUI project `dotnet publish -f net10.0-windows10.0.19041.0 -r <rid>` sets the RID during the restore of all target frameworks; the Android restore may end with `NU1102` for `Mono.win-x64`. `-p:TargetFrameworks=net10.0-windows10.0.19041.0` narrows the app's restore, but a global MSBuild property also gets into project references: in run 10 this led to `NETSDK1005` — Core, Data and Presentation expected the missing `net10.0`.
- `-p:RuntimeIdentifierOverride=<rid>` does not set the SDK RID: in release `v0.1.4` (checked 2026-10-29) the `win-arm64` package contained x64 PE files.
- A working way was checked on 2026-10-30 in GitHub Actions `release.yml`, run 11 (`441c03ac3cbb9cc0525e8d51f1da5093e1add720`): pass `-p:ImageGenWindowsRid=<rid>`, and in `ImageGen.Maui.csproj` set `RuntimeIdentifier` only when the target platform is Windows and the property is set. This way project references do not get the RID and keep their TFMs.
- The `Check PE machine` step checks `ImageGen.Maui.exe`, `coreclr.dll` and `Microsoft.WindowsAppRuntime.Bootstrap.dll` before packaging. The artifacts of run 11 were additionally checked with `file`: all five checked PE files of the x64 package (`ImageGen.Maui.exe`, `coreclr.dll`, `hostfxr.dll`, `Microsoft.WindowsAppRuntime.Bootstrap.dll`, `Microsoft.ui.xaml.dll`) are x86-64; in the arm64 package the same files are AArch64. Running the package on Windows on ARM was not checked.
