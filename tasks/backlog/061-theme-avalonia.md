---
id: 061
status: planned
assignee:
claimed:
created: 2026-10-28
---

# Тема Avalonia: словари трёх стилей и переключение на лету

## Описание
Перевести Avalonia-оболочку на токены дизайн-системы: три словаря ресурсов (по стилю), в каждом — светлая и тёмная тема; смена стиля и темы из `AppearanceService` применяется сразу. Экраны пока не меняются (их перевод — задачи 063, 066–072), но ресурсы `Ig*` уже доступны.

Справочник: `docs/design/implementation.md` (ключи ресурсов, значения, шрифты, значки — дословно оттуда); токены: `docs/design/tokens.json`; макеты: `docs/design/prototype.html` (открыть: `python3 -m http.server` в `docs/design/`, затем http://localhost:8000/prototype.html; опубликованная версия — https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решение: ADR 0014.

Зависит от: 059, 060.
Среда: Linux или Windows (headless-тесты на Linux).

## Критерии готовности
- Все ключи из `implementation.md`, разделы 2.1–2.4, есть в словарях Avalonia; значения совпадают с `tokens.json` (тест).
- При старте приложение применяет сохранённые настройки; `AppearanceService.SaveAsync` меняет стиль и тему без перезапуска.
- Тема `System` следует системной; `Light` и `Dark` её перекрывают.
- Тесты из «Плана» проходят; `dotnet format` без изменений.

## Анализ
- Сейчас: `ImageGen.Avalonia/App.axaml` — `FluentTheme`, `RequestedThemeVariant="Default"`, один ресурс `ErrorBrush` в `ThemeDictionaries` (используется в `ChatView.axaml`, класс `TextBlock.error`). `ErrorBrush` оставить до задачи 068, но значения взять из `IgErrColor` (ссылка `{DynamicResource IgErrBrush}` в `ChatView`).
- Avalonia поддерживает `ResourceDictionary.ThemeDictionaries` с ключами `Light` и `Dark` внутри подключаемого словаря; `Application.RequestedThemeVariant` = `ThemeVariant.Default` / `Light` / `Dark` выбирает ветку.
- Подмена стиля: `Application.Current.Resources.MergedDictionaries` — убрать прежний словарь стиля, добавить новый (`ResourceInclude` с `Source="avares://ImageGen.Avalonia/Themes/Quiet.axaml"` или экземпляр, загруженный `AvaloniaXamlLoader.Load(new Uri(...))`). Все ссылки на `Ig*` — `DynamicResource`.
- Композиция: `ImageGen.Avalonia/Composition/AppServices.cs` (сюда регистрация `AppearanceService`), старт — `App.axaml.cs`.
- Тесты: `ImageGen.Avalonia.Tests` (`[AvaloniaFact]`, `TestAppBuilder.cs`), образец — `Views/MainWindowTests.cs`.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, ключей и строк переноси дословно. Если шаг не получается так, как написано (компилятор не принимает разметку, API другое), не придумывай обход: опиши в «Комментариях», что пробовал и что получилось, поставь `blocked` и остановись.

1. Словари: `ImageGen/ImageGen.Avalonia/Themes/Common.axaml` (раздел 2.3, кроме `IgFontSizeBody`) и `Themes/Quiet.axaml`, `Themes/Gallery.axaml`, `Themes/Notebook.axaml`. В каждом словаре стиля:
   - `ResourceDictionary.ThemeDictionaries` с `x:Key="Light"` и `x:Key="Dark"`: 16 `Color` и 16 `SolidColorBrush` (раздел 2.1; кисть ссылается на цвет: `Color="{StaticResource IgBgColor}"`), плюс кисти-варианты из 2.4, которые зависят от цвета (`IgUserMessageBackgroundBrush`, `IgPromptBackgroundBrush`, `IgPromptForegroundBrush`, `IgCardBackgroundBrush`);
   - вне `ThemeDictionaries`: раздел 2.2, `IgFontSizeBody`, остальные ключи 2.4.
   - Шрифты: Quiet — `<FontFamily x:Key="IgFontText">$Default</FontFamily>`; Gallery — `avares://ImageGen.Avalonia/Assets/Fonts#Onest`; Notebook — `#IBM Plex Sans` и `#IBM Plex Mono`.
2. `App.axaml`: в `Application.Resources` подключить `Themes/Common.axaml` и `Themes/Quiet.axaml` через `MergedDictionaries` (`ResourceInclude`); `ErrorBrush` в Light/Dark заменить на `IgErrColor`-значения из Quiet (временно, до 068).
3. `ImageGen/ImageGen.Avalonia/Services/AvaloniaAppearanceApplier.cs` — `sealed class`, конструктор `(AppearanceService service, Application application)`:
   - `Apply(AppearanceSettings settings)`: найти в `application.Resources.MergedDictionaries` словарь текущего стиля (запомнить ссылку в поле при первом применении), заменить словарем нужного стиля; `application.RequestedThemeVariant` = `ThemeVariant.Default` для System, `Light`, `Dark`;
   - `Start()`: `Apply(service.Current)` и подписка на `service.Changed` (вызов через `Dispatcher.UIThread.Post`, если не UI-поток).
   - Чистая функция для тестов: `static string ThemeUri(AppearanceStyle style)` → `avares://ImageGen.Avalonia/Themes/<Style>.axaml`.
4. `AppServices.Create`: `services.AddSingleton<AppearanceService>()`. `App.OnFrameworkInitializationCompleted`: после создания контейнера `new AvaloniaAppearanceApplier(...).Start()` до создания `MainWindow`.
5. Тесты (`ImageGen.Avalonia.Tests/Appearance/`):
   - `ThemeTokensTests.Dictionaries_MatchTokensJson` — найти корень репозитория (подниматься от `AppContext.BaseDirectory`, пока не найдётся `docs/design/tokens.json`), прочитать JSON; для каждого стиля загрузить словарь `AvaloniaXamlLoader.Load(new Uri(ThemeUri(style)))`, для `Light` и `Dark` получить `ThemeDictionaries[ThemeVariant.Light]` и сравнить 16 цветов (`Color.ToString()` в верхнем регистре без альфы против `hex`); сравнить радиусы и `IgCardGap` с `radius.*` и `size.cardGap`.
   - `ThemeTokensTests.Dictionaries_HaveSameKeys` — множества ключей трёх словарей стиля (включая ключи внутри Light и Dark) совпадают.
   - `AvaloniaAppearanceApplierTests.Apply_Gallery_ChangesAccentColor` — после `Apply(Gallery, Light)` `Application.Current.FindResource(ThemeVariant.Light, "IgAccentColor")` = `#0F766E`.
   - `Apply_Dark_SetsRequestedThemeVariant` и `Apply_System_SetsDefaultVariant`.
   - `SaveAsync_Notebook_AppliesWithoutRestart` — через `AppearanceService` с временной папкой: после `SaveAsync` значение `IgFontMeta` — семейство с `IBM Plex Mono`.
6. `docs/knowledge/avalonia.md` — факт о подмене словаря и `ThemeDictionaries` (что подтвердилось на деле).

### Проверка
```
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Avalonia/Themes/*.axaml`, `ImageGen/ImageGen.Avalonia/App.axaml`, `App.axaml.cs`, `Composition/AppServices.cs`, `Services/AvaloniaAppearanceApplier.cs`, `ImageGen/ImageGen.Avalonia.Tests/Appearance/*`, `docs/knowledge/avalonia.md`

## Отчёт

## Комментарии
