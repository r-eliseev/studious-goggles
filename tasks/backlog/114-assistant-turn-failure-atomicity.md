---
id: 114
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Атомарно принять user message и его attachments

## Описание
Сейчас user message insert и attachment link — разные commits. Link failure оставляет message в history, attachments unlinked, а UI восстанавливает input; повтор создаёт duplicate. Эта задача делает короткую локальную transaction границей принятия. Обработка ошибок после принятия вынесена в 123.

Зависит от: 110.
Среда: Linux или Windows.

## Критерии готовности
- User message insert и link всех attachment ids происходят в одной SQLite transaction.
- Missing/wrong-session/already-linked attachment → rollback: нет user row, ни один attachment не изменён.
- После commit message и все attachments доступны `RetryLastTurnAsync`.
- Transaction не охватывает title update, context/provider HTTP, run generation или assistant reply.
- Pre-accept failure выходит в UI; editor и pending attachments восстанавливаются, provider не вызывается.

## Анализ
Предпочтительный узкий contract: новый `IChatMessageRepository.AddUserWithAttachmentsAsync(message, ids, token)`. Data ChatMessageRepository владеет transaction и обновляет `chat_attachments`. После перевода production call удалить старые `AttachmentService.LinkToMessageAsync`, `IChatAttachmentRepository.LinkToMessageAsync` и implementation: grep подтверждает, что иных production calls нет. Не добавлять отдельный repository service/DI. Задача нужна для предотвращения дубликатов при повторной отправке после частичной записи. Пересечения: 110 меняет fixtures AssistantService, 096/097 — ChatPageModelTests; перед правкой тестов rebase и сохранить их сценарии. Удаление старого API требует обновить ВСЕ test doubles и проверки через `rg -n "LinkToMessageAsync|IChatMessageRepository" ImageGen`.

## План
Для Haiku/Luna: сначала Data tests, затем Core, затем Presentation. Никакой transaction вокруг HTTP.

1. Контракт `Task AddUserWithAttachmentsAsync(ChatMessage message, IReadOnlyList<Guid> attachmentIds, CancellationToken cancellationToken)` в `IChatMessageRepository`: null аргументы → ArgumentNullException; роль не User или повторяющийся id → ArgumentException до записи. Missing/wrong-session/already-linked id → InvalidOperationException; все аргументы SQL параметризованы. В Data реализовать одной connection/transaction: validate role User; insert message; для каждого attachment update `WHERE id AND session_id AND message_id IS NULL`; affected rows exactly 1; commit. Любая ошибка rollback.
2. Data tests на реальной временной SQLite: no attachments; two valid attachments; second missing → user row absent и first unlinked; wrong session; already linked; duplicate ids и non-User rejected без изменений; заранее отменённый token не создаёт строки. Проверять состояние новой connection после завершения метода. Rollback при ошибке после первого UPDATE доказывает тест second missing; не использовать задержки для гонки cancellation.
3. В Core/Presentation fixtures `AddAsync` сейчас наполняет in-memory history: перенести запись user rows в substitute нового atomic method, оставить `AddAsync` для assistant rows. Проверки links перенести с `_chatAttachments.LinkToMessageAsync` на аргументы atomic call. AssistantService вместо `messages.AddAsync + attachments.LinkToMessageAsync` вызывает atomic method. Accepted boundary — успешный return method.
4. Удалить `AttachmentService.LinkToMessageAsync`, interface member и implementation `IChatAttachmentRepository.LinkToMessageAsync`; удалить/перенести старые repository/service tests на atomic method.
5. Core tests: atomic method failure → chat client/title not called; exception outward; no assistant row.
6. Переписать existing `SendCommand_AssistantFailure_Restores...` только для pre-accept repository failure: text/attachments restored, alert shown, IsBusy false.
7. Success regression: one user row with attachment ids; pending cleared; retry reads same attachments. В114 provider failure ПОСЛЕ accept пока сохраняет прежнее поведение; его новый terminal outcome и отдельный UI-сценарий делает123. Тест `SendCommand_PreAcceptFailure_RestoresTextAndAttachments` из шага6 в123 не удалять.
8. После проверок заполнить «Отчёт» (изменения, команды, ограничения), выполнить `scripts/release-task.sh 114` и `bash scripts/check.sh`; обновить PR по `docs/tasks.md`. Код менять маленькими шагами TDD.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Storage/IChatMessageRepository.cs`, `ImageGen/ImageGen.Core/Storage/IChatAttachmentRepository.cs`, `ImageGen/ImageGen.Core/Services/AttachmentService.cs`, `ImageGen/ImageGen.Data/Repositories/ChatMessageRepository.cs`, `ImageGen/ImageGen.Data/Repositories/ChatAttachmentRepository.cs`, `ImageGen/ImageGen.Core/Services/AssistantService.cs`, `ImageGen/ImageGen.Data.Tests/Repositories/ChatMessageRepositoryTests.cs`, `ImageGen/ImageGen.Data.Tests/ChatAttachmentRepositoryTests.cs`, `ImageGen/ImageGen.Core.Tests/Services/AssistantServiceTests.cs`, `ImageGen/ImageGen.Core.Tests/Services/AttachmentServiceTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ChatPageModelTests.cs`

## Отчёт

## Комментарии
