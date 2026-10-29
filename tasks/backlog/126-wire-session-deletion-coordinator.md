---
id: 126
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Подключить generation coordinator к удалению сессии

## Описание
После 108 Core умеет остановить local jobs, но SessionService.DeleteAsync его не вызывает. Нужно зарегистрировать alias того же GenerationService singleton, вызвать stop до DB/files и обновить constructor call sites. Remote provider cancel добавит 121.

Зависит от: 108.
Среда: Linux или Windows.

## Критерии готовности
- DI `ISessionGenerationCoordinator` и `GenerationService` разрешаются одним ReferenceEquals instance.
- Delete: caller cancellation проверена до stop; после tombstone local stop→DB delete→file delete доводятся cleanup token независимо от поздней caller cancellation.
- Stop строго раньше repository/file delete.
- После return DB rows/folder удалены и не появляются снова; retry Delete идемпотентен.
- Все constructor call sites compile; unrelated session operations unchanged.

## Анализ
GenerationService не зависит от SessionService, DI cycle нет. Alias регистрируется factory к existing singleton, не `AddSingleton<Interface, GenerationService>` вторым instance. 124 не функциональная предпосылка: named HTTP client и singleton alias правят разные строки CoreServiceCollectionExtensions. При rebase сохранить обе регистрации.

Нужность: без wiring108 остаётся неиспользуемой защитой. Пересечение089: SessionService и new-call sites; добавить coordinator обязательным аргументом после существующих обязательных (включая SessionChangeNotifier, если089 уже в main), до optional. Не вставлять обязательный параметр после optional; не откатывать notifier. 089/126 независимы по поведению, но их изменения constructor надо сохранить вместе.

## План
Для Haiku/Luna: только wiring/deletion, не менять RunningJob и remote CancelAsync.

1. Core DI: GenerationService зарегистрирован один раз, alias ISessionGenerationCoordinator через factory GetRequiredService<GenerationService>. В существующем Data.Tests/CompositionTests.AddImageGenCoreAndData_BuildWithValidation_ResolvesAllServices проверить Assert.Same для interface/service. Не создавать Core.Tests composition test, требующий Data: Core.Tests не должен ссылаться на Data.
2. SessionService required constructor param `ISessionGenerationCoordinator generationCoordinator` (без production no-op/default). DeleteAsync: `ThrowIfCancellationRequested`; await Stop with internal cleanup token; then sessions.Delete and files.Delete with same non-caller cleanup token. Позднюю caller cancellation не прерывает destructive sequence.
3. Unit test recording substitutes: stop→repository→files exact order; stop failure means no delete; DB failure means file delete not called and second Delete can retry.
4. Интеграционный тест ТОЛЬКО в `ImageGen.Data.Tests/CompositionTests.cs`: реальный Core+Data DI, temp SQLite/file store и управляемый fake provider/router. Инициализировать SqliteDatabase, создать session/run, дождаться provider gate; вызвать DeleteAsync; fake наблюдает owned cancellation и завершает работу. После Delete return session/run/generation rows и folder отсутствуют; no further writes/events. Для проверяемого late-result сценария provider игнорирует cancellation до TCS: Delete до gate не завершён; release gate, дождаться Delete, проверить отсутствие file/rows. Никаких Task.Delay/scheduler-turn ожиданий.
5. Обновить exact `new SessionService` call sites: Core SessionServiceTests/AssistantServiceTests; Presentation ChatPageModelTests, SessionModelsPageModelTests, SessionParametersPageModelTests, SessionsPageModelTests. Передавать substitute coordinator; no noop production default.
6. Проверить caller cancellation ДО Delete →coordinator/DB/files не вызваны; после входа Stop →поздняя отмена caller не прерывает cleanup. Повтор Delete после успеха безопасен. Если folder deletion бросает после DB delete, второй Delete повторяет только идемпотентные операции и очищает folder. Если089 уже есть, уведомление удаления публикуется после успешного cleanup; не публиковать ложный success при ошибке.
7. Обновить раздел удаления в `docs/architecture.md`: один coordinator/GenerationService singleton, stop до DB→files, cleanup не зависит от поздней caller cancellation; remote cancel добавит121.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Services/SessionService.cs`, `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Core.Tests/Services/SessionServiceTests.cs`, `ImageGen/ImageGen.Core.Tests/Services/AssistantServiceTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ChatPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionModelsPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionParametersPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionsPageModelTests.cs`, `ImageGen/ImageGen.Data.Tests/CompositionTests.cs`, `docs/architecture.md`

После проверок заполнить «Отчёт» (изменения, проверки, непроверенное), выполнить `scripts/release-task.sh 126`, затем `bash scripts/check.sh`; отправить PR по `docs/tasks.md`.

## Отчёт

## Комментарии
