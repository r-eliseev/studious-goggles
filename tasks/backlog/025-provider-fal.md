---
id: 025
status: approved
assignee:
claimed:
created: 2026-10-27
---

# Провайдер fal.ai и контракт ADR 0003

## Описание
Расширить `IImageGenerator` до контракта submit/watch/cancel (ADR 0003), реализовать fal: очередь, позиция в очереди, `has_nsfw_concepts` → «отфильтровано», восстановление генераций после перезапуска по сохранённому id запроса.

Детализировать (точные файлы, код, тесты, как в задачах v0.1) после задачи 016, когда будет код скелета.

Зависит от: 036 (общий слой провайдеров); ADR 0003 и 0005 приняты.
Среда: Linux или Windows (в MAUI меняется только модель карточки, она тестируется в MauiLinked; сборку MAUI проверяет CI на Windows).

## Критерии готовности
- Контракт ADR 0003 принят владельцем до начала работы.
- Тесты на подменённом HTTP.

## Анализ

### Решения, от которых зависит план
- Контракт: ADR 0003 (`docs/decisions/0003-provider-contract-and-jobs.md`) в статусе «предложено». Критерий «принят владельцем до начала работы» не выполнен. План написан по тексту ADR, реализация не начинается без его принятия.
- Общий слой (`ProviderKind`, `ProviderConfig`, `ModelRef`, `SecretNames.For`, миграция `provider_id`) — в задаче 036 (раздел «План» там). 025 идёт после 036.
- ADR 0003 описывает `IImageProvider` и заменяет узкий `IImageGenerator` (`ImageGen/ImageGen.Core/Providers/IImageGenerator.cs`). Текущий `IModelProvider` (ключ и каталог) сливается в него: так задумано в ADR.
- Задача 016 (ручная проверка скелета) не выполнена, см. 036.

### Что говорит документация fal (`https://fal.ai/docs/documentation/model-apis/inference/queue`, проверено 2026-10-28, дата на странице не указана)
- Отправка: `POST https://queue.fal.run/{endpoint}`, тело — входные параметры модели. Заголовки: `Authorization: Key <ключ>`, `Content-Type: application/json`.
- Ответ отправки: `request_id`, `status_url`, `response_url`, `cancel_url`, `queue_position`.
- Статус: `GET .../requests/{request_id}/status` (с `?logs=1` — логи). Значения: `IN_QUEUE` (с `queue_position`), `IN_PROGRESS` (с логами), `COMPLETED` (с `metrics.inference_time`, а при сбое с `error` и `error_type`).
- Результат: `GET .../requests/{request_id}` после завершения; схема зависит от модели.
- Отмена: `PUT .../requests/{request_id}/cancel`. `200 {"status":"OK"}` — снято из очереди; `202 {"status":"CANCELLATION_REQUESTED"}` — уже выполняется, может завершиться; `400 {"status":"ALREADY_COMPLETED"}`; `404 {"status":"NOT_FOUND"}`.
- Адреса статуса, результата и отмены используют только владельца и приложение (`fal-ai/flux`), полный путь модели даёт 405. Поэтому план берёт адреса из ответа отправки, а не собирает их сам.
- Веб-хук: параметр `fal_webhook`, не используем (нужен публичный адрес).
- Повторы: при 503, 504 и ошибках соединения fal сам ставит запрос в очередь повторно до 10 раз; `X-Fal-No-Retry: 1` отключает.
- Срок жизни результата и очереди на странице не указан: «неизвестно». Ссылки на медиа живут по настройке media expiration (заголовок `X-Fal-Object-Lifecycle-Preference`).

### Что не подтверждено документацией (нужно узнать до реализации)
- Поля результата конкретной модели: план ожидает `images[].url`, `seed`, `has_nsfw_concepts` (так говорит спецификация, раздел 2, таблица). В прочитанной документации это не написано: схема «зависит от модели». Нужен реальный ответ (пример от владельца или пробный запрос с ключом).
- Эндпоинт проверки ключа (F1.2) в прочитанной документации не найден.
- Коды HTTP и `error_type` fal в ошибках: таблицы не нашёл. План использует сопоставление как у OpenRouter с пометкой «предположение».
- Цена: в прочитанной документации цены нет, `CostUsd` для fal пока `null`. Цены и лимиты в задаче не указываются, пока нет официального источника с датой.

### Связь с текущим кодом
- `GenerationService.ExecuteAsync` держит слот `_slots` на всё время генерации. Для очереди fal (минуты) это лишает слота: план держит слот только на отправку.
- `RecoverInterruptedAsync` помечает всё незавершённое как `Interrupted`. По ADR после перезапуска генерации с сохранённым handle возобновляются, без handle остаются как есть.
- `IImageFileStore.SaveAsync` принимает `byte[]`. Скачивание результата идёт потоком (правило `docs/knowledge/csharp.md`), поэтому нужна перегрузка из потока.
- `GenerationStatus` без состояния «запрос на отмену отправлен», хотя ADR его требует.

## План

Общий слой (типы `ProviderKind`, `ProviderConfig`, `ModelRef`, `SecretNames.For`, миграция `provider_id`) задан в 036 и здесь не повторяется. 025 идёт после 036 и после принятия ADR 0003.

### Контракт (`ImageGen/ImageGen.Core/Providers/`, по ADR 0003)
- `IImageProvider`: `CheckKeyAsync(key)`, `ListModelsAsync()`, `DescribeModelAsync(modelId)`, `SubmitAsync(ProviderJobRequest)` → `ProviderJobHandle`, `PollAsync(handle)` → `JobUpdate` (одно чтение состояния; ADR 0005), `CancelAsync(handle)` → `CancelOutcome`. `IImageGenerator` и `IModelProvider` удаляются.
- `Providers/ProviderJobPoller` (общий для всех провайдеров, ADR 0005): `WatchAsync(provider, handle)` → `IAsyncEnumerable<JobUpdate>`. Цикл: `PollAsync` → отдать обновление при смене состояния → ждать интервал (растёт от минимума к максимуму из конфига провайдера) → повтор до конечного состояния. Сбой сети на опросе — повтор опроса, не отправки.
- `ProviderJobRequest` (record): `ModelId`, `Prompt`, `Seed` (null, если модель не принимает seed), `NumImages` всегда 1 (одна генерация = один запуск модели с одним сидом).
- `ProviderJobHandle` (record, сериализуемый): `ProviderId`, `Kind` (вариант на провайдера). Для fal: `RequestId`, `StatusUrl`, `ResponseUrl`, `CancelUrl` из ответа отправки. Хранится в `generations` (JSON), см. миграцию ниже.
- `JobUpdate`: состояние (`Queued` с позицией или `null`, `Running` с логами, `Succeeded` с `ImageDownload`, `Filtered`, `Failed` с `ErrorKind` и текстом, `Cancelled`). Состояния соответствуют единой модели из ADR 0003.
- `ImageDownload`: адрес для скачивания и тип медиа; байты не держит.
- `CancelOutcome`: `Cancelled`, `CancellationRequested`, `AlreadyCompleted`, `NotSupported`.
- `GenerationStatus` получает `CancelRequested` (запрос на отмену отправлен). Позиция в очереди не сохраняется в БД, только передаётся в событие.

### OpenRouter как задание в фоне (`ImageGen/ImageGen.Core/OpenRouter/`, ADR 0005)
- `OpenRouterJobRunner` (singleton, в памяти): `Start(request)` → локальный id; задание ждёт слот (семафор на 4, локальная позиция в очереди), затем в `Task.Run` выполняет `POST images` своим `CancellationTokenSource`. Состояния: в очереди → идёт → готово (байты, тип, цена) / ошибка / отфильтровано (403 с `metadata.reasons`) / отменено. Конечное задание удаляется после того, как опрос его выдал, или по сроку.
- `OpenRouterImageProvider : IImageProvider`: `SubmitAsync` вызывает `Start` и сразу возвращает handle (`ProviderId`, локальный id), не дожидаясь HTTP; `PollAsync` читает состояние из `OpenRouterJobRunner`, неизвестный id → ошибка `ErrorKind.Interrupted` (задание не пережило перезапуск); `CancelAsync` отменяет HTTP-запрос → `Cancelled`. Ключ, каталог и проверка ключа — из нынешнего `OpenRouterClient`.
- `ImageDownload` для OpenRouter несёт байты в памяти (у fal и Replicate — адрес для скачивания).

### fal (`ImageGen/ImageGen.Core/Fal/`, новые)
- `FalImageProvider : IImageProvider`: сопоставляет вызовы с `FalQueueClient` и разбирает ответы.
- `FalQueueClient`: HTTP-вызовы отправки, статуса, результата, отмены. Ключ берётся из `ISecretStore` на каждый запрос (`SecretNames.For("fal")`).
- `FalJson` (записи для `System.Text.Json`, добавляются в `CoreJsonContext`).

### Поток отправки и опроса
1. `GenerationService` вызывает `SubmitAsync` одинаково для всех провайдеров (у OpenRouter он тоже возвращается сразу, ADR 0005). Слоты: у fal и Replicate семафор провайдера держится только на отправку, у OpenRouter очередь и слоты внутри `OpenRouterJobRunner`.
2. Handle сохраняется в `generations` сразу. Без сохранения handle задание не продолжается.
3. `ProviderJobPoller.WatchAsync` вызывает `PollAsync` fal: статус по `StatusUrl` (`?logs=1`), интервал нарастает (вопрос 8). Сопоставление:
   - `IN_QUEUE` → `Queued(queue_position)`;
   - `IN_PROGRESS` → `Running(logs)`;
   - `COMPLETED` с `error` → `Failed(ErrorKind по error_type, текст без ключа)`;
   - `COMPLETED` без `error` → запрос `ResponseUrl`, разбор результата: `has_nsfw_concepts` для изображения `true` → `Filtered` без скачивания; иначе `Succeeded(ImageDownload)`.
4. `GenerationService` по `Succeeded` скачивает картинку потоком в `IImageFileStore`, и только потом сохраняет статус `Succeeded` и вызывает событие.

### Отмена
- `CancelAsync` вызывает `PUT CancelUrl`: `200` → `Cancelled`; `202` → `CancellationRequested` и продолжение опроса до финального статуса; `400 ALREADY_COMPLETED` → `AlreadyCompleted`, итог берётся из опроса; `404` → `Failed` с текстом.
- Сам поток отмены в сервисе (одно задание и группа) задача 024. 025 даёт провайдерный `CancelAsync` и маппинг исходов.

### Восстановление после перезапуска
- Генерации со статусом `Queued` или `Running` и сохранённым handle возобновляют опрос (`ProviderJobPoller`) с тем же handle. Новая отправка не происходит.
- У OpenRouter handle тоже сохранён, но задание жило в памяти: первый `PollAsync` после перезапуска возвращает ошибку `Interrupted`. Отдельной ветки для OpenRouter нет.
- Генерации без handle (созданные до этой задачи) помечаются `Interrupted`, как сейчас.
- Если `ResponseUrl` больше не отдаёт результат, генерация `Failed` с текстом провайдера (срок хранения неизвестен, вопрос 4).

### Миграция `NNNN_generation_jobs.sql` (номер — следующий свободный при реализации)
- `generations.job_json TEXT NULL` (сериализованный handle).
- `generations.cancel_requested_at TEXT NULL` (нужен для состояния `CancelRequested` после перезапуска).

### Крайние случаи
- Отправка вернула ошибку → генерация `Failed` без handle; опрос не начинается.
- Сеть оборвалась на опросе → повторяется только опрос, отправка не повторяется (платный запуск).
- Опрос идёт дольше порога → задача 036 не меняет; порог и поведение «подождать или отменить» (F5.6) вне 025.
- `has_nsfw_concepts` есть, но `false` → обычный успех.
- Запрос отмены пришёл, пока опрос ещё не получил `IN_QUEUE` → отмена всё равно уходит по `CancelUrl`.

### Тест-кейсы (условие → результат; HTTP подменяется `StubHttpMessageHandler`)
- Отправка → `POST https://queue.fal.run/<endpoint>` с заголовком `Authorization: Key <ключ>`; тело содержит `prompt`, при `Seed = null` поля `seed` нет.
- Ответ отправки → handle содержит три адреса из ответа; адреса не собираются из endpoint.
- Статус `IN_QUEUE` с `queue_position: 3` → `Queued(3)`.
- Статус `IN_PROGRESS` с логами → `Running` с логами.
- `COMPLETED` с `error` → `Failed` с текстом; текст не содержит ключа.
- `COMPLETED` без ошибки, `has_nsfw_concepts: [true]` → `Filtered`, скачивания не было.
- `COMPLETED` без ошибки, изображение есть → скачивание по URL, файл сохранён, статус `Succeeded`, `CostUsd = null`.
- Отмена: `200` → `Cancelled`; `202` → `CancellationRequested`, затем `COMPLETED` → итоговый статус из опроса; `400` → `AlreadyCompleted`; `404` → `Failed`.
- Сбой сети на опросе → опрос повторяется, новой отправки нет; после лимита повторов → `Failed` с `Network`.
- Ключ не задан → `InvalidKey` без HTTP-вызова.
- Перезапуск с `Running` и handle → опрос с тем же handle, `SubmitAsync` не вызван (NSubstitute).
- OpenRouter: `SubmitAsync` возвращается до ответа HTTP (подмена держит ответ, пока тест не отпустит); опрос даёт «в очереди» → «идёт» → «готово» с байтами; файл сохранён до события.
- OpenRouter: пятое задание при четырёх занятых слотах → «в очереди» с позицией 1.
- OpenRouter: `CancelAsync` во время HTTP → запрос отменён (токен подмены сработал), состояние «отменено».
- OpenRouter: 403 с `metadata.reasons` → «отфильтровано».
- OpenRouter: `PollAsync` с неизвестным локальным id → ошибка `Interrupted`.
- `ProviderJobPoller`: интервал растёт от минимума к максимуму; одинаковые состояния подряд не дают повторного события.
- Перезапуск с `Running` без handle → `Interrupted`, как раньше.
- Новая миграция на базе с данными: существующие строки `job_json = NULL`.

### Команды проверки
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Providers/IImageProvider.cs` (новый), `ImageGen/ImageGen.Core/Providers/ProviderJobPoller.cs` (новый), `ImageGen/ImageGen.Core/OpenRouter/OpenRouterJobRunner.cs` (новый), `ImageGen/ImageGen.Core/OpenRouter/OpenRouterImageProvider.cs` (новый), `ImageGen/ImageGen.Core.Tests/OpenRouter/OpenRouterImageProviderTests.cs` (новый), `ImageGen/ImageGen.Core.Tests/Providers/ProviderJobPollerTests.cs` (новый), `ImageGen/ImageGen.Core/Providers/IImageGenerator.cs` (удаляется), `ImageGen/ImageGen.Core/Providers/IModelProvider.cs` (удаляется), `ImageGen/ImageGen.Core/Providers/ProviderJobRequest.cs` (новый), `ImageGen/ImageGen.Core/Providers/ProviderJobHandle.cs` (новый), `ImageGen/ImageGen.Core/Providers/JobUpdate.cs` (новый), `ImageGen/ImageGen.Core/Providers/CancelOutcome.cs` (новый), `ImageGen/ImageGen.Core/Providers/ImageDownload.cs` (новый), `ImageGen/ImageGen.Core/Fal/FalImageProvider.cs` (новый), `ImageGen/ImageGen.Core/Fal/FalQueueClient.cs` (новый), `ImageGen/ImageGen.Core/Fal/FalJson.cs` (новый), `ImageGen/ImageGen.Core/Domain/Generations/GenerationStatus.cs`, `ImageGen/ImageGen.Core/Domain/Generations/Generation.cs`, `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs`, `ImageGen/ImageGen.Core/Services/ApiKeyService.cs`, `ImageGen/ImageGen.Core/Storage/IRunRepository.cs`, `ImageGen/ImageGen.Core/Storage/IImageFileStore.cs`, `ImageGen/ImageGen.Core/Serialization/CoreJsonContext.cs`, `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Data/Migrations/NNNN_generation_jobs.sql` (новый), `ImageGen/ImageGen.Data/Repositories/RunRepository.cs`, `ImageGen/ImageGen.Data/FileImageStore.cs`, `ImageGen/ImageGen.Core.Tests/Fal/FalImageProviderTests.cs` (новый), `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`, `ImageGen/ImageGen.Core.Tests/OpenRouter/OpenRouterClientTests.cs`, `ImageGen/ImageGen.Data.Tests/Repositories/RunRepositoryTests.cs`, `ImageGen/ImageGen.Data.Tests/FileImageStoreTests.cs`, `ImageGen/ImageGen.Maui/PageModels/GenerationItemViewModel.cs`, `docs/architecture.md` (раскладка провайдеров)

## Отчёт

## Комментарии

### 2026-10-28, claude-qnrqyt
План написан (статус `planned`), код не начат. Блокер: ADR 0003 в статусе «предложено» (критерий «контракт принят владельцем» не выполнен). Вопросы владельцу:

1. Принять ADR 0003 в текущем виде? В плане одно отклонение: `DescribeModelAsync` остаётся в интерфейсе по ADR, но для fal возвращает только id (схема параметров — F2.5, P1). Ок?
2. Поля результата fal (`images[].url`, `seed`, `has_nsfw_concepts`) в прочитанной документации не подтверждены: там сказано, что схема зависит от модели. Пришлите пример реального ответа модели, которую используем, или ключ fal для одного пробного запроса. Тогда сниму неизвестность в `poc/v0.1/025-provider-fal/`; без этого поля берутся из спецификации как предположение.
3. Эндпоинт проверки ключа fal (F1.2) не найден в прочитанной документации. Какой запрос считать проверкой? Без него `CheckKeyAsync` не готов.
4. Срок хранения очереди и результата fal не указан (неизвестно). План скачивает результат сразу после `COMPLETED`. Ок?
5. Цена: в прочитанной документации fal цены нет, `CostUsd = null`. Откуда брать: страница модели или поле ответа? Цены не указываю без официального источника с датой.
6. Один запрос = одна картинка (`num_images = 1`), как сейчас в ядре (модель × сид). Ок?
7. fal сам повторяет запрос при 503/504 до 10 раз (документация). Ставить ли `X-Fal-No-Retry: 1`? Это влияет на расход.
8. Опрос: план держит слот семафора только на отправку, опрос без слота. Нужен ли отдельный лимит одновременных опросов и какой интервал опроса (предлагаю от 1 с до 10 с)?
9. Коды HTTP и `error_type` fal → `ErrorKind`: таблицы в документации не нашёл. Предлагаю сопоставление как у OpenRouter с пометкой «предположение», уточнить по реальным ответам.

### 2026-10-28, ревью (claude, Windows)
Общий порядок по пересечениям файлов и таблиц (предложение ревью, решает владелец): 035 → 036 → 020 → 017 → 018 → 019 → 027; 025 и 026 после 036 и принятия ADR 0003. Причины: 036 меняет `ImageModel`, `image_models` (ключ `(provider_id, id)`), `SessionSettings.ImageModelIds` (`ModelRef`) и `GenerationService`; 020 вводит полный список параметров модели, из которого 018 берёт лимит референсов; 017 вводит каталог чат-моделей, `IImageFileStore.ReadAsync` и `ImageDataUrl` для 018, 019 и 027. Номера миграций в планах заменены на `NNNN_<смысл>.sql` (правило в `docs/tasks.md`), строка «Среда» добавлена в «Описание».

### 2026-10-28, владелец (через ревью)
ADR 0003 принят. Уточнение в ADR 0005: OpenRouter тоже работает как задание в фоне (`SubmitAsync` возвращается сразу, HTTP в отдельной задаче), статус всех провайдеров читается общим опросом (`PollAsync` + `ProviderJobPoller`). План поправлен: раздел «OpenRouter как задание в фоне», поток, восстановление, тест-кейсы, файлы. Вопрос 1 закрыт.

### 2026-10-28, владелец (ответы в чате, записал ревью)
Порядок выполнения (решение владельца): 035 → 036 → 020 → 017 → 018 → 019 → 027, затем 025 и 026. План одобрен владельцем сейчас, не дожидаясь 016 (статус `approved`); начинать в этом порядке, после мерджа предыдущей задачи сверить план с кодом.
Ответы: ADR 0003 принят (уточнение ADR 0005). Поля результата, проверка ключа и коды ошибок fal — по допущению из спецификации и документации, живая проверка вручную после реализации. Один запрос = одна картинка; результат скачивается сразу после `COMPLETED`; `X-Fal-No-Retry` не ставим (fal повторяет сам); опрос от 1 до 10 с без отдельного лимита одновременных опросов; цена fal — «неизвестно», пока нет официального источника; ошибки fal сопоставляются как у OpenRouter (пометка «предположение»).
