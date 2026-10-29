---
id: 123
status: planned
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

Malformed wire test нужен на provider level: HTTP 200, `tool_calls.function.name` numeric/object вызывает protocol exception; service отдельно проверяет mapping.

## План
Для Haiku/Luna: не mock sealed AssistantService; использовать существующие real-service fixtures и repository/chat substitutes.

1. Добавить provider test в `OpenAiCompatibleClientTests`: valid JSON syntax, invalid type `function.name`; зафиксировать protocol exception без process crash. Shared wire достаточно OpenRouter/OpenAI-compatible.
2. Refactor accepted turn в build-outcome и persist-outcome phases. ProviderException/known attachment errors формируют outcome в build phase; общий non-cancellation catch только build phase → Unknown.
3. Move attachment catch непосредственно вокруг каждого `ReadDataUrlAsync`. History/title/run storage failures идут Unknown.
4. Удалить широкий `StartRunReplyAsync catch InvalidOperationException`. No image models определить явной проверкой или typed generation result; test AddRunAsync InvalidOperation → Unknown.
5. Persist phase создаёт/добавляет one assistant message. Если AddAsync этого error/normal message падает, exception outward, no retry. Summary/log after success.
6. Tests post-accept failures по одному: SetTitle; history read; chat client; AddRun. Каждый: one user + one assistant error, expected kind, provider call count.
7. Cancellation core test: token cancelled after accepted boundary → OCE, no assistant error. Записать ограничение UI None в XML-doc; не добавлять cancellation UX.
8. Расширенный logger state из 110: operational Error properties SessionId/ProviderId/ModelId/UserMessageId без exception detail; отдельная Debug запись содержит exception. Ни одна запись выше Debug не содержит user text/attachment data/key.
9. Переписать existing presentation post-accept failure test: input/pending empty, no Alert from Send, IsBusy false, timeline user+error. Добавить pre-accept test из 114 отдельно.

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

Файлы: `ImageGen/ImageGen.Core/Services/AssistantService.cs`, `ImageGen/ImageGen.Core.Tests/Services/AssistantServiceTests.cs`, `ImageGen/ImageGen.Core.Tests/OpenAiCompatible/OpenAiCompatibleClientTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ChatPageModelTests.cs`

## Отчёт

## Комментарии
