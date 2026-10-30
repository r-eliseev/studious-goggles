---
id: 144
status: approved
assignee:
claimed:
created: 2026-10-30
---

# Ошибка сохранения картинки должна завершать задание

## Описание
После успешного ответа провайдера исключение IOException/UnauthorizedAccessException из IImageFileStore.SaveAsync уходит в supervisor, который лишь логирует и удаляет job. В БД остаётся Running/Queued, карточка больше не обновляется. Воспроизведение: один успешный provider result, файловое хранилище бросает IOException, БД исправна, WhenIdleAsync завершился — terminal state отсутствует.

Зависит от: 113, 122, 125, 129.
Среда: Linux или Windows.

## Критерии готовности
- При ошибке записи файла и исправной БД generation становится Failed с отдельным кодом ImageSaveFailed; UI показывает локализованную причину и допускает обычный Repeat.
- Ровно одно подтверждённое terminal event; повторного submit/платного запроса и автоматического повтора SaveAsync нет.
- Owned cancellation и ошибки DB persistence сохраняют прежние контракты 108/113/122/125; файл с неизвестным путём не удаляется.
- Покрыты native queued provider и in-memory adapter; success не меняется.

## Анализ
Evidence: GenerationService.cs SaveResultAndPersistAsync:930–931, Launch:522–540. Тест StartRunAsync_ImageSaveThrows_DoesNotInventCleanupPath сейчас проверяет только отсутствие cleanup/success. Это новая политика обработки file-save error, а не незавершённость113: её старый план запрещал второй terminal update для любого сбоя хранения. DB failure по-прежнему нельзя маскировать новым update. Частичный файл внутри SaveAsync относится к141, она не является предпосылкой этой задачи. Пересечение с145: один GenerationService и его тесты; исполнять144 перед145.

## План
1. Прочитать GenerationService (ApplyUpdateAsync, SaveResultAndPersistAsync, FailGenerationAsync, Launch), ErrorKind, ErrorKindText и текущие save/cleanup tests.
2. Сначала тест: SaveAsync бросает IOException, repository update успешен → после WhenIdleAsync последнее сохранённое состояние и event Failed, один submit, нет success, нет DeleteFileAsync. Повторить UnauthorizedAccessException и оба вида queued providers на существующих fake/substitute fixtures.
3. Добавить ErrorKind.ImageSaveFailed следующим свободным числовым значением, не перенумеровывать существующие. Добавить строку ErrorKindImageSaveFailed в AppResources.resx и accessor AppResources.cs; ErrorKindText и его тест отображают её. Текст: «Could not save the generated image. Check free space and folder permissions.»
4. Узкий try/catch охватывает только вызов files.SaveAsync до получения path. Ловить только IOException/UnauthorizedAccessException; перед terminal update проверить owned cancellation. Заполнить Failed/code/безопасное ErrorMessage/CompletedAt и известную result.CostUsd, сохранить через SaveAndNotifyAsync; завершить обработку update без перехода в success.
5. Нельзя окружать этим catch PersistStateWithRetryAsync, cleanup или NotifyConfirmedState. Если Failed UPDATE тоже падает — пусть supervisor113 наблюдает ошибку; не пытаться третий update. OperationCanceledException не превращать в ImageSaveFailed. Не выдумывать relativePath и не удалять папку.
6. Добавить regression: own cancellation не terminal error; Failed UPDATE failure не повторяется сверх политики125; успешная запись по-прежнему даёт один Succeeded; существующие tests122 проходят. Обновить старый save-throws test, сохранив его проверки cleanup.
7. Обновить описание ошибки/границ хранения в docs/architecture.md или specs/chat-sessions.md только в соответствующем абзаце. Новый код enum не требует SQLite migration.

Файлы: `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core/Domain/Generations/ErrorKind.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`, `ImageGen/ImageGen.Presentation/Services/ErrorKindText.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `ImageGen/ImageGen.Presentation.Tests/Services/ErrorKindTextTests.cs`, `docs/architecture.md`.

Проверка: `dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj`; затем Presentation.Tests.

### Команды проверки

Выполнять по одной, из корня репозитория:

```bash
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
bash scripts/check.sh
```

### Порядок исполнения и проверки
План рассчитан на Luna: прочитать перечисленные файлы, выполнить шаги по порядку; сначала падающий регрессионный тест, затем минимальный фикс (ub-mode из AGENTS.md). Не реализовывать соседние задачи. Сигнатуры сверять с текущим main; при rebase сохранять чужие изменения. Не использовать платные API и не записывать секреты. Команды выполнять последовательно: общие build outputs не допускают параллельные dotnet build/test.
После изменения каждого проекта: `dotnet format ImageGen/<Проект>/<Проект>.csproj --verify-no-changes`; после всех изменений — перечисленные тесты и `bash scripts/check.sh`. В отчёте указать команды, результаты, ручные ограничения и отличие от плана. Код писать только после одобрения плана по docs/tasks.md.

## Отчёт

## Комментарии

### 2026-10-30, codex-audit
Создано по поручению владельца: подробный план для Luna. В этой задаче код ещё не исправлен; статус planned не означает одобрение реализации.

### 2026-10-30, владелец (в чате, записал codex)
План одобрен явным поручением «Заапрувь таски на фиксы». Можно реализовывать по описанному плану после готовности зависимостей в main и проверки среды/занятости по docs/tasks.md.
