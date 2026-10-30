---
id: 154
status: planned
assignee:
claimed:
created: 2026-10-30
---

# Изолировать исключения подписчиков SessionChanged

## Описание
SessionChangeNotifier.Notify вызывает multicast event одним Invoke. Если первый подписчик бросает, последующие не вызываются, а исключение уходит в сервис после уже успешной записи сессии/run. Это противоречит одобренному плану089 шаг1, который требовал изоляцию каждого handler и безопасное логирование.

Зависит от: — (остаток089, не зависеть от самой089).
Среда: Linux или Windows.

## Критерии готовности
- Каждый handler из snapshot invocation list вызывается один раз; исключение одного не прекращает остальные и не выходит в caller.
- Warning содержит только безопасный SessionId, без exception object/message; детали доступны отдельной Debug записью.
- Обычный event сохраняет sender/id; отсутствие подписчиков безопасно.
- Все прямые конструкторы и DI согласованы; создание новых уведомлений остаётся отдельной151.

## Анализ
Evidence: Core/Services/SessionChangeNotifier.cs:8 — прямой SessionChanged?.Invoke; Core.Tests/Services/SessionChangeNotifierTests.cs содержит лишь обычный success test. План089 шаг1 явно требует GetInvocationList/ILogger и Notify_HandlerThrows_OtherHandlersStillCalled. Это второй самостоятельный остаток089 наряду с отсутствующим Create notification151. Меняется constructor: найти все new SessionChangeNotifier через rg, включая Core.Tests, Data.Tests, Presentation.Tests, Avalonia.Tests и POC; не удалять тесты для облегчения компиляции.

## План
1. Прочитать notifier, его тест, регистрации CoreServiceCollectionExtensions и usage всех direct constructors.
2. Сначала failing test: три подписчика, средний бросает InvalidOperationException с фиктивным приватным текстом; Notify не бросает, все три вызваны с теми же sender/id. Добавить no-subscriber и ordinary-success cases, сохранить прежний тест.
3. В primary constructor добавить ILogger<SessionChangeNotifier>. В Notify снять snapshot SessionChanged, пройти GetInvocationList по очереди; try/catch Exception вокруг каждого handler. Не делать один общий catch вокруг multicast Invoke.
4. Писать безопасный Warning c SessionId и отдельный LogDebug(exception,...). CapturingLogger test проверяет Exception=null и отсутствие фиктивного текста для всех уровней выше Debug; Debug содержит исходную exception. Logger в tests передавать NullLogger либо CapturingLogger; DI singleton уже умеет inject ILogger.
5. Обновить все direct constructors минимально, не добавлять optional nullable fallback или новый интерфейс. Подписки/отписки во время callback действуют на следующее Notify, а текущий snapshot остаётся стабильным; добавить один тест этого поведения.
6. Regression существующего Rename с throwing subscriber: запись состоялась, метод завершился успешно, следующий handler вызван. Других сервисов/notification points не менять — это151.
7. Дописать результат в089; она остаётся blocked до154 и151. Не утверждать, что изоляция framework logger failures является новой гарантией — это задача об ошибках subscribers.

Файлы: `ImageGen/ImageGen.Core/Services/SessionChangeNotifier.cs`, `ImageGen/ImageGen.Core.Tests/Services/SessionChangeNotifierTests.cs`, direct constructor call sites/tests по результату rg, `tasks/backlog/089-sessions-list-refresh.md`.

Проверка: последовательно Core.Tests, Data.Tests, Presentation.Tests, Avalonia.Tests; format всех изменённых проектов; `bash scripts/check.sh`.

### Команды проверки

Выполнять по одной, из корня репозитория:

```bash
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
bash scripts/check.sh
```

### Порядок исполнения и проверки
План рассчитан на Luna: прочитать перечисленные файлы, выполнить шаги по порядку; сначала падающий регрессионный тест, затем минимальный фикс (ub-mode из AGENTS.md). Не реализовывать соседние задачи. Сигнатуры сверять с текущим main; при rebase сохранять чужие изменения. Не использовать платные API и не записывать секреты. Команды выполнять последовательно: общие build outputs не допускают параллельные dotnet build/test.
После изменения каждого проекта: `dotnet format ImageGen/<Проект>/<Проект>.csproj --verify-no-changes`; после всех изменений — перечисленные тесты и `bash scripts/check.sh`. В отчёте указать команды, результаты, ручные ограничения и отличие от плана. Код писать только после одобрения плана по docs/tasks.md.

## Отчёт

## Комментарии

### 2026-10-30, codex-audit
Выделено при перепроверке плана151: нельзя полагаться на несуществующую изоляцию подписчиков. Код не менялся, реализация ждёт одобрения плана.
