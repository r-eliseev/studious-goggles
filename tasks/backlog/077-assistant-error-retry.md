---
id: 077
status: in-progress
assignee: claude-536f
claimed: 2026-10-29T05:40:00Z
created: 2026-10-28
---

# Повтор запроса после ошибки ассистента

## Описание
Решение владельца по 074: у сообщения об ошибке ассистента (S2.5: нет сети, лимит, неверный ключ) кнопка «Try again» заново отправляет ассистенту последнее сообщение пользователя (с его вложениями). Новое сообщение пользователя не создаётся; сообщение об ошибке остаётся в истории (в контекст ассистента ошибки и так не попадают — `AssistantService`, фильтр `!m.IsError`).

Справочник: `docs/design/implementation.md`; макеты: `docs/design/prototype.html` (https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решения владельца — задача 074 и «Комментарии» этой задачи.

Зависит от: 068 (вид сообщений), 076 (те же файлы чата), 083 (тот же `AssistantService`: рефакторинг `CompleteTurnAsync` делать поверх сборки запроса со сводкой).
Среда: Linux или Windows (Avalonia — headless-тесты на Linux; MAUI — сборка Android на Linux с Android SDK, Windows-цель собирает CI; ручные проверки — 016 и 046).

## Критерии готовности
- «Try again» есть только у последнего сообщения в чате, если это ошибка ассистента; во время ответа ассистента кнопка недоступна.
- Повтор работает и после перезапуска приложения (опирается на БД, а не на память).
- Успешный повтор добавляет ответ ассистента (и прогон, если ассистент вызвал генерацию) после сообщения об ошибке.
- `docs/specs/chat-sessions.md` S2.5 дополнен.
- Тесты из «Плана» проходят.

## Анализ
- `AssistantService.SendUserMessageAsync` (`ImageGen.Core/Services/AssistantService.cs`) сохраняет сообщение пользователя, связывает вложения, ставит название, затем строит запрос (`BuildRequestAsync`) и обрабатывает ответ. Для повтора нужна та же вторая половина без первых шагов: вынести её в приватный метод `CompleteTurnAsync(Session session, IReadOnlyList<Guid> attachmentIds, CancellationToken)` и вызвать из `SendUserMessageAsync` и нового `RetryLastTurnAsync`.
- Последнее сообщение пользователя и его вложения: `IChatMessageRepository.ListBySessionAsync` (последнее с `Role == User`), вложения — через `IChatAttachmentRepository` по id сообщения (проверить метод; если нет — добавить `ListByMessageAsync`).
- Presentation: `MessageItemViewModel` получает `CanRetry` (последний элемент чата и `IsError`) и `RetryCommand`; `ChatPageModel` вычисляет `CanRetry` при загрузке и блокирует при `IsBusy`.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, ключей и строк переноси дословно. Если шаг не получается так, как написано (API другое, компилятор не принимает разметку), не придумывай обход: опиши в «Комментариях», что пробовал, поставь `blocked` и остановись.

1. Core: рефакторинг `AssistantService` (вынести `CompleteTurnAsync`, поведение `SendUserMessageAsync` не меняется — существующие тесты проходят без правок); новый `RetryLastTurnAsync(Guid sessionId, CancellationToken)`: нет сообщений пользователя → `InvalidOperationException`; берёт вложения последнего сообщения пользователя. Тесты `AssistantServiceTests`: `RetryLastTurn_SendsLastUserMessageAgain`, `RetryLastTurn_DoesNotAddUserMessage`, `RetryLastTurn_ExcludesErrorFromHistory`, `RetryLastTurn_NoUserMessage_Throws`, `RetryLastTurn_WithAttachments_SendsImages`.
2. Presentation: `MessageItemViewModel.CanRetry`, `RetryCommand` (делегат из `ChatPageModel`, как у карточек); `ChatPageModel.RetryAsync` — `IsBusy = true`, `RetryLastTurnAsync`, перезагрузка ленты, ошибки — `AlertAsync`. Строка `TryAgain` = "Try again". Тесты: `Load_LastMessageError_CanRetry`, `Load_ErrorNotLast_CannotRetry`, `Retry_CallsServiceAndReloads`, `Retry_WhileBusy_CannotExecute`.
3. Avalonia `ChatView.axaml` и MAUI `ChatPage.xaml`: в шаблоне сообщения кнопка `Button.ghost.small` со значком `Repeat` и текстом «Try again», видима при `CanRetry`.
4. Тест Avalonia `Flows/AssistantRetryFlowTests.cs`: первый ответ заглушки OpenRouter — 500, второй — текст; после «Try again» в ленте ответ ассистента, сообщений пользователя одно.
5. `docs/specs/chat-sessions.md` S2.5: «…; кнопка „Try again“ у последней ошибки повторяет запрос с последним сообщением пользователя».

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

Файлы: `ImageGen/ImageGen.Core/Services/AssistantService.cs`, `ImageGen/ImageGen.Core/Storage/IChatAttachmentRepository.cs` и `ImageGen/ImageGen.Data/Repositories/*Attachment*` (если нужен метод), `ImageGen/ImageGen.Presentation/PageModels/{ChatPageModel,MessageItemViewModel}.cs`, строки, `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, тесты, `docs/specs/chat-sessions.md`

## Отчёт

## Комментарии
