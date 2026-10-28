---
id: 065
status: planned
assignee:
claimed:
created: 2026-10-28
---

# Экран настроек: ключ, стиль, тема

## Описание
Новый экран по ADR 0014 и макету «Настройки»: раздел OpenRouter (ключ подключён, последние 4 символа, «Change…» открывает экран ключа), раздел Appearance (выбор стиля тремя карточками, тема — переключатель System / Light / Dark), раздел About (открыть папку логов — только на десктопе). Вход — значок настроек в списке сессий; в Avalonia ещё `Ctrl+,` (на macOS `Cmd+,`).

Справочник: `docs/design/implementation.md` (ключи ресурсов, значения, шрифты, значки — дословно оттуда); токены: `docs/design/tokens.json`; макеты: `docs/design/prototype.html` (открыть: `python3 -m http.server` в `docs/design/`, затем http://localhost:8000/prototype.html; опубликованная версия — https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решение: ADR 0014.

Зависит от: 059, 061, 062, 063, 064.
Среда: Linux или Windows (Avalonia — headless-тесты на Linux; MAUI — сборка Android на Linux с Android SDK, Windows-цель собирает CI; запуск под Windows — ручная проверка 016, Avalonia на рабочем столе — 046).

## Критерии готовности
- Маршрут `settings` (`Routes.Settings`) в обеих оболочках; `SettingsPageModel` в Presentation.
- Смена стиля или темы сохраняется в `appearance.json` и сразу меняет вид приложения.
- Ключ показывается только последними 4 символами (`docs/conventions.md`); «Change…» ведёт на `Routes.ApiKey`.
- Все интерактивные элементы имеют доступные имена; тест доступности Avalonia покрывает новый экран.
- Тест-кейсы из «Плана» проходят.

## Анализ
- Ключ читается через `ISecretStore` (`SecretNames.OpenRouterApiKey`), как в `ApiKeyPageModel`.
- Открыть папку логов: путь `ImageGenLogging.LogDirectoryFor(paths.DataDirectory)`; открытие — новый интерфейс `IFolderLauncher` в Presentation с реализациями: Avalonia — `TopLevel.Launcher.LaunchDirectoryInfoAsync`; MAUI — `Launcher.Default.OpenAsync` на Windows и Mac, на телефонах кнопка скрыта (`IFolderLauncher.IsSupported`).
- Навигация: Avalonia — `PageRoutes` в `AppServices`; MAUI — `AddTransientWithShellRoute<SettingsPage, SettingsPageModel>(Routes.Settings)`.
- Строки интерфейса — в `AppResources.resx` и классе доступа `AppResources.cs` (как остальные; `docs/knowledge/maui.md` про ручной класс доступа).

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, ключей и строк переноси дословно. Если шаг не получается так, как написано (компилятор не принимает разметку, API другое), не придумывай обход: опиши в «Комментариях», что пробовал и что получилось, поставь `blocked` и остановись.

1. Presentation:
   - `Routes.Settings = "settings"`; тест в `RoutesTests`.
   - Строки (`AppResources.resx` + свойства в `AppResources.cs`): `SettingsTitle` = "Settings", `SettingsOpenRouter` = "OpenRouter", `SettingsApiKey` = "API key", `SettingsKeyConnectedFormat` = "Connected · ends with {0}", `SettingsKeyMissing` = "Not connected", `SettingsChangeKey` = "Change…", `SettingsAppearance` = "Appearance", `SettingsStyle` = "Style", `SettingsTheme` = "Theme", `StyleQuiet` = "Quiet", `StyleQuietHint` = "Native look, dense", `StyleGallery` = "Gallery", `StyleGalleryHint` = "Images first", `StyleNotebook` = "Notebook", `StyleNotebookHint` = "All run data visible", `ThemeSystem` = "System", `ThemeLight` = "Light", `ThemeDark` = "Dark", `SettingsAbout` = "About", `SettingsLogs` = "Logs", `SettingsOpenLogs` = "Open folder". Тест `Resources` (существующий) должен видеть новые ключи.
   - `Services/IFolderLauncher.cs`: `bool IsSupported { get; }`, `Task OpenAsync(string path, CancellationToken cancellationToken)`.
   - `PageModels/StyleOptionViewModel.cs`: `Style`, `Name`, `Hint`, `IsSelected` (observable), `SelectCommand`.
   - `PageModels/SettingsPageModel.cs`: конструктор `(AppearanceService appearance, ISecretStore secrets, INavigationService navigation, IFolderLauncher folders, IAppPaths paths)`; свойства `KeyText`, `Styles` (3 варианта), `SelectedTheme` (`AppearanceTheme`, двусторонняя привязка; при смене — `SaveAsync`), `CanOpenLogs`; команды `LoadCommand` (читает ключ и текущие настройки), `ChangeKeyCommand` (`GoToAsync(Routes.ApiKey)`), `OpenLogsCommand`.
   - Тесты `PageModels/SettingsPageModelTests.cs`: `Load_KeySaved_ShowsLastFourCharacters` (ключ `sk-or-v1-abcd1234` → "Connected · ends with 1234"), `Load_NoKey_ShowsNotConnected`, `SelectStyle_Gallery_SavesAndMarksSelected`, `SelectedTheme_Dark_Saves`, `ChangeKey_NavigatesToApiKey`, `OpenLogs_CallsLauncherWithLogFolder`, `CanOpenLogs_FollowsLauncher`.
2. Avalonia: `Views/SettingsView.axaml` (+ `.cs`) по макету: заголовки разделов `TextBlock.title`, строки с разделителем `IgLineBrush`, карточки стилей — `ListBox` или три `RadioButton` в виде карточек (`Border` радиус `IgRadiusMd`, выбранная — рамка `IgAccentBrush` 2), тема — три `RadioButton` в группе с видом сегментов. Маршрут в `AppServices` (`PageRoutes.Add(Routes.Settings, ...)`, `AddTransient<SettingsPageModel>`), `IFolderLauncher` → `AvaloniaFolderLauncher`. Значок настроек (`TextBlock.icon` с `IconGlyphs.Settings`) в `SessionsView` рядом с «New session», `AutomationProperties.Name` = `SettingsTitle`. `MainWindow`: `KeyBinding` `Ctrl+OemComma` (на macOS `Meta+OemComma` — через `KeyGesture` `Cmd`? проверить при реализации) → переход на `Routes.Settings`.
3. MAUI: `Pages/SettingsPage.xaml` (+ `.cs`) — та же разметка на `StyleClass` из 064; `ToolbarItem` с `FontImageSource` (`LucideIcons`, `IconGlyphs.Settings`) на `SessionsPage`; `MauiFolderLauncher` (`IsSupported` = `DeviceInfo.Idiom == DeviceIdiom.Desktop`).
4. Тесты Avalonia: `Flows/SettingsFlowTests.cs` — `OpenSettings_FromSessions_ShowsSettingsView`; `SelectGallery_ChangesAccentResource`; добавить экран настроек в `Views/ViewsAccessibilityTests` (`AssertNamed(shell, "настройки")`).
5. Документация: `docs/specs/chat-sessions.md` S6.3 — экран называется «Settings», вход из списка сессий и `Ctrl/Cmd+,`; `docs/architecture.md` — новый маршрут и `IFolderLauncher`, если там перечислены маршруты и сервисы.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android -p:AndroidSdkDirectory=$ANDROID_HOME
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Presentation/{Routes.cs,Services/IFolderLauncher.cs,PageModels/SettingsPageModel.cs,PageModels/StyleOptionViewModel.cs,Resources/Strings/*}`, `ImageGen/ImageGen.Avalonia/{Views/SettingsView.*,Views/SessionsView.axaml,Views/MainWindow.*,Composition/AppServices.cs,Services/AvaloniaFolderLauncher.cs}`, `ImageGen/ImageGen.Maui/{Pages/SettingsPage.*,Pages/SessionsPage.xaml,MauiProgram.cs,Services/MauiFolderLauncher.cs}`, тесты, `docs/specs/chat-sessions.md`, `docs/architecture.md`

## Отчёт

## Комментарии
