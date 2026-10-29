---
id: 075
status: in-progress
assignee: claude-536f
claimed: 2026-10-29T05:16:59Z
created: 2026-10-28
---

# Список сессий: переименование и число прогонов

## Описание
Решение владельца по 074: переименование сессии (пункт Rename: правый клик или «…» на десктопе, долгое нажатие на телефоне) и число прогонов в строке списка («2 min · 3 runs»). Поиск — отдельная задача 079. Меняет `docs/specs/chat-sessions.md` S1.2 («переименование позже»).

Справочник: `docs/design/implementation.md`; макеты: `docs/design/prototype.html` (https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решения владельца — задача 074 и «Комментарии» этой задачи.

Зависит от: 067 (общий список `SessionsListView`), 065 (те же файлы списка).
Среда: Linux или Windows (Avalonia — headless-тесты на Linux; MAUI — сборка Android на Linux с Android SDK, Windows-цель собирает CI; ручные проверки — 016 и 046).

## Критерии готовности
- Rename открывает ввод с текущим названием; пустое или из одних пробелов — не принимается (кнопка недоступна или сообщение); длина обрезается до `Session.MaxTitleLength` (40).
- Новое название сохраняется в БД и не заменяется первым сообщением (`SetTitleFromFirstMessageAsync` меняет только `Session.NewSessionTitle` — уже так).
- В строке: «<время> · N runs» (`1 run` в единственном числе), число берётся одним запросом для всех сессий.
- Тесты из «Плана» проходят; спецификация S1.2 обновлена.

## Анализ
- Ядро: `SessionService` (`ImageGen.Core/Services/SessionService.cs`) — добавить `RenameAsync`; `ISessionRepository.UpdateAsync` уже есть.
- Число прогонов: новый метод `IRunRepository.CountRunsBySessionAsync` → `IReadOnlyDictionary<Guid, int>` одним SQL `SELECT session_id, COUNT(*) FROM runs GROUP BY session_id` (таблица `runs`, `0001_initial.sql`). Параметров нет, конкатенации нет.
- Ввод: `IDialogService.PromptAsync` (есть).
- Меню строки: Avalonia `ContextMenu` на строке (Rename, Delete); MAUI — `SwipeView` (телефоны: Rename и Delete) и `FlyoutBase.ContextFlyout` (десктоп).

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, ключей и строк переноси дословно. Если шаг не получается так, как написано (API другое, компилятор не принимает разметку), не придумывай обход: опиши в «Комментариях», что пробовал, поставь `blocked` и остановись.

1. Core: `SessionService.RenameAsync(Guid sessionId, string title, CancellationToken cancellationToken)` — `Trim`, пусто → `ArgumentException`, длиннее 40 → обрезать, `UpdatedAt` не менять (переименование не поднимает сессию в списке). Тесты `SessionServiceTests`: `RenameAsync_SetsTrimmedTitle`, `RenameAsync_Blank_Throws`, `RenameAsync_Long_Truncates`, `RenameAsync_KeepsUpdatedAt`, `SetTitleFromFirstMessage_AfterRename_KeepsName`.
2. Data: `IRunRepository.CountRunsBySessionAsync(CancellationToken)` + реализация в `RunRepository`; тест `RunRepositoryTests.CountRunsBySession_CountsPerSession` (две сессии, 2 и 0 прогонов → словарь с одной записью).
3. Presentation: `SessionItemViewModel` — `RunCount`, `DetailsText` («2 min · 3 runs»; строки `RunsCountOne` = "1 run", `RunsCountFormat` = "{0} runs"), `Title` становится observable, `RenameCommand`; `SessionsPageModel` при загрузке берёт словарь числа прогонов; переименование — `PromptAsync(AppResources.RenameSessionTitle = "Rename session", "", AppResources.Rename = "Rename", AppResources.Cancel, item.Title)`, затем `RenameAsync`, затем обновить `Title`. Тесты: `Load_ShowsRunCount`, `Rename_UpdatesTitle`, `Rename_Cancelled_KeepsTitle`, `Rename_Blank_ShowsError`.
4. Avalonia `SessionsListView`: `TextBlock.meta` с `DetailsText`; `ContextMenu` (Rename, Delete) на строке. MAUI `Controls/SessionsListView.xaml`: `SwipeView` с Rename и Delete на телефонах, `ContextFlyout` на десктопе.
5. Тест Avalonia `Views/SessionsListViewTests.cs`: `ContextMenu_HasRenameAndDelete`.
6. `docs/specs/chat-sessions.md` S1.2: «Название — первые 40 символов первого сообщения; пользователь может переименовать сессию (до 40 символов)».

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android -p:AndroidSdkDirectory=$ANDROID_HOME
dotnet format ImageGen/<каждый изменённый проект>/<проект>.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Services/SessionService.cs`, `ImageGen/ImageGen.Core/Storage/IRunRepository.cs`, `ImageGen/ImageGen.Data/Repositories/RunRepository.cs`, `ImageGen/ImageGen.Presentation/PageModels/{SessionsPageModel,SessionItemViewModel}.cs`, строки, `ImageGen/ImageGen.Avalonia/Views/SessionsListView.*`, `ImageGen/ImageGen.Maui/Controls/SessionsListView.*`, тесты, `docs/specs/chat-sessions.md`

## Отчёт

## Комментарии
