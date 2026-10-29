---
id: 078
status: in-progress
assignee: claude-536f
claimed: 2026-10-29T05:57:08Z
created: 2026-10-28
---

# Пустой чат: кликабельные примеры

## Описание
Решение владельца (2026-10-28, в чате): «давай и её добавим». В пустом состоянии (широкое окно — справа от списка сессий, задача 066; узкое — когда сессий нет) три примера запроса. Нажатие на пример создаёт новую сессию, открывает её чат и сразу отправляет текст примера ассистенту, как если бы пользователь его написал.

Справочник: `docs/design/implementation.md`; макеты: `docs/design/prototype.html` (https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решения владельца — задача 074 и «Комментарии» этой задачи. На макете — экран «Список сессий» (примеры под подсказкой).

Зависит от: 066 (пустое состояние), 079 (те же файлы списка), 077 (те же файлы чата: `ChatPageModel`).
Среда: Linux или Windows (Avalonia — headless-тесты на Linux; MAUI — сборка Android на Linux с Android SDK, Windows-цель собирает CI; ручные проверки — 016 и 046).

## Критерии готовности
- Примеры (строки `.resx`): `EmptyChatExample1` = "A lighthouse on a rocky coast at dusk, watercolor", `EmptyChatExample2` = "Isometric tiny office with plants, soft light", `EmptyChatExample3` = "Sticker set: cat astronaut, 6 poses".
- Нажатие: создаётся сессия, открывается её чат, сообщение отправляется один раз (повторное открытие чата из списка ничего не отправляет).
- Примеры — кнопки с доступными именами.
- Тесты из «Плана» проходят.

## Анализ
- `SessionsPageModel.NewSessionAsync` — образец: `sessionService.CreateAsync`, затем `GoToAsync(Routes.ChatFor(id))`.
- Отправка после открытия: `ChatPageModel.ApplyQueryAttributes` читает `sessionId`; добавить необязательный параметр `message` (`Routes.ChatWithMessage(Guid, string)` с `Uri.EscapeDataString`). После загрузки ленты `ChatPageModel` кладёт текст в `InputText` и выполняет `SendCommand`. Чтобы не отправить второй раз при возврате на страницу, параметр обрабатывается один раз (флаг в модели страницы; в MAUI Shell query живёт в маршруте — после обработки вызвать `query.Clear()` у словаря, если это словарь изменяемый, иначе флаг).
- Пустое состояние на узком окне: в `SessionsView`, когда `Sessions` пуст.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, ключей и строк переноси дословно. Если шаг не получается так, как написано (API другое, компилятор не принимает разметку), не придумывай обход: опиши в «Комментариях», что пробовал, поставь `blocked` и остановись.

1. Presentation: `Routes.MessageParameter = "message"`, `Routes.ChatWithMessage(Guid sessionId, string message)`; тест в `RoutesTests` (текст с пробелами и `&` экранируется и читается обратно).
2. `ChatPageModel`: в `ApplyQueryAttributes` прочитать `message` (раскодировать `Uri.UnescapeDataString`), сохранить в `_pendingMessage`; после первой загрузки ленты — `InputText = _pendingMessage`, `_pendingMessage = null`, `await SendCommand.ExecuteAsync(null)`. Тесты: `Query_WithMessage_SendsOnceAfterLoad`, `Query_WithoutMessage_SendsNothing`, `Reload_AfterPendingSent_DoesNotSendAgain`.
3. `SessionsPageModel`: `Examples` (три строки из ресурсов), `StartExampleCommand(string text)` — создать сессию, `GoToAsync(Routes.ChatWithMessage(id, text))`. Тест `StartExample_CreatesSessionAndNavigatesWithMessage`.
4. Avalonia `SessionsView.axaml`: под подсказкой пустого состояния `ItemsControl` примеров — кнопки на всю ширину блока (ширина 380), левое выравнивание текста, рамка `IgLineBrush`, радиус `IgRadiusMd`; на узком окне пустое состояние с примерами показывается, когда список пуст.
5. MAUI `SessionsPage.xaml`: то же.
6. Тест Avalonia `Flows/EmptyChatExampleFlowTests.cs`: нажатие на первый пример → открыт `ChatView`, в ленте сообщение пользователя с текстом примера и ответ заглушки ассистента.

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

Файлы: `ImageGen/ImageGen.Presentation/{Routes.cs,PageModels/ChatPageModel.cs,PageModels/SessionsPageModel.cs}`, строки, `ImageGen/ImageGen.Avalonia/Views/SessionsView.axaml`, `ImageGen/ImageGen.Maui/Pages/SessionsPage.xaml`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml.cs` (передача query, если нужно), тесты

## Отчёт

## Комментарии
