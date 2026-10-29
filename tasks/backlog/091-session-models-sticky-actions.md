---
id: 091
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Модели сессии: кнопки «Save» и «Cancel» всегда видны

## Описание
Замечание владельца (2026-10-29): чтобы сохранить выбор моделей, приходится листать список до самого низа. Кнопки «Save» и «Cancel» должны быть видны всегда, вне прокрутки.

Что известно: на странице `SessionModelsPage` (MAUI) кнопки уже стоят отдельной строкой под `ScrollView`, а в боковой панели широкого окна (`ChatPage.xaml`, «Models pane body») они внутри `ScrollView`; это и видел владелец. Проверить панель параметров (`SessionParametersPage` и её копию в панели) и Avalonia (`SessionModelsView`, `SessionParametersView`).

Зависит от: 090 (те же файлы экрана моделей).
Среда: Linux или Windows (как у 090).

## Критерии готовности
- На странице и в боковой панели моделей кнопки закреплены внизу и видны при любой длине списка и на любой вкладке.
- То же для экрана и панели параметров, если там так же.
- Тест Avalonia: кнопки вне `ScrollViewer`.

## Анализ
Проверено по коду 2026-10-29, до 090:

| Экран | Где кнопки | Вне прокрутки? |
|---|---|---|
| MAUI `SessionModelsPage` | `ButtonRow`, строка `Auto` под `ScrollView` | да |
| MAUI панель моделей в `ChatPage` | последний элемент внутри `ScrollView` | **нет** — это и видел владелец |
| MAUI `SessionParametersPage` | `ButtonRow`, строка `Auto` под `ScrollView` | да |
| MAUI панель параметров в `ChatPage` | `Grid RowDefinitions="*,Auto"`, кнопки во второй строке | да |
| Avalonia `SessionModelsView` (страница и панель) | `ButtonRow`, строка `Auto` под `ScrollViewer` | да |
| Avalonia `SessionParametersView` (страница и панель) | `ButtonRow`, строка `Auto` под `ScrollViewer` | да |

Вывод: сломана только копия панели моделей в MAUI. Задача 090 заменяет её общим `Controls/SessionModelsView`, где кнопки стоят в строке `Auto` вне вкладок и прокрутки. Поэтому 091 закрепляет результат тестами, чтобы вложение кнопок в прокрутку не вернулось, и чинит разметку только если тест упадёт.

Решение плана: панель параметров MAUI в `ContentView` не выносится, у неё кнопки уже вне прокрутки. Клавиатура Android, закрывающая низ экрана, — задача 095, не здесь.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, членов, ключей строк и тестов переноси дословно. Если шаг не получается так, как написано (API другое, компилятор не принимает разметку), не придумывай обход: опиши в «Комментариях», что пробовал, поставь `blocked` и остановись.

Выполнять после: 090 (создаёт `ImageGen.Maui/Controls/SessionModelsView.xaml` и вкладки, на которые опираются тесты). Если 090 не в статусе `done`, поставь `blocked` с комментарием «ждёт 090».

Прочитай перед началом:
- План и «Отчёт» задачи 090.
- `ImageGen/ImageGen.Maui/Controls/SessionModelsView.xaml`, `ImageGen/ImageGen.Maui/Pages/SessionParametersPage.xaml`, в `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml` — блок после комментария «Parameters pane body».
- `ImageGen/ImageGen.Avalonia/Views/SessionModelsView.axaml`, `ImageGen/ImageGen.Avalonia/Views/SessionParametersView.axaml`.
- `ImageGen/ImageGen.Avalonia.Tests/Views/ChatPaneTests.cs` (`OpenWideChatAsync`, `SidePane`), `ImageGen/ImageGen.Avalonia.Tests/Flows/SessionModelsFlowTests.cs`, `ImageGen/ImageGen.Avalonia.Tests/TestDoubles/OpenRouterFakes.cs`.
- `ImageGen/ImageGen.Presentation.Tests/Design/MauiSessionModelsMarkupTests.cs` (создан в 090).

1. Длинный каталог в заглушке. В `OpenRouterFakes.Create` добавь последний необязательный параметр `string? catalogJson = null`: если он задан, ответ на `/images/models` — он, иначе `CatalogJson`. Там же добавь `public static string LongCatalogJson(int count)` — JSON `{"data":[...]}` из `count` моделей `vendor/model-NN` с названием `Model NN` и пустыми `supported_parameters`. Существующие тесты не меняются.
2. Avalonia-тесты, новый файл `ImageGen/ImageGen.Avalonia.Tests/Views/SessionButtonsTests.cs`, класс `SessionButtonsTests`. Сначала напиши все тесты и запусти.
   - `ModelsScreen_SaveAndCancel_OutsideScrollViewerOnBothTabs` — узкое окно (ширина 600, `Show()`), открыть экран моделей, дождаться загрузки. У кнопок с именами `SaveButton` и `CancelButton` нет предка `ScrollViewer` (проверка через `GetVisualAncestors()`). Затем `ShowAssistantModelCommand`, `UpdateLayout()` — то же.
   - `ModelsScreen_LongCatalog_SaveButtonFitsInView` — `ShellHarness` с `OpenRouterFakes.Create(catalogJson: OpenRouterFakes.LongCatalogJson(60))`, окно 600×640, экран моделей открыт, `models.Models.Count == 60`, `UpdateLayout()`. Нижний край `SaveButton` в координатах `SessionModelsView` (через `TranslatePoint` от точки `(0, SaveButton.Bounds.Height)`) не больше `Bounds.Height` представления.
   - `ModelsPane_SaveAndCancel_OutsideScrollViewer` — широкое окно (как `OpenWideChatAsync` в `ChatPaneTests`), `OpenModelsCommand`. В `SessionModelsView` внутри панели у `SaveButton` и `CancelButton` нет предка `ScrollViewer`.
   - `ParametersScreen_SaveAndCancel_OutsideScrollViewer` — открыть экран параметров (`OpenParametersCommand` в узком окне, дождаться `SessionParametersView`). У `SaveButton` и `CancelButton` нет предка `ScrollViewer`.
   - Если какой-то тест падает, перенеси кнопки этого экрана в отдельную строку `Auto` корневого `Grid`, вне `ScrollViewer` и `TabControl`. Это минимальная правка `SessionModelsView.axaml` или `SessionParametersView.axaml`; повтори тесты.
3. MAUI-тесты в `ImageGen/ImageGen.Presentation.Tests/Design/MauiSessionModelsMarkupTests.cs`. Разбирай XAML через `XDocument.Load`; имя `x:Name` — атрибут `Name` в пространстве `http://schemas.microsoft.com/winfx/2009/xaml`.
   - `SessionModelsView_Buttons_OutsideScrollView` — в `Controls/SessionModelsView.xaml` у элементов `Button` с `x:Name` `SaveButton` и `CancelButton` нет предка с локальным именем `ScrollView`.
   - `SessionModelsView_ButtonRow_InAutoRow` — `ButtonRow` — прямой потомок корневого `Grid`. Номер его строки (`Grid.Row`) указывает на `Auto` в `RowDefinitions` корня, а строка с содержимым вкладок — на `*`.
   - `SessionParametersPage_Buttons_OutsideScrollView` — то же для `Pages/SessionParametersPage.xaml` (`SaveButton`, `CancelButton`).
   - `ChatPage_ParametersPaneButtons_OutsideScrollView` — в `Pages/ChatPage.xaml` у каждой `Button` с атрибутом `Command="{Binding SaveCommand}"` или `Command="{Binding CancelCommand}"` нет предка `ScrollView`.
   - Если тест падает, вынеси кнопки в строку `Auto` корневого `Grid` этого блока, вне `ScrollView`, и собери Android.
4. Документация: в `docs/specs/chat-sessions.md`, раздел S5, добавь пункт S5.4: «Save» и «Cancel» экранов и панелей моделей и параметров закреплены внизу, вне прокрутки, и видны при любой длине списка и на любой вкладке.
5. Ручные пункты (см. «Ручная проверка») допиши в задачи 016 и 046.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
bash scripts/check.sh
```
`dotnet format` для `ImageGen.Avalonia` — только если менялась разметка.

### Ручная проверка
- В задачу 016 (MAUI под Windows): боковая панель моделей и параметров в широком окне и страницы в узком — при длинном каталоге (после «Refresh») «Save» и «Cancel» видны без прокрутки на обеих вкладках.
- В задачу 046 (Avalonia): то же.
- Android — в «Отчёт» этой задачи: «не проверено, проверить владельцу: кнопки видны на телефоне без клавиатуры».

Файлы: `ImageGen/ImageGen.Avalonia.Tests/TestDoubles/OpenRouterFakes.cs`, `ImageGen/ImageGen.Avalonia.Tests/Views/SessionButtonsTests.cs` (новый), `ImageGen/ImageGen.Presentation.Tests/Design/MauiSessionModelsMarkupTests.cs`; только если тест упал — `ImageGen/ImageGen.Maui/Controls/SessionModelsView.xaml`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Maui/Pages/SessionParametersPage.xaml`, `ImageGen/ImageGen.Avalonia/Views/SessionModelsView.axaml`, `ImageGen/ImageGen.Avalonia/Views/SessionParametersView.axaml`; `docs/specs/chat-sessions.md`, `tasks/v0.1/016-*.md`, `tasks/backlog/046-*.md`.

## Отчёт

## Комментарии

### 2026-10-29, владелец (в чате, записал claude-plan)
План одобрен; допущения из комментариев claude-plan приняты как решения. План переписан под небольшую модель.
