---
id: 091
status: planned
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
Проверено по коду 2026-10-29 (до 090):

| Экран | Где кнопки | Вне прокрутки? |
|---|---|---|
| MAUI `SessionModelsPage` | `ButtonRow`, строка `Auto` под `ScrollView` | да |
| MAUI панель моделей в `ChatPage` | последний элемент внутри `ScrollView` | **нет** — это видел владелец |
| MAUI `SessionParametersPage` | `ButtonRow`, строка `Auto` под `ScrollView` | да |
| MAUI панель параметров в `ChatPage` | `Grid RowDefinitions="*,Auto"`, кнопки во второй строке | да |
| Avalonia `SessionModelsView` (страница и панель) | `ButtonRow`, строка `Auto` под `ScrollViewer` | да |
| Avalonia `SessionParametersView` (страница и панель) | `ButtonRow`, строка `Auto` под `ScrollViewer` | да |

Вывод: сломана только копия панели моделей в MAUI. 090 выносит экран моделей MAUI в `Controls/SessionModelsView` с кнопками в строке `Auto` вне вкладок и прокрутки, и после 090 панель моделей исправлена. Тогда 091 — закрепить это тестами (чтобы копия или вложение в прокрутку не вернулись), проверить поведение на длинном списке и на обеих вкладках, и при необходимости поправить.

Условие «видны при любой длине списка»: строка с прокруткой должна быть `*`, а не внутри `VerticalStackLayout`/`StackPanel` (там `ScrollView` растягивается на всё содержимое и выталкивает кнопки за край). Хост панели (`ChatPage`: `Grid RowDefinitions="Auto,*"`, `ChatView` — аналогично) даёт `ContentView` строку `*` — подходит.

Панель параметров MAUI тоже копия (`ChatPage.xaml` повторяет `SessionParametersPage`), но кнопки там уже вне прокрутки. Вынос её в `ContentView` в рамки 091 не входит (критерий — только видимость кнопок); если нужен — отдельная задача.

Клавиатура Android, закрывающая низ экрана, — задача 095, не здесь.

Пересечения: после 090 (тот же `Controls/SessionModelsView.xaml` и `SessionModelsView.axaml`), до 092 и 093.

## План
Кода в `Presentation` нет; правки только в разметке, если тест упадёт.

Шаги (TDD):
1. Avalonia, падающие или подтверждающие headless-тесты (`Views/SessionModelsButtonsTests` или в `SessionModelsFlowTests`):
   - у `SaveButton` и `CancelButton` в `SessionModelsView` нет предка `ScrollViewer` → на вкладке «Image models» и после переключения на «Assistant model»;
   - каталог из 60 моделей в заглушке, окно 960×640, `UpdateLayout()` → границы `SaveButton` целиком внутри границ представления (кнопка видна без прокрутки);
   - то же в панели широкого окна (`ChatPaneTests`, ширина ≥ 840);
   - у кнопок `SessionParametersView` нет предка `ScrollViewer` (страница и панель).
2. MAUI, текстовый тест (`Presentation.Tests/Design/MauiSessionModelsMarkupTests`, разбор XAML как XML): в `Controls/SessionModelsView.xaml` у элементов `SaveButton`, `CancelButton` нет предка `ScrollView`, а их строка сетки — `Auto`, строка с вкладками — `*`; в `SessionParametersPage.xaml` и в теле панели параметров `ChatPage.xaml` кнопки сохранения и отмены не внутри `ScrollView`.
3. Если тест падает — перенести кнопки в отдельную строку `Auto` корневого `Grid` (минимальная правка разметки) и повторить.
4. Документация: `docs/specs/chat-sessions.md` S5 — «Save» и «Cancel» закреплены внизу экрана и панели, вне прокрутки (и для параметров).
5. Ручные пункты: в 016 (MAUI под Windows: панель моделей и параметров при длинном списке, обе вкладки) и в 046 (Avalonia).

Проверка: `dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj`, `dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj`, `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android` (если менялась разметка MAUI), `dotnet format … --verify-no-changes` по изменённым проектам, `scripts/check.sh`.

Файлы: `ImageGen/ImageGen.Avalonia.Tests/Views/ChatPaneTests.cs`, `ImageGen/ImageGen.Avalonia.Tests/Flows/SessionModelsFlowTests.cs` (или новый `ImageGen/ImageGen.Avalonia.Tests/Views/SessionModelsButtonsTests.cs`), `ImageGen/ImageGen.Avalonia.Tests/TestDoubles/OpenRouterFakes.cs` (длинный каталог), `ImageGen/ImageGen.Presentation.Tests/Design/MauiSessionModelsMarkupTests.cs`; при падении тестов — `ImageGen/ImageGen.Maui/Controls/SessionModelsView.xaml`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Maui/Pages/SessionParametersPage.xaml`, `ImageGen/ImageGen.Avalonia/Views/SessionModelsView.axaml`, `ImageGen/ImageGen.Avalonia/Views/SessionParametersView.axaml`; `docs/specs/chat-sessions.md`, `tasks/v0.1/016-*.md`, `tasks/backlog/046-*.md`.

## Отчёт

## Комментарии
