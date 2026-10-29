---
id: 108
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Остановить активные задания перед удалением сессии

## Описание
Найдено ревью 2026-10-29. `SessionService.DeleteAsync` удаляет строки БД и каталог изображений, но запущенные `GenerationService` задачи используют `CancellationToken.None`. После подтверждённого удаления провайдер продолжает платную работу; пришедший результат может заново создать `images/<sessionId>` и попытаться обновить уже удалённую строку.

Зависит от: 106, 107.
Среда: Linux или Windows.

## Критерии готовности
- После начала удаления session-level barrier запрещает создавать и регистрировать новые runs/generation jobs этой сессии; конкурентный `StartRunAsync` завершается контролируемой ошибкой до платного provider call.
- Перед удалением строк и файлов все уже начатые операции создания run и локальные generation-задачи этой сессии получают отмену и завершаются.
- Для queued-задания с handle выполняется best-effort `IQueuedImageProvider.CancelAsync`; ошибка remote cancel логируется без ключа и не запрещает локальное удаление.
- После возврата `SessionService.DeleteAsync` никакая старая задача не может записать файл или обновить generation удалённой сессии.
- Задачи других сессий не отменяются.
- Повторное удаление/остановка идемпотентно.
- Есть unit-тест с заблокированным провайдером и интеграционный тест с временной БД/папкой.

## Анализ
- `_running` хранит только `generationId → Task`; токены и `sessionId` не хранятся.
- Между `AddRunAsync` и `Launch` есть race: простой повторный snapshot `_running` может решить, что работ нет, прежде чем конкурентный run зарегистрирует tasks. Нужен session-level barrier, который атомарно запрещает новые start-операции и ждёт уже получившие lease операции.
- `GenerationService` sealed, поэтому `SessionService` нельзя подменить через concrete type в тесте порядка. Нужен узкий interface coordinator.
- `Launch`, `ExecuteAsync`, queued watch, provider calls и file writes используют `CancellationToken.None`.
- `FileImageStore.SaveAsync` создаёт удалённый каталог заново.
- Пересекается с задачей 024 (пользовательская отмена) и 102 (единый контракт OpenRouter). Эта задача вводит только координацию удаления и не добавляет UI-кнопки. Реализацию 024 и 102 затем строить поверх того же cancellation-механизма.

## План
Для исполнителя (план рассчитан на Haiku/Luna): изменение широкое, поэтому выполняй строго по шагам. После каждого шага запускай тесты ядра. Не добавляй UI. Не меняй статус задачи 024. Если появляется циклическая DI-зависимость, не используй service locator: запиши граф зависимостей, поставь `blocked`.

Прочитай: `GenerationService.cs`, `SessionService.cs`, `IRunRepository.cs`, `GenerationServiceTests.cs`, `SessionServiceTests.cs`, data-тесты удаления сессии.

1. Добавить internal session lifecycle state в `GenerationService`: флаг stopping/deleting, число start leases и список running jobs. Любой публичный метод, создающий run, получает lease до первого await и освобождает его только после регистрации всех generation tasks. Начало stop атомарно ставит флаг и ждёт leases; после флага новый start получает `InvalidOperationException` до записи run/provider call.
2. Падающий race-тест: заблокировать `AddRunAsync`, начать `StartRunAsync`, затем deletion stop; stop не возвращается до регистрации/отмены tasks. Второй `StartRunAsync` после stop-флага не пишет run и не вызывает provider.
3. Добавить падающий тест: две сессии с заблокированными provider calls; остановка первой отменяет только её token и ожидает завершения её задач.
4. Заменить значение `_running` на небольшой внутренний объект с `SessionId`, `Task`, `CancellationTokenSource` и queued handle при наличии. Передавать token через normal/queued paths до provider, poll delay, чтения references, сохранения файла и статуса. CTS всегда dispose.
5. Добавить интерфейс `ISessionGenerationCoordinator` с одним `StopSessionAsync`; `GenerationService` его реализует. Зарегистрировать interface как ссылку на singleton `GenerationService`, не создавать второй instance.
6. `StopSessionAsync`: поставить barrier, дождаться start leases, отменить работы сессии, best-effort вызвать remote cancel для валидного queued handle и дождаться tasks. Работы других сессий не трогать. Повторный stop безопасен.
7. Внедрить `ISessionGenerationCoordinator` в `SessionService`; первым шагом `DeleteAsync` вызвать stop, затем удалить БД и файлы. Unit-тест порядка использует substitute интерфейса.
8. Добавить data/integration тест: provider освобождается после начала удаления; после завершения удаления каталог отсутствует и не появляется снова.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Services/ISessionGenerationCoordinator.cs` (новый), `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core/Services/SessionService.cs`, `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`, `ImageGen/ImageGen.Core.Tests/Services/SessionServiceTests.cs`, `ImageGen/ImageGen.Data.Tests/` (новый интеграционный тест или существующий подходящий файл)

## Отчёт

## Комментарии
