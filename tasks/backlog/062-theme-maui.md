---
id: 062
status: planned
assignee:
claimed:
created: 2026-10-28
---

# Тема MAUI: словари трёх стилей и переключение на лету

## Описание
То же, что 061, для MAUI: шесть словарей (стиль × тема), смена из `AppearanceService` без перезапуска. Экраны пока не меняются; шаблонные `Colors.xaml` и `Styles.xaml` остаются до задачи 064.

Справочник: `docs/design/implementation.md` (ключи ресурсов, значения, шрифты, значки — дословно оттуда); токены: `docs/design/tokens.json`; макеты: `docs/design/prototype.html` (открыть: `python3 -m http.server` в `docs/design/`, затем http://localhost:8000/prototype.html; опубликованная версия — https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решение: ADR 0014.

Зависит от: 059, 060.
Среда: Linux или Windows (Avalonia — headless-тесты на Linux; MAUI — сборка Android на Linux с Android SDK, Windows-цель собирает CI; запуск под Windows — ручная проверка 016, Avalonia на рабочем столе — 046).

## Критерии готовности
- Шесть словарей с ключами из `implementation.md`, разделы 2.1–2.4, и общий словарь 2.3; значения совпадают с `tokens.json` (тест на разобранном XAML).
- При старте применяются сохранённые настройки; смена стиля или темы, в том числе системной, применяется сразу.
- Сборка Android проходит; Windows-цель собирает CI; ручная проверка смены на Windows добавлена в 016.

## Анализ
- В MAUI нет `ThemeDictionaries`: нужны отдельные словари `QuietLight`, `QuietDark`, … и подмена в `Application.Current.Resources.MergedDictionaries` при смене стиля, темы или системной темы (`Application.RequestedThemeChanged`). Тема приложения: `Application.Current.UserAppTheme` = `AppTheme.Unspecified` / `Light` / `Dark` (`docs/knowledge/maui.md`).
- Словарь создаётся в коде, только если у XAML есть `x:Class` и code-behind (`partial class QuietLight : ResourceDictionary` с `InitializeComponent()`).
- Значения по платформе (раздел 2.3) — `OnPlatform` с `WinUI`, `MacCatalyst`, `iOS`, `Android`.
- `LayoutOptions` в словаре: `<x:Static x:Key="IgUserMessageAlignment" Member="LayoutOptions.End" />`; `FontAttributes` — `<x:Static Member="FontAttributes.Italic" />`.
- Выбор словаря — чистая функция в Presentation, чтобы её проверить тестом без MAUI.
- Тестового проекта MAUI нет: соответствие токенам проверяется тестом в `ImageGen.Presentation.Tests`, который читает XAML-файлы MAUI как XML (без компиляции MAUI).
- Композиция: `MauiProgram.cs`, старт — `App.xaml.cs`.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, ключей и строк переноси дословно. Если шаг не получается так, как написано (компилятор не принимает разметку, API другое), не придумывай обход: опиши в «Комментариях», что пробовал и что получилось, поставь `blocked` и остановись.

1. Presentation: `ImageGen/ImageGen.Presentation/Appearance/ThemeSelection.cs` — `public static class ThemeSelection`:
   - `bool IsDark(AppearanceTheme theme, bool systemIsDark)` — Dark → true, Light → false, System → `systemIsDark`;
   - `string DictionaryName(AppearanceStyle style, bool isDark)` → `"QuietLight"`, `"GalleryDark"` и т. д.
   Тесты `ImageGen.Presentation.Tests/Appearance/ThemeSelectionTests.cs`: `IsDark_*` (3 случая × 2 значения системы), `DictionaryName_AllCombinations` (6 случаев).
2. MAUI словари в `ImageGen/ImageGen.Maui/Resources/Themes/`: `Common.xaml` (раздел 2.3 через `OnPlatform`, кроме `IgFontSizeBody`) и шесть `<Style><Theme>.xaml` с `x:Class="ImageGen.Maui.Resources.Themes.<Имя>"` и `.xaml.cs`. В каждом: 16 `Color` + 16 `SolidColorBrush` (2.1), раздел 2.2 (шрифты — псевдонимы `OnestRegular`, `PlexSansRegular`, `PlexMonoRegular`; Quiet — пустая строка), `IgFontSizeBody` через `OnPlatform` (десктоп 14 или 15 для Gallery, iOS 17, Android 16), раздел 2.4.
3. `App.xaml`: добавить `Resources/Themes/Common.xaml` в `MergedDictionaries` после шаблонных словарей.
4. `ImageGen/ImageGen.Maui/Services/MauiAppearanceApplier.cs` — `sealed class`, конструктор `(AppearanceService service)`:
   - `Apply(AppearanceSettings settings)`: `UserAppTheme` по теме; `isDark = ThemeSelection.IsDark(settings.Theme, Application.Current.RequestedTheme == AppTheme.Dark)`; создать словарь по `DictionaryName` (switch на 6 типов), заменить прежний (держать ссылку в поле), добавить в конец `MergedDictionaries`;
   - `Start()`: `Apply(service.Current)`; подписки на `service.Changed` и `Application.Current.RequestedThemeChanged` (оба — через `MainThread.BeginInvokeOnMainThread`).
5. `MauiProgram.cs`: `AddSingleton<AppearanceService>()`, `AddSingleton<MauiAppearanceApplier>()`. `App.xaml.cs`: конструктор получает `MauiAppearanceApplier` (через DI в конструкторе `App`, MAUI это поддерживает) и вызывает `Start()` после `InitializeComponent()`.
6. Тест соответствия `ImageGen.Presentation.Tests/Design/MauiThemeTokensTests.cs`:
   - найти корень репозитория (как в 061), прочитать `tokens.json`;
   - для каждого из шести файлов `ImageGen/ImageGen.Maui/Resources/Themes/<Имя>.xaml` разобрать XML (`XDocument`), взять элементы `Color` с атрибутом `x:Key` (пространство `http://schemas.microsoft.com/winfx/2009/xaml`) и сравнить текст с `hex` (без учёта регистра);
   - `Dictionaries_HaveSameKeys` — множества `x:Key` шести файлов совпадают.
7. `tasks/v0.1/016-skeleton-manual-check.md` — дописать в «Комментарии» пункт ручной проверки на Windows: (а) записать в `appearance.json` в папке данных `{"style":"gallery","theme":"dark"}`, запустить — тёмная тема, акцент бирюзовый; (б) при `"theme":"system"` переключить тему Windows — приложение меняет тему без перезапуска. Экрана настроек ещё нет (задача 065).

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android -p:AndroidSdkDirectory=$ANDROID_HOME
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Presentation/Appearance/ThemeSelection.cs`, `ImageGen/ImageGen.Maui/Resources/Themes/*`, `ImageGen/ImageGen.Maui/App.xaml`, `App.xaml.cs`, `MauiProgram.cs`, `Services/MauiAppearanceApplier.cs`, тесты, `tasks/v0.1/016-skeleton-manual-check.md`

## Отчёт

## Комментарии
