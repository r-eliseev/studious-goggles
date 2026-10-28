---
id: 064
status: planned
assignee:
claimed:
created: 2026-10-28
---

# Базовые стили элементов MAUI

## Описание
То же, что 063, для MAUI: заменить шаблонные `Colors.xaml` и `Styles.xaml` стилями на ресурсах `Ig*`. После задачи у приложения нет шаблонного фиолетового и шрифта OpenSans.

Справочник: `docs/design/implementation.md` (ключи ресурсов, значения, шрифты, значки — дословно оттуда); токены: `docs/design/tokens.json`; макеты: `docs/design/prototype.html` (открыть: `python3 -m http.server` в `docs/design/`, затем http://localhost:8000/prototype.html; опубликованная версия — https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решение: ADR 0014. На макете — экран «Дизайн-система».

Зависит от: 062.
Среда: Linux или Windows (Avalonia — headless-тесты на Linux; MAUI — сборка Android на Linux с Android SDK, Windows-цель собирает CI; запуск под Windows — ручная проверка 016, Avalonia на рабочем столе — 046).

## Критерии готовности
- `Resources/Styles/Styles.xaml` переписан на `DynamicResource Ig*`; `Colors.xaml` удалён (и строка о нём в `App.xaml`); OpenSans удалён из `Resources/Fonts` и `ConfigureFonts`.
- Стили-классы из «Плана» (`StyleClass`) есть; неявные стили для `Page`, `Label`, `Button`, `Entry`, `Editor`, `CheckBox`, `Picker`, `Switch`, `Border`, `ActivityIndicator`, `ProgressBar`, `Shell`.
- Состояния через `VisualStateManager`: `Normal`, `PointerOver` (Windows, Mac), `Pressed`, `Disabled`, `Focused`.
- Сборка Android проходит; тест на отсутствие шаблонных ключей проходит.

## Анализ
- `Styles.xaml` шаблона ссылается на `Primary`, `Gray*`, `White` и т. д. из `Colors.xaml` через `StaticResource` и `AppThemeBinding`; всё это заменяется на `DynamicResource` (`AppThemeBinding` больше не нужен: тему выбирает словарь из 062).
- Экраны используют `Primary`? Проверить `grep -rn "StaticResource\|DynamicResource" ImageGen/ImageGen.Maui/Pages` и заменить найденные ключи на `Ig*` в этой же задаче.
- `StyleClass` в MAUI: `<Style Class="primary" TargetType="Button">`, в разметке `StyleClass="primary"`.
- Значок Lucide: `Label` с `FontFamily="LucideIcons"`, текст — `{x:Static appearance:IconGlyphs.Plus}`; для кнопки — `FontImageSource` с тем же шрифтом.
- Высота элемента: `IgControlHeight` (Windows 32, iOS 44, Android 48) — правило размера касания из `docs/design/research.md`.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, ключей и строк переноси дословно. Если шаг не получается так, как написано (компилятор не принимает разметку, API другое), не придумывай обход: опиши в «Комментариях», что пробовал и что получилось, поставь `blocked` и остановись.

1. `Styles.xaml` переписать целиком. Неявные стили: `Page` и `Shell` (фон `IgBgColor`, у `Shell` — цвета панели: `Shell.BackgroundColor` = `IgSunkenColor`, `Shell.ForegroundColor`/`TitleColor` = `IgFgColor`), `Label` (`IgFontText`, `IgFontSizeBody`, `IgFgColor`), `Button` (как обычная кнопка в 063: фон `IgSurfaceColor`, рамка 1 `IgControlColor`, `CornerRadius` — число из `IgRadiusControl`; если `CornerRadius` у `Button` — `int`, добавить в словари 062 ресурс `IgButtonCornerRadius` типа `x:Int32` и записать в `implementation.md`), `Entry`, `Editor`, `Picker` (фон `IgSurfaceColor`, текст `IgFgColor`, подсказка `IgMutedColor`), `CheckBox` и `Switch` (`IgAccentColor`), `ActivityIndicator`/`ProgressBar` (`IgAccentColor`), `Border` (рамка `IgLineColor`).
2. Классы: `Button` — `primary`, `ghost`, `danger`, `icon`, `small`; `Label` — `muted`, `meta`, `caption`, `title`, `heading`, `section`, `icon`, `error`; `Border` — `chip`, `tag`, `card`, `status`, и для статусов `Label` — `status-done`, `status-running`, `status-queued`, `status-failed`, `status-filtered`, `status-cancelled` (цвета как в 063).
3. `VisualStateManager` для `Button`: `PointerOver` (фон `IgSunkenColor`; у `primary` — прозрачность 0.92), `Pressed`, `Disabled` (прозрачность 0.45), `Focused` (рамка `IgAccentColor` 2).
4. Удалить `Resources/Styles/Colors.xaml`, строку в `App.xaml`, `OpenSans-*.ttf` и их `AddFont`. Заменить в `Pages/*.xaml` ссылки на удалённые ключи.
5. Тест `ImageGen.Presentation.Tests/Design/MauiStylesTests.cs`:
   - `Styles_DoNotReferenceTemplateKeys` — в `Styles.xaml` и `Pages/*.xaml` нет `Primary`, `Secondary`, `Tertiary`, `Gray100`…`Gray950`, `OffBlack`, `OpenSans`;
   - `Styles_UseOnlyIgResources` — каждое `DynamicResource X`/`StaticResource X` в `Styles.xaml` начинается с `Ig`.
6. `docs/knowledge/maui.md` — что выяснилось про `CornerRadius` кнопки и `VisualStateManager` на Windows.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android -p:AndroidSdkDirectory=$ANDROID_HOME
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Maui/Resources/Styles/*`, `ImageGen/ImageGen.Maui/Resources/Fonts/OpenSans-*`, `ImageGen/ImageGen.Maui/App.xaml`, `MauiProgram.cs`, `ImageGen/ImageGen.Maui/Pages/*.xaml` (только замена ключей), `ImageGen/ImageGen.Maui/Resources/Themes/*` (если нужен `IgButtonCornerRadius`), тесты, `docs/design/implementation.md`, `docs/knowledge/maui.md`

## Отчёт

## Комментарии
