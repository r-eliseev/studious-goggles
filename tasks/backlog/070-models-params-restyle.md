---
id: 070
status: planned
assignee:
claimed:
created: 2026-10-28
---

# Модели сессии и параметры в новом стиле

## Описание
Перевести экраны «Модели сессии» и «Параметры» на дизайн-систему (макеты): поиск, число запусков — степпер, раздел «Image models · N selected» с кнопкой Refresh, строки моделей с флажком, названием и `IgFontMeta`-строкой (seed / no seed, цена), раздел «Assistant model» с радиокнопками и метками («takes images», «free, limits possible»); параметры — подписи над выпадающими списками, пояснение сверху, кнопки Cancel и Save внизу в порядке платформы (`implementation.md`, раздел 7). Пока это отдельные экраны; панель справа на широком окне — задача 071.

Справочник: `docs/design/implementation.md` (ключи ресурсов, значения, шрифты, значки — дословно оттуда); токены: `docs/design/tokens.json`; макеты: `docs/design/prototype.html` (открыть: `python3 -m http.server` в `docs/design/`, затем http://localhost:8000/prototype.html; опубликованная версия — https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решение: ADR 0014.

Зависит от: 063, 064.
Среда: Linux или Windows (Avalonia — headless-тесты на Linux; MAUI — сборка Android на Linux с Android SDK, Windows-цель собирает CI; запуск под Windows — ручная проверка 016, Avalonia на рабочем столе — 046).

## Критерии готовности
- Модели ассистента — радиокнопки (сейчас кнопки); выбранная отмечена.
- Счётчик «N selected» в заголовке раздела моделей картинок обновляется при отметке.
- Кнопки Save и Cancel внизу экрана; порядок — по платформе (Windows и Linux: Save, Cancel; macOS: Cancel, Save; телефоны MAUI: Save в верхней панели, Cancel — системный назад).
- Тесты из «Плана» проходят; тесты потоков моделей и параметров проходят.

## Анализ
- Разметка: Avalonia `Views/SessionModelsView.axaml`, `Views/SessionParametersView.axaml`; MAUI `Pages/SessionModelsPage.xaml`, `Pages/SessionParametersPage.xaml`.
- `SessionModelsPageModel`: `Models`, `AssistantModels`, `RunsPerModel`, `RunsPerModelText`, `SearchText`, `RefreshCommand`, `SaveCommand`; у `AssistantModelOptionViewModel` — `SelectCommand`, `DetailsText`, `AutomationName`, признак выбора (проверить имя свойства). Нужны: `SelectedCountText` («Image models · {0} selected», строка `ImageModelsSelectedFormat`), `CancelCommand` (назад без сохранения) у обеих моделей страниц. У `AssistantModelOptionViewModel` — `Tags` (список меток) вместо разбора `DetailsText`, если метки сейчас внутри текста.
- Порядок кнопок на десктопе: `OperatingSystem.IsMacOS()` в Avalonia code-behind или `OnPlatform` в MAUI.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, ключей и строк переноси дословно. Если шаг не получается так, как написано (компилятор не принимает разметку, API другое), не придумывай обход: опиши в «Комментариях», что пробовал и что получилось, поставь `blocked` и остановись.

1. Presentation: `SelectedCountText`, `CancelCommand` (обе модели страниц), `AssistantModelOptionViewModel.Tags` (`IsSelected` уже есть; `DisplayName` с «✓ » в новой разметке не нужен — выбор показывает радиокнопка); строки `ImageModelsSelectedFormat` = "Image models · {0} selected", `Cancel` уже есть. Тесты: `ToggleModel_UpdatesSelectedCount`, `Cancel_GoesBackWithoutSaving` (обе модели страниц), `AssistantOption_Tags_ContainTakesImages`.
2. Avalonia `SessionModelsView.axaml`: поиск (`TextBox` со значком `Search`), строка «Runs per model» + `NumericUpDown` (вид степпера), `TextBlock.section` + `Button.ghost.small` Refresh, `ItemsControl` строк с `CheckBox` (название + `TextBlock.meta`), раздел ассистента с `RadioButton` (`GroupName="assistant"`) и `Border.tag`; низ — `StackPanel` кнопок, порядок по ОС (`OperatingSystem.IsMacOS()` в конструкторе view меняет `Grid.Column` у двух кнопок).
3. Avalonia `SessionParametersView.axaml`: пояснение `TextBlock.muted`, четыре пары подпись + `ComboBox`, кнопки как выше.
4. MAUI: те же экраны на `StyleClass`; `ToolbarItem` Save на телефонах (`OnIdiom`), кнопки внизу на десктопе.
5. Тесты Avalonia: `Flows/SessionModelsFlowTests`, `Flows/SessionParametersFlowTests` проходят (обновить поиск элементов, если менялись типы: `RadioButton` вместо `Button`); новый `Views/DialogButtonOrderTests.cs`: `ModelsView_OnWindows_SaveIsFirst` (на Linux тот же порядок, что Windows).

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

Файлы: `ImageGen/ImageGen.Presentation/PageModels/{SessionModelsPageModel,SessionParametersPageModel,AssistantModelOptionViewModel}.cs`, строки, `ImageGen/ImageGen.Avalonia/Views/{SessionModelsView,SessionParametersView}.*`, `ImageGen/ImageGen.Maui/Pages/{SessionModelsPage,SessionParametersPage}.*`, тесты

## Отчёт

## Комментарии
