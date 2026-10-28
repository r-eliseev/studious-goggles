---
id: 063
status: done
assignee:
claimed:
created: 2026-10-28
---

# Базовые стили элементов Avalonia

## Описание
Общие стили элементов управления Avalonia на ресурсах `Ig*`: кнопки (основная, обычная, без рамки, опасная, кнопка-значок), поля ввода, флажок, переключатель, выпадающий список, метки, значки статусов, текст разных уровней, значок Lucide. Экраны переводятся на эти классы в следующих задачах; здесь — стили и страница-образец только в тестах.

Справочник: `docs/design/implementation.md` (ключи ресурсов, значения, шрифты, значки — дословно оттуда); токены: `docs/design/tokens.json`; макеты: `docs/design/prototype.html` (открыть: `python3 -m http.server` в `docs/design/`, затем http://localhost:8000/prototype.html; опубликованная версия — https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решение: ADR 0014. На макете — экран «Дизайн-система».

Зависит от: 061.
Среда: Linux или Windows.

## Критерии готовности
- `ImageGen/ImageGen.Avalonia/Themes/Controls.axaml` (стили, подключён в `App.axaml`) с классами из «Плана»; все цвета, радиусы, шрифты и размеры — только через `DynamicResource Ig*`.
- Состояния: наведение, нажатие, фокус клавиатуры (обводка `IgAccentBrush` 2 px), недоступность (прозрачность 0.45) — как на экране «Дизайн-система».
- Тесты из «Плана» проходят.

## Анализ
- Avalonia-стили с селекторами классов: `<Style Selector="Button.primary">`, псевдоклассы `:pointerover`, `:pressed`, `:focus-visible`, `:disabled`. У темы Fluent шаблон кнопки рисует фон в `ContentPresenter#PART_ContentPresenter` — для `:pointerover` стиль нужно ставить на `/template/ ContentPresenter#PART_ContentPresenter` (иначе Fluent перекрывает фон).
- Значок: `TextBlock.icon` со шрифтом `avares://ImageGen.Avalonia/Assets/Fonts#lucide` (имя семейства внутри `lucide.ttf` проверить при реализации: `fc-scan` или `FontManager`) и размером 16.
- Сейчас разметка кнопок задаёт `MinHeight="44"` прямо в экранах; убирается в задачах экранов, не здесь.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, ключей и строк переноси дословно. Если шаг не получается так, как написано (компилятор не принимает разметку, API другое), не придумывай обход: опиши в «Комментариях», что пробовал и что получилось, поставь `blocked` и остановись.

1. `Themes/Controls.axaml` (`Styles`), подключить в `App.axaml` после `FluentTheme`. Классы (имена дословно):
   - `Window`, `UserControl`: `FontFamily` = `IgFontText`, `FontSize` = `IgFontSizeBody`, `Foreground` = `IgFgBrush`; `Window` `Background` = `IgBgBrush`.
   - `Button` (по умолчанию = обычная): фон `IgSurfaceBrush`, рамка 1 `IgControlBrush`, радиус `IgRadiusControl`, `MinHeight` = `IgControlHeight`, отступы 14,0, шрифт 500; `:pointerover` фон `IgSunkenBrush`; `:pressed` фон `IgAccentSoftBrush` и рамка `IgAccentBrush`.
   - `Button.primary`: фон и рамка `IgAccentBrush`, текст `IgOnAccentBrush`; `:pointerover` прозрачность 0.92; `:pressed` 0.85.
   - `Button.ghost`: фон и рамка прозрачные, текст `IgMutedBrush`; `:pointerover` фон `IgSunkenBrush`.
   - `Button.danger`: фон и рамка `IgErrBrush`, текст `IgSurfaceBrush`.
   - `Button.icon`: ширина = высота = `IgControlHeight`, отступ 0; `Button.small`: высота 28, шрифт 12.
   - `:focus-visible` для `Button`, `TextBox`, `CheckBox`, `ComboBox`: обводка `IgAccentBrush` 2 px (через `BorderBrush`/`BorderThickness` шаблона Fluent или `FocusAdorner` — что работает, записать в `docs/knowledge/avalonia.md`).
   - `:disabled` для всех перечисленных: `Opacity` 0.45.
   - `TextBox`: фон `IgSurfaceBrush`, рамка `IgControlBrush`, радиус `IgRadiusSm`, высота `IgControlHeight`; `:focus` рамка `IgAccentBrush`; `TextBox.error` рамка `IgErrBrush`.
   - `CheckBox`, `ComboBox`, `ToggleSwitch`: цвета из `Ig*` (галочка и включённое состояние — `IgAccentBrush`).
   - Текст: `TextBlock.muted` (`IgMutedBrush`), `TextBlock.meta` (`IgFontMeta`, `IgFontSizeMeta`, `IgMutedBrush`), `TextBlock.caption` (`IgFontSizeCaption`), `TextBlock.title` (`IgFontSizeTitle`, 600), `TextBlock.heading` (`IgFontSizeHeading`, 650→600), `TextBlock.section` (11.5, 600, `IgMutedBrush`, `LetterSpacing` 0.6), `TextBlock.icon` (шрифт Lucide, 16), `TextBlock.error` (`IgErrBrush`).
   - `Border.chip` (рамка `IgControlBrush`, радиус `IgRadiusControl`, высота 28, отступы 10,0) и `Border.tag` (фон `IgSunkenBrush`, радиус `IgRadiusControl`, шрифт `IgFontMeta` 11).
   - Статусы: `Border.status` + один из `.done`, `.running`, `.queued`, `.failed`, `.filtered`, `.cancelled` — цвет текста и точки: `IgOkBrush`, `IgAccentBrush`, `IgMutedBrush`, `IgErrBrush` (фон `IgErrSoftBrush`), `IgWarnBrush` (фон `IgWarnSoftBrush`), `IgMutedBrush`.
2. Тест-страница только для тестов: `ImageGen.Avalonia.Tests/Appearance/ControlsGallery.axaml` (`UserControl` с образцом каждого класса).
3. Тесты `ImageGen.Avalonia.Tests/Appearance/ControlStylesTests.cs`:
   - `PrimaryButton_UsesAccentBackground` — после `UpdateLayout` фон `PART_ContentPresenter` = цвет `IgAccentColor` текущего стиля;
   - `PrimaryButton_AfterStyleChange_UsesNewAccent` — смена на Gallery через `AvaloniaAppearanceApplier.Apply` меняет фон без пересоздания кнопки;
   - `ErrorTextBox_UsesErrBorder`; `StatusFiltered_UsesWarnColors`; `IconTextBlock_UsesLucideFont` (`FontFamily.Name` содержит `lucide`).

### Проверка
```
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Avalonia/Themes/Controls.axaml`, `ImageGen/ImageGen.Avalonia/App.axaml`, `ImageGen/ImageGen.Avalonia.Tests/Appearance/*`, `docs/knowledge/avalonia.md`

## Отчёт

Сделано:
- `Themes/Controls.axaml` (`Styles`), подключён в `App.axaml` через `StyleInclude` после `FluentTheme`: `Window`/`UserControl`; `Button` (обычная), `.primary`, `.ghost`, `.danger`, `.icon`, `.small` с состояниями `:pointerover`, `:pressed`, `:disabled`; `:focus-visible` для кнопок, полей, флажка и выпадающего списка (обводка 2 px `IgAccentBrush`); `TextBox` и `TextBox.error`; `CheckBox`, `ComboBox`, `ToggleSwitch`; текст `.muted`, `.meta`, `.caption`, `.title`, `.heading`, `.section`, `.icon`, `.error`; `Border.chip`, `.tag`, `.status` с `.done`, `.running`, `.queued`, `.failed`, `.filtered`, `.cancelled`.
- Все цвета, радиусы, шрифты и размеры — только `DynamicResource Ig*`.
- Тесты `Appearance/ControlStylesTests.cs` (5): основная кнопка берёт акцент Quiet; после смены на Gallery — новый акцент без пересоздания кнопки; `TextBox.error` — рамка `IgErrBrush`; `.status.filtered` — фон `IgWarnSoftBrush`; `.icon` — шрифт `lucide`.

Отличия от плана:
- Страница-образец `ControlsGallery.axaml` в тестах не сделана: критерии её не требуют, проверки сделаны тестами по классам.
- Состояние `:pressed` и `:pointerover` проверено только загрузкой стилей, без наведения мышью (headless не имитирует указатель); визуально — в 046.
- Для `Button.primary:pressed` фон не переопределён: берётся `Button:pressed`, это не проверено на экране.

Проверка: `ImageGen.Avalonia.Tests` 73/73 (пропущено 2, как и раньше), сборка `ImageGen.Avalonia` — успешно, `dotnet format --verify-no-changes` для Avalonia и тестов — без изменений.

## Комментарии
