---
id: 126
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Подключить generation coordinator к удалению сессии

## Описание
После 108 Core умеет остановить local jobs, но SessionService.DeleteAsync его не вызывает. Нужно зарегистрировать alias того же GenerationService singleton, вызвать stop до DB/files и обновить constructor call sites. Remote provider cancel добавит 121.

Зависит от: 108, 124.
Среда: Linux или Windows.

## Критерии готовности
- DI `ISessionGenerationCoordinator` и `GenerationService` разрешаются одним ReferenceEquals instance.
- Delete: caller cancellation проверена до stop; после tombstone local stop→DB delete→file delete доводятся cleanup token независимо от поздней caller cancellation.
- Stop строго раньше repository/file delete.
- После return DB rows/folder удалены и не появляются снова; retry Delete идемпотентен.
- Все constructor call sites compile; unrelated session operations unchanged.

## Анализ
GenerationService не зависит от SessionService, DI cycle нет. Alias регистрируется factory к existing singleton, не `AddSingleton<Interface, GenerationService>` вторым instance. Dependency 124 сериализует общий CoreServiceCollectionExtensions file.

## План
Для Haiku/Luna: только wiring/deletion, не менять RunningJob и remote CancelAsync.

1. Core DI: register GenerationService once, interface factory `GetRequiredService<GenerationService>`. Новый CoreServiceCollectionExtensions test ReferenceEquals.
2. SessionService required constructor param interface. DeleteAsync: `ThrowIfCancellationRequested`; await Stop with internal cleanup token; then sessions.Delete and files.Delete with same non-caller cleanup token. Позднюю caller cancellation не прерывает destructive sequence.
3. Unit test recording substitutes: stop→repository→files exact order; stop failure means no delete; DB failure means file delete not called and second Delete can retry.
4. Integration-style test with real GenerationService/temp DB/file store: blocked provider, Delete begins, owned job cancels; after return rows/folder absent; release old gate/scheduler turn, folder remains absent.
5. Обновить exact `new SessionService` call sites: Core SessionServiceTests/AssistantServiceTests; Presentation ChatPageModelTests, SessionModelsPageModelTests, SessionParametersPageModelTests, SessionsPageModelTests. Передавать substitute coordinator; no noop production default.
6. Re-run composition tests Avalonia/Data registration.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Services/SessionService.cs`, `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Core.Tests/Services/CoreServiceCollectionExtensionsTests.cs` (новый), `ImageGen/ImageGen.Core.Tests/Services/SessionServiceTests.cs`, `ImageGen/ImageGen.Core.Tests/Services/AssistantServiceTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ChatPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionModelsPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionParametersPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionsPageModelTests.cs`, `ImageGen/ImageGen.Data.Tests/CompositionTests.cs`

## Отчёт

## Комментарии
