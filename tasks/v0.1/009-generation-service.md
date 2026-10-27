---
id: 009
status: done
assignee:
claimed:
created: 2026-10-27
---

# Ядро: прогоны и генерации

## Описание
`GenerationService` создаёт прогон на моделях сессии (N общих сидов на все модели), выполняет генерации параллельно в фоне (не больше 4 одновременно), сохраняет картинку в файл и запись в БД до события `GenerationUpdated` и отмечает отфильтрованные модерацией. Действия над генерацией: Repeat (тот же промпт, модель, сид), New seed (новый сид), Edit (новый промпт, та же модель и сид). При запуске приложения незавершённые генерации помечаются «Interrupted».

Зависит от: 006, 007.
Среда: Linux или Windows (нужен только .NET 10 SDK, MAUI-нагрузки не нужны).

## Критерии готовности
- `GenerationService`, `ISeedSource`, `RandomSeedSource`, `GenerationUpdatedEventArgs` реализованы с поведением из плана.
- Тест-кейсы из плана написаны и проходят вместе с остальными тестами `ImageGen.Core.Tests`.
- `dotnet format` для `ImageGen.Core` и `ImageGen.Core.Tests` проходит.

## Анализ
- Спецификация: S3.1–S3.8, S4.1–S4.4 (`docs/specs/chat-sessions.md`).
- Картинки генерирует `IImageGenerator`. Его реализует `OpenRouterClient` (через `IModelProvider : IImageGenerator`), отдельный адаптер не нужен.
- Сиды от 0 до 2147483647 (`int.MaxValue`): этот диапазон принимают все модели с seed.
- Сид не отправляется модели, у которой в каталоге нет параметра `seed` (`SeedApplied = false`); неизвестная каталогу модель тоже считается без seed.
- Фоновые генерации запускаются через `Task.Run` и отслеживаются в словаре; `WhenIdleAsync` нужен тестам, чтобы дождаться окончания.
- Событие приходит в фоновом потоке и несёт копию генерации; MAUI переносит обновление в UI-поток (задача 014).
- Пробная реализация: `poc/v0.1/009-generation-service/` (подсказка, не копировать дословно, см. `poc/README.md`).

## План
1. `Services/ISeedSource.cs`: `long NextSeed()` — сид от 0 до 2147483647 включительно. `Services/RandomSeedSource.cs`: реализация через `Random.Shared`, константа `MaxSeed = int.MaxValue`.
2. `Services/GenerationUpdatedEventArgs.cs`: `EventArgs` со свойством `Generation` (снимок после сохранения).
3. `Services/GenerationService.cs`, `sealed`, регистрируется singleton. Зависимости: `ISessionRepository`, `IRunRepository`, `IImageGenerator`, `IImageFileStore`, `ModelCatalogService`, `ISeedSource`, `TimeProvider`, `ILogger<GenerationService>`.
   - Константа `MaxParallelGenerations = 4`; семафор на 4; словарь «id генерации → фоновая задача».
   - Событие `GenerationUpdated` (фоновый поток).
   - Публичные методы и составление пар «модель × сид»:

     | Метод | `RunOrigin` | Промпт | Пары |
     |---|---|---|---|
     | `StartRunAsync(sessionId, prompt, ct)` | `Assistant` | `prompt.Trim()` | для каждой модели из `Settings.ImageModelIds` × N общих сидов (`N = Settings.RunsPerModel`, сиды берутся один раз на прогон) |
     | `RepeatAsync(generationId, ct)` | `Repeat` | как у исходной | (модель, сид) исходной |
     | `NewSeedAsync(generationId, ct)` | `NewSeed` | как у исходной | (модель исходной, `NextSeed()`) |
     | `EditAsync(generationId, newPrompt, ct)` | `Edit` | `newPrompt.Trim()` | (модель, сид) исходной |

     Ошибки: сессии нет или у неё пустой список моделей → `InvalidOperationException`; исходной генерации нет → `InvalidOperationException`; пустой промпт → `ArgumentException`.
   - `RecoverInterruptedAsync(ct)` → `runs.MarkUnfinishedAsInterruptedAsync(now)`, возвращает число записей.
   - `WhenIdleAsync()` → ждёт все задачи из словаря.
   - Общий шаг создания прогона:
     ```
     run = новый Run(sessionId, prompt, origin, CreatedAt = now)
     для каждой пары (index, model, seed):
         modelInfo = catalog.FindAsync(model)
         generation = Queued, SeedApplied = modelInfo?.SupportsSeed ?? false, SortOrder = index
     runs.AddRunAsync(run, generations)        // одна транзакция
     для каждой генерации: запустить ExecuteAsync в Task.Run, положить в словарь, убрать из словаря по завершении
     вернуть run (не дожидаясь генераций)
     ```
   - Жизнь одной генерации (`ExecuteAsync`), машина состояний:
     ```
     Queued --(семафор взят)--> Running --(картинка получена и файл записан)--> Succeeded
                                        --(ProviderException с Kind = Moderated)--> Filtered
                                        --(другая ProviderException или любое исключение)--> Failed
     Queued/Running --(приложение закрыли)--> Interrupted (при следующем запуске, RecoverInterruptedAsync)
     ```
     На каждом переходе: сначала `runs.UpdateGenerationAsync`, потом событие с копией генерации. В `Succeeded` записать `ImagePath` (от `IImageFileStore.SaveAsync`), `MediaType`, `CostUsd`. В `Filtered`/`Failed` — `ErrorKind` и `ErrorMessage` (для не-`ProviderException` — `ErrorKind.Unknown` и запись в лог). В конечном состоянии `CompletedAt = now`. Сид передаётся генератору только при `SeedApplied`. Ошибка сохранения логируется и не роняет процесс; семафор отпускается в `finally`.
4. Тесты `ImageGen.Core.Tests/Services/GenerationServiceTests.cs` (NSubstitute для репозиториев, генератора, файлов и сидов; каталог — настоящий `ModelCatalogService` над подменённым `IImageModelRepository` с моделями `seeded` (seed есть) и `unseeded` (нет), модели создаются через `ModelCatalogServiceTests.Model`; сессия с этими двумя моделями и `RunsPerModel = 2`; сиды 11, 22, 33):
   - Две модели × 2 прогона → 4 генерации в порядке (seeded 11), (seeded 22), (unseeded 11), (unseeded 22), `SortOrder` 0..3; промпт обрезан; `Origin = Assistant`; `SeedApplied` true только у `seeded`.
   - Успех → у всех генераций `Succeeded`, путь, тип, цена и `CompletedAt`; событий `Running` и `Succeeded` по 4.
   - Генератор получает сид только для `seeded` (2 вызова с сидом, 2 без).
   - `ProviderException(Moderated)` → все `Filtered` с `ErrorKind.Moderated`.
   - Одна модель падает с `RateLimited` → её генерации `Failed` с текстом ошибки, у другой `Succeeded`.
   - Пустой список моделей → `InvalidOperationException`.
   - Repeat → один прогон `Repeat` с тем же промптом, моделью и сидом; New seed → сид из `ISeedSource`; Edit → новый (обрезанный) промпт, та же модель и сид.
   - `RecoverInterruptedAsync` передаёт текущее время в репозиторий и возвращает его результат.

Файлы: `ImageGen/ImageGen.Core/Services/ISeedSource.cs`, `ImageGen/ImageGen.Core/Services/RandomSeedSource.cs`, `ImageGen/ImageGen.Core/Services/GenerationUpdatedEventArgs.cs`, `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`

### Проверка
```
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
scripts/check.sh
```

## Отчёт

- Реализованы `GenerationService`, `ISeedSource`, `RandomSeedSource` (`MaxSeed = int.MaxValue`) и `GenerationUpdatedEventArgs` в `ImageGen.Core/Services/` по плану.
- Отклонения от плана: клиент назван `IModelProvider`, в сервисе используется `IImageGenerator` (интерфейс из плана `IOpenRouterClient` и `OpenRouterImageRequest` не применялись: в репозитории их нет).
- Регистрация `GenerationService` как singleton в `AddImageGenCore()` не сделана: метод пустой, регистрация всех сервисов относится к задаче 011.
- `docs/architecture.md` не менялся: раздела «Состояние кода» больше нет (правило из `docs/log/2026-10-27-architecture-without-progress.md`), описанное устройство совпадает с кодом.
- Фоновые генерации: `Task.Run` с ожиданием шлюза, чтобы задача не удалила свою запись раньше её добавления; семафор на 4 слота; `CancellationToken.None` для фона.
- Если сохранение записи в БД упало, генерация не отправляет событие и ошибка пишется в лог; ошибки неизвестного типа дают `ErrorKind.Unknown`, текст ошибки берётся из `Exception.Message`.
- Тесты: `ImageGen.Core.Tests/Services/GenerationServiceTests.cs`, 14 тестов (плюс проверки пустого промпта, неизвестной сессии и генерации). Сумма `ImageGen.Core.Tests`: 73 теста, все проходят, три прогона подряд без сбоев.
- `dotnet format --verify-no-changes` для `ImageGen.Core` и `ImageGen.Core.Tests` проходит; `scripts/check.sh` проходит.
- Среда: Linux, .NET SDK 10.0.403, сеть в тестах не используется.

## Комментарии
