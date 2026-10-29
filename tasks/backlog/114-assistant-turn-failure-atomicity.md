---
id: 114
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Не дублировать сообщение после неожиданной ошибки ассистента

## Описание
Найдено ревью 2026-10-29. `AssistantService.SendUserMessageAsync` сохраняет user message до сборки запроса и provider call. `CompleteTurnAsync` преобразует `ProviderException` и часть ошибок вложений в assistant error, но другие protocol/storage exceptions выходят наружу. `ChatPageModel.SendAsync` тогда возвращает исходный текст и вложения в editor. Повторная отправка добавляет второе одинаковое user message, хотя первое уже находится в истории.

Зависит от: 110.
Среда: Linux или Windows.

## Критерии готовности
- После того как `messages.AddAsync(userMessage)` успешно завершился, любое последующее non-cancellation исключение, включая `LinkToMessageAsync`, `SetTitleFromFirstMessageAsync`, сборку request и provider protocol, сохраняется как assistant message с `IsError=true`, `ErrorKind.Unknown` либо более точным существующим kind.
- `SendUserMessageAsync` в этом случае возвращает `AssistantTurnResult`, поэтому UI не восстанавливает уже принятое сообщение в editor.
- Исключение до сохранения user message по-прежнему выходит наружу, а UI сохраняет введённый текст/вложения.
- Внешняя отмена не превращается в assistant error автоматически.
- Ошибка сохранения самого assistant error не маскируется и не создаёт бесконечный retry.
- Тесты покрывают malformed provider response, ошибку чтения history и ошибку до записи user message.

## Анализ
- `OpenAiChatWire.ReadReply` может бросить `InvalidOperationException` при неверном JSON type.
- Catch вложений имеет условие с приоритетом операторов: `IOException` ловится всегда, `InvalidOperationException` — только при attachments. Новая обработка не должна ошибочно назвать общий storage/protocol сбой `AttachmentUnreadable`.
- Catch внутри `CompleteTurnAsync` не покрывает `attachments.LinkToMessageAsync` и `SetTitleFromFirstMessageAsync`, которые выполняются уже после принятия user message. Общая граница обработки должна начинаться сразу после успешного `messages.AddAsync`.
- Настоящая транзакция не должна охватывать сетевой вызов. Решение — terminal assistant error после принятого user message, а не длинная SQLite-транзакция.
- Пересекается с 110 по `AssistantService` и tests; выполнять после 110.

## План
Для исполнителя (Haiku/Luna): не добавляй DB-транзакцию вокруг HTTP. Зафиксируй границу «user message уже принято» существующим порядком метода. Один падающий тест — одна правка.

Прочитай: `AssistantService.cs`, `OpenAiChatWire.cs`, `ChatPageModel.SendAsync`, `AssistantServiceTests.cs`, `ChatPageModelTests.cs`, S2.5.

1. Падающие core-тесты: `LinkToMessageAsync`, `SetTitleFromFirstMessageAsync`, чтение history и chat client по очереди бросают non-cancellation exception после успешного `messages.AddAsync`. Во всех случаях user message остаётся одно, сервис добавляет assistant error `Unknown` и возвращает result.
2. Падающий тест до границы: сам `messages.AddAsync(userMessage)` бросает → исключение выходит наружу, assistant error не добавляется.
3. В `SendUserMessageAsync` сразу после успешного добавления user message вызвать новый private helper accepted-turn, который включает link attachments, title update и `CompleteTurnAsync`; общий non-cancellation catch должен охватывать весь helper.
4. Сузить catch `AttachmentUnreadable` до чтения конкретных attachment data URL. Не называть ошибки link/title/history ошибкой вложения.
5. Для общего сбоя записать structured Error без пользовательского текста и создать assistant error через один helper. Не допустить, чтобы существующий catch в `CompleteTurnAsync` и внешний catch добавили два assistant messages.
6. `OperationCanceledException` при отменённом входном token пропускать наружу.
7. Если `messages.AddAsync(assistantError)` само падает, исходное принятое user message остаётся, исключение выходит в UI; не делать бесконечный retry. Тест фиксирует один вызов записи error.
8. Presentation-тест: exception до принятия user message → editor восстановлен. Успешный `AssistantTurnResult` с `IsError` → editor пуст и повторной user message нет.
9. Не менять кнопку Try again: она использует уже сохранённое последнее user message.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Services/AssistantService.cs`, `ImageGen/ImageGen.Core.Tests/Services/AssistantServiceTests.cs`, `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs` (только если тест выявит нужную правку), `ImageGen/ImageGen.Presentation.Tests/PageModels/ChatPageModelTests.cs`

## Отчёт

## Комментарии
