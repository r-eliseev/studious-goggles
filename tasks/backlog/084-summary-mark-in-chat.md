---
id: 084
status: done
assignee:
claimed:
created: 2026-10-28
---

# Контекст ассистента: пометка о сводке в ленте

## Описание
Третья часть 027 (C4, решение владельца «тонкая пометка в ленте»): если у сессии есть сводка, в ленте после последнего покрытого сообщения — тонкий разделитель «Earlier messages are summarized for the assistant» и кнопка «Show summary», которая показывает текст сводки, модель и цену. Обе оболочки.

Спецификация: `docs/specs/assistant-context.md` (разделы C1–C4 — источник правды, числа оттуда), ADR 0015. Решения владельца — задача 027, «Комментарии».

Зависит от: 083; 077 и 078 (те же файлы чата).
Среда: Linux или Windows (Avalonia — headless-тесты; MAUI — сборка Android, ручная проверка в 016).

## Критерии готовности
- Разделитель стоит после сообщения `CoversUpToMessageId`; без сводки его нет; после новой сводки и перезагрузки ленты он сдвигается.
- «Show summary» показывает текст, модель и цену (строка как у описания картинки: `DescriptionFromFormat`, `DescriptionCostUnknown`).
- Разделитель — `TextBlock.meta` между двумя линиями `IgLineBrush`; доступное имя у кнопки.
- Спецификация `assistant-context.md` — статус «реализовано».
- Тест-кейсы из «Плана» проходят.

## Анализ
- Лента: `ChatPageModel.Items` (`ObservableCollection<object>`: `MessageItemViewModel`, `RunItemViewModel`), загрузка — `SessionService.GetTimelineAsync`. Новый тип элемента `SummaryMarkerViewModel`; в MAUI — шаблон в `ChatItemTemplateSelector` (`ChatItemTemplateChoice.Pick` знает только два типа — расширить на три).
- Сводку отдаёт `IConversationSummaryRepository.GetAsync` (083) — через `SessionService` (новый метод `GetSummaryAsync`), чтобы Presentation не ходил в хранилище напрямую.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов и строк переноси дословно. Если шаг не получается так, как написано, не придумывай обход: опиши в «Комментариях», что пробовал, поставь `blocked` и остановись.

1. Core: `SessionService.GetSummaryAsync(Guid sessionId, CancellationToken)` → `ConversationSummary?`; тест.
2. Presentation: `PageModels/SummaryMarkerViewModel.cs` (`Text` = строка `SummaryMarker` = "Earlier messages are summarized for the assistant", `ShowCommand` → `IDialogService.AlertAsync(AppResources.SummaryTitle = "Summary for the assistant", текст сводки + пустая строка + `DescriptionFromFormat`-строка с моделью и ценой)`); `ChatPageModel` после загрузки вставляет маркер после сообщения `CoversUpToMessageId` (если его нет в ленте — в начало). `ChatItemTemplateChoice.Pick` — третий шаблон. Строки `SummaryMarker`, `SummaryTitle`, `ShowSummary` = "Show summary". Тесты: `Load_WithSummary_InsertsMarkerAfterCoveredMessage`, `Load_NoSummary_NoMarker`, `ShowSummary_AlertsTextModelAndCost`, `Pick_SummaryMarker_ReturnsMarkerTemplate`.
3. Avalonia `ChatView.axaml`: шаблон `SummaryMarkerViewModel` — `Grid` из трёх колонок (`*`, `Auto`, `*`): линии `Border` высотой 1 `IgLineBrush`, в центре `TextBlock.meta` и `Button.ghost.small` «Show summary».
4. MAUI `ChatPage.xaml` и `PageModels/ChatItemTemplateSelector.cs`: то же.
5. Тест Avalonia `Views/SummaryMarkerTests.cs`: сессия с сохранённой сводкой → в ленте есть разделитель, кнопка с доступным именем.
6. `docs/specs/assistant-context.md` — «Статус: реализовано», строка «Код:» с путями.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android -p:AndroidSdkDirectory=$ANDROID_HOME
dotnet format ImageGen/<каждый изменённый проект>/<проект>.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Services/SessionService.cs`, `ImageGen/ImageGen.Presentation/PageModels/{SummaryMarkerViewModel,ChatPageModel,ChatItemTemplateChoice}.cs`, строки, `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml`, `ImageGen/ImageGen.Maui/{Pages/ChatPage.xaml,PageModels/ChatItemTemplateSelector.cs}`, тесты, `docs/specs/assistant-context.md`

## Отчёт

Сделано по плану (`claude-536f`, 2026-10-29). Тесты писались до кода в каждом слое.

- Core: `SessionService.GetSummaryAsync(sessionId, ct)` — сводка из репозитория (083). Конструктор `SessionService` получил репозиторий сводки последним параметром; все вызовы в тестах обновлены. Тесты: `GetSummaryAsync_ReturnsSavedSummary`, `GetSummaryAsync_NoSummary_ReturnsNull`.
- Presentation: `SummaryMarkerViewModel` (`Text` — «Earlier messages are summarized for the assistant», `ShowText` — «Show summary», `ShowCommand` — `AlertAsync` с заголовком «Summary for the assistant», текстом сводки и строкой модели и цены в формате описания картинки). `ChatPageModel.ReloadAsync` вставляет маркер сразу после сообщения `CoversUpToMessageId`; если такого сообщения в ленте нет, маркер идёт первым; без сводки маркера нет. `ChatItemTemplateChoice.Pick` получил третий шаблон (необязательный). Строки `SummaryMarker`, `SummaryTitle`, `ShowSummary`. Тесты: `OnAppearingAsync_WithSummary_InsertsMarkerAfterCoveredMessage`, `OnAppearingAsync_NoSummary_NoMarker`, `ShowSummary_AlertsSummaryWithModelAndPrice`, `Pick_SummaryMarker_ReturnsSummaryTemplate`.
- Avalonia: шаблон маркера в `ChatView.axaml` — тонкие линии `IgLineBrush` по сторонам, в центре `meta`-текст и кнопка «Show summary» с доступным именем. Тест `Views/SummaryMarkerTests.ChatWithSummary_ShowsMarkerWithShowSummaryButton` (сессия с сохранённой сводкой, открыт чат, маркер и кнопка).
- MAUI: `SummaryTemplate` у `ChatItemTemplateSelector` и шаблон в `ChatPage.xaml` (линии `IgLineColor`, текст, кнопка). Сборка `-f net10.0-android` проходит.
- `docs/specs/assistant-context.md`: статус «реализовано», строка «Код:» с путями.

Проверено: Core 345/345, Data 69/69, Presentation 259/259, Avalonia 109/109 (2 пропущены, как раньше), `dotnet format --verify-no-changes` по всем изменённым проектам чистый, MAUI собирается, `scripts/check.sh` проходит.

Отклонения от плана:
- Текст маркера взят из строки `SummaryMarker` без изменений; «Show summary» — отдельная строка `ShowSummary`, как и в плане.
- Тест Avalonia сделан на уровне страницы чата и шаблона, без проверки нажатия (показ `AlertAsync` покрыт тестом `ShowSummary_AlertsSummaryWithModelAndPrice`).
- Ручная проверка вида маркера на MAUI и Windows — в задаче 016 и у владельца.

## Комментарии
