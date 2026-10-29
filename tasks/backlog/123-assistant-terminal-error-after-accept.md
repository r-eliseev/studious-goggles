---
id: 123
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Сохранить terminal assistant error после принятого сообщения

## Описание
После atomic accept 114 любые title/context/provider/protocol/run exceptions должны дать ровно один assistant error вместо exception в UI и повторного user message. Current `CompleteTurnAsync` catch scopes и широкий `StartRunReplyAsync catch InvalidOperationException` классифицируют часть storage errors неверно.

Зависит от: 114, 127.
Среда: Linux или Windows.

## Критерии готовности
- После accepted user message любой non-cancellation failure до terminal assistant persistence превращается в outcome `IsError/Unknown` либо существующий точный Provider/Attachment kind.
- Terminal assistant message записывается ровно один раз. Failure этой записи выходит наружу и не вызывает вторую попытку error message.
- Summary request и finished log только после успешной terminal запись.
- `OperationCanceledException` caller после accept выходит наружу; UI сейчас передаёт None, поэтому этот core contract не вызывает duplicate в production.
- Storage InvalidOperation из generation path → Unknown, не ModelUnavailable.
- Unexpected operational Error содержит только structured SessionId/ProviderId/ModelId/UserMessageId; полный exception detail только Debug; user text/attachment data/key не логируются выше Debug.

## Анализ
Нужны две фазы: `BuildAcceptedTurnOutcomeAsync` без terminal write и `PersistAssistantOutcomeAsync` с одной записью. Catch outcome не должен охватывать persistence assistant message. Attachment unreadable catch — только вокруг `ReadDataUrlAsync`. Известный no-model case проверяется явно/typed result, а не широким InvalidOperation catch.

Malformed wire проверяется на provider level: HTTP 200 с неверным типом `tool_calls.function.name` сейчас выбрасывает InvalidOperationException из `GetValue<string>()` в `OpenAiChatWire.ReadReply`. Отдельного типа protocol exception нет: явно проверять форму wire и возвращать существующий `ProviderException(ErrorKind.Unknown)` с постоянным безопасным сообщением. Не добавлять catch вокруг всего provider/service, скрывающий storage failures. Необходимость задачи: accepted user row уже нельзя автоматически повторять как новую отправку.

Логирование следует policy127 (отдельный новый ADR и conventions), в123 менять только затронутые ветки AssistantService. Пересечения ChatPageModelTests с096/097 сохраняются при rebase.

## План
Для Haiku/Luna: не mock sealed AssistantService; использовать существующие real-service fixtures и repository/chat substitutes.

1. Добавить provider tests `CompleteChatAsync_MalformedToolName_ReturnsUnknown`: valid JSON syntax, `function.name` number/object → ProviderException/Unknown, фиксированное message без содержимого ответа. В `OpenAiChatWire.ReadReply` валидировать object/array/string через JsonNode shape и TryGetValue<string>; для неверной структуры tool call возвращать тот же ProviderException. Нормальные string arguments и object arguments поддерживать как сейчас; пустой/отсутствующий name сохраняет прежний skip. Shared wire покрывает OpenRouter/OpenAI-compatible; generic service test отдельно бросает ProviderException/Unknown.
2. Добавить приватный record `AcceptedTurnOutcome` с Run?, Text, ErrorKind?, MessageCount, ToolCallCount; выделить `BuildAcceptedTurnOutcomeAsync` и `PersistAssistantOutcomeAsync`. Перенести `SetTitleFromFirstMessageAsync` из SendUserMessageAsync ПОСЛЕ atomic accept внутрь build catch: сейчас title вызывается до CompleteTurnAsync и иначе выпадет из новой границы. Trace user-input тоже перенести внутрь build catch после accept: его сбой не должен выпадать мимо outcome. При retry title не менять (явный bool updateTitle). Refactor accepted turn в build-outcome и persist-outcome phases. ProviderException/known attachment errors формируют outcome в build phase; общий non-cancellation catch только build phase → Unknown.
3. Move attachment catch непосредственно вокруг каждого `ReadDataUrlAsync`. History/title/run storage failures идут Unknown.
4. Удалить широкий `StartRunReplyAsync catch InvalidOperationException`. Перед `StartRunWithAttachmentsAsync` явно проверить `session.Settings.ImageModels.Count == 0` (передать уже загруженный Session): outcome ModelUnavailable без вызова generation. Проверку null Run → ModelWithoutReferences сохранить. Другие исключения из generation не перехватывать как ModelUnavailable; test AddRunAsync InvalidOperation → Unknown.
5. Persist phase создаёт/добавляет one assistant message. Если AddAsync этого error/normal message падает, exception outward, no retry. Summary/log after success.
6. Tests post-accept failures по одному: SetTitle; history read; chat client; AddRun. Каждый: one user + one assistant error, expected kind, provider call count.
7. Cancellation core test: dependency бросает OCE с отменённым caller token after accepted boundary → OCE outward, no assistant error. OCE при НЕотменённом caller token считать Unknown (это не пользовательская отмена). Записать ограничение UI None в XML-doc; не добавлять cancellation UX.
8. Расширенный logger state из 110: operational Error properties SessionId/ProviderId/ModelId/UserMessageId без exception detail; отдельная Debug запись содержит exception. Ни одна запись выше Debug не содержит user text/attachment data/key.
9. Добавить отдельный `SendCommand_PostAcceptFailure_KeepsEditorClearedAndShowsTerminalError`: input/pending empty, no Alert from Send, IsBusy false, timeline user+error. Pre-accept test уже создан114: сохранить без дублирования. Добавить terminal-write-failure тест для normal и error outcome: AddAsync вызван один раз, исключение outward, summary не запрошена, finished log отсутствует. `summaryService.Request` и finished log выполнять после persistence; их диагностическая ошибка не должна повторно писать terminal message.
10. После проверок заполнить «Отчёт» (изменения, команды, ограничения), выполнить `scripts/release-task.sh 123` и `bash scripts/check.sh`; обновить PR по `docs/tasks.md`. Код менять маленькими шагами TDD.

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

Файлы: `ImageGen/ImageGen.Core/Services/AssistantService.cs`, `ImageGen/ImageGen.Core.Tests/Services/AssistantServiceTests.cs`, `ImageGen/ImageGen.Core.Tests/OpenAiCompatible/OpenAiCompatibleClientTests.cs`, `ImageGen/ImageGen.Core/OpenAiCompatible/OpenAiChatWire.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ChatPageModelTests.cs`

## Отчёт

## Комментарии
