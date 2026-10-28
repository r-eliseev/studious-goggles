---
id: 067
status: approved
assignee:
claimed:
created: 2026-10-28
---

# Широкое окно: список сессий рядом с чатом

## Описание
На широком окне (≥ 840) экран чата показывает слева тот же список сессий (272 px, как на макете «Чат»), выбор сессии в списке открывает её чат, «New session» создаёт новую. На узком — как сейчас: только чат, назад — к списку.

Справочник: `docs/design/implementation.md` (ключи ресурсов, значения, шрифты, значки — дословно оттуда); токены: `docs/design/tokens.json`; макеты: `docs/design/prototype.html` (открыть: `python3 -m http.server` в `docs/design/`, затем http://localhost:8000/prototype.html; опубликованная версия — https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решение: ADR 0014.

Зависит от: 066, 072.
Среда: Linux или Windows (Avalonia — headless-тесты на Linux; MAUI — сборка Android на Linux с Android SDK, Windows-цель собирает CI; запуск под Windows — ручная проверка 016, Avalonia на рабочем столе — 046).

## Критерии готовности
- Список сессий вынесен в отдельный элемент (`SessionsListView` в Avalonia, `SessionsListView` — `ContentView` в MAUI), его используют экран списка (066) и экран чата.
- В чате на ширине ≥ 840 список виден, текущая сессия выделена; < 840 — скрыт.
- Открытие другой сессии из списка в чате заменяет текущий чат, а не кладёт его в стек (назад не возвращает к прошлому чату).
- Тесты из «Плана» проходят.

## Анализ
- `SessionsPageModel` — синглтон в обеих оболочках, поэтому один экземпляр можно показывать и на экране списка, и в чате.
- Выделение текущей сессии: `SessionItemViewModel.IsCurrent` — задаётся `SessionsPageModel.SetCurrent(Guid?)`, который вызывает `ChatPageModel` при загрузке сессии (через новый интерфейс или событие; не давать `ChatPageModel` прямую ссылку на `SessionsPageModel`, если это ломает тесты — тогда `ICurrentSessionTracker` в Presentation, синглтон).
- Замена чата без стека: Avalonia — `AvaloniaNavigationService` (посмотреть, есть ли замена; если нет — добавить маршрут вида `//chat?...` или метод `ReplaceAsync`); MAUI Shell — маршрут `//sessions/chat?...` или `GoToAsync("../chat?…")`. Выбрать при реализации и записать в `docs/knowledge/`.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, ключей и строк переноси дословно. Если шаг не получается так, как написано (компилятор не принимает разметку, API другое), не придумывай обход: опиши в «Комментариях», что пробовал и что получилось, поставь `blocked` и остановись.

0. Presentation: удаление сессии в `SessionsPageModel` вызывает `ConfirmAsync(..., destructive: true)` (перегрузка из 072). Тест `Delete_AsksDestructiveConfirm` (подмена `IDialogService` получила `destructive: true`).
1. Presentation: `ICurrentSessionTracker` (`Guid? CurrentId`, событие `Changed`) и реализация `CurrentSessionTracker` (синглтон); `ChatPageModel` сообщает свою сессию при загрузке; `SessionsPageModel` подписан и выставляет `SessionItemViewModel.IsCurrent`. Тесты: `OpenChat_MarksSessionCurrent`, `OpenAnotherChat_MovesMark`.
2. `INavigationService`: если нужен, метод `ReplaceAsync(string route)` (заменить верхнюю страницу). Реализации в обеих оболочках и тест Avalonia навигации (`Services/Navigation`).
3. Avalonia: `Views/SessionsListView.axaml` — левая колонка из 066 переносится сюда; `SessionsView` и `ChatView` включают её. `ChatView`: колонки 272 и `*`, обработчик ширины как в 066.
4. MAUI: `Controls/SessionsListView.xaml` (`ContentView`, `x:DataType` `SessionsPageModel`, `BindingContext` из DI); `ChatPage` включает его с `AdaptiveTrigger`.
5. Тесты Avalonia `Views/ChatLayoutTests.cs`: `WideChat_ShowsSessionsList`, `NarrowChat_HidesSessionsList`, `SelectSessionInChat_ReplacesChat` (после выбора `GoBackAsync` ведёт к списку, а не к прошлому чату).

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

Файлы: `ImageGen/ImageGen.Presentation/{Services/ICurrentSessionTracker.cs,Services/CurrentSessionTracker.cs,Services/INavigationService.cs,PageModels/ChatPageModel.cs,PageModels/SessionsPageModel.cs,PageModels/SessionItemViewModel.cs}`, `ImageGen/ImageGen.Avalonia/{Views/SessionsListView.*,Views/SessionsView.axaml,Views/ChatView.*,Services/Navigation/*}`, `ImageGen/ImageGen.Maui/{Controls/SessionsListView.*,Pages/ChatPage.xaml,Pages/SessionsPage.xaml,Services/ShellNavigationService.cs}`, тесты, `docs/knowledge/*`

## Отчёт

## Комментарии
