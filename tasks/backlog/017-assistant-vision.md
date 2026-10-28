---
id: 017
status: approved
assignee:
claimed:
created: 2026-10-27
---

# Ассистент видит картинки последнего прогона

## Описание
Если модель ассистента принимает картинки на входе, к запросу ассистенту добавляются готовые картинки последнего прогона (не больше 4, как data URL), чтобы он мог учитывать результат при правках (S2.4 в `docs/specs/chat-sessions.md`). Для скелета необязательно (решение владельца).

Детализировать (точные файлы, код, тесты, как в задачах v0.1) после задачи 016, когда будет код скелета.

Зависит от: 036 (общий интерфейс чата `IAssistantChatClient`).
Среда: Linux или Windows (только ядро и хранилище).

## Критерии готовности
- Картинки уходят ассистенту только у моделей с картинками на входе; у остальных запрос не меняется.
- Тест построения запроса с картинками на подменённом клиенте.

## Анализ
Проверено 2026-10-28 по `origin/main`. Задачи 012–015 в `in-progress`, 016 в `planned`: правило «детализировать после 016» пока не выполнено. План опирается на текущий код ядра; MAUI не меняется.

Среда: строки «Среда» в описании нет. Задача трогает только `ImageGen.Core` и `ImageGen.Data`, выполнима на Linux и Windows.

Что уже есть:
- `AssistantService.BuildRequestAsync`: последний прогон сессии (`runs.ListRunsBySessionAsync(...).LastOrDefault()`), системный промпт с текущим промптом, история `TakeLast(20)` без ошибок, инструмент `generate_images`.
- `OpenRouterChatMessage.ImageDataUrls` и `OpenRouterClient.BuildMessage` уже формируют `image_url` для роли user. Поле пока никто не заполняет.
- `OpenRouterChatResult.CostUsd` и `OpenRouterClient.ReadCost` есть (понадобятся 019 и 027).
- `Generation` хранит `ImagePath` (относительный путь), `MediaType`, `Status`, `SortOrder`, `RunId`.
- Модель ассистента по умолчанию `google/gemma-4-31b-it:free`. По `GET /api/v1/models` на 2026-10-28: `architecture.input_modalities` = image, text, video; `context_length` = 262144; цена 0; в `supported_parameters` есть `tools`.

Чего не хватает:
- Чтения файла в ядре. `IImageFileStore` умеет `SaveAsync`, `GetFullPath`, `DeleteSessionFilesAsync`, но не отдаёт байты. Нужен метод чтения в интерфейсе и в `FileImageStore` (`ImageGen.Data`).
- Признака «модель ассистента принимает картинки». `ImageModel.SupportsImageInput` берётся из `GET /api/v1/images/models` и относится к моделям картинок, не к чат-моделям. Каталога чат-моделей в коде нет, `GET /api/v1/models` в `docs/architecture.md` не перечислен. Каталог нужен и 019, и 027: здесь вводится минимально (в памяти, без таблицы).
- Правила выбора картинок прогона. В прогоне `моделей × N` генераций, картинок не больше 4, а какие именно — не определено.
- Помощника для data URL (`data:<media type>;base64,...`).

Наблюдение о флагах моделей (важно для 018 и 019):
- `SupportsImageInput` по `input_modalities` у моделей картинок: из 61 модели (`GET /api/v1/images/models`, 2026-10-28) 60 имеют `image` на входе, исключение `recraft/recraft-v4.1-flash`. Флаг почти всегда истинен и не отличает модели с референсами.
- Референсы описывает `supported_parameters.input_references` с `max`. Параметр есть у 59 моделей, у всех `max > 0` (у `black-forest-labs/flux.2-klein-4b` диапазон 0–4).
- В коде `ImageModel.MaxImagesPerRequest` берётся из `supported_parameters.n.max` и лимитом референсов не является. Это касается 018.

Конфликты и порядок:
- 017 и 018 оба меняют `AssistantService.BuildRequestAsync` и добавляют чтение файлов и data URL. Делать 017 первой и выносить общие помощники (`ImageDataUrl`, `IImageFileStore.ReadAsync`) в неё, чтобы 018 их переиспользовала.
- 017 и 027 меняют тот же метод (выбор истории `TakeLast(20)`). 027 делать после 017.
- 017 и 019 конфликтов по файлам не имеют.

## План
Цель: если модель ассистента принимает картинки, до 4 успешных картинок последнего прогона уходят в запрос как data URL. Иначе запрос строится как сейчас.

Типы и ответственность:
- `IImageFileStore.ReadAsync(relativePath, ct) → byte[]` (новый член). `FileImageStore` читает файл по `GetFullPath`. Отсутствующий файл даёт `FileNotFoundException`; решение о пропуске принимает вызывающий.
- `Services/ImageDataUrl` (статический помощник): `From(byte[] content, string mediaType) → string` даёт `data:<mediaType>;base64,<base64>`. Пустой или пробельный media type — `ArgumentException`.
- `Services/RunImageSelector` (статический, чистая функция): вход — генерации одного прогона; выход — не больше `AssistantPrompts.MaxRunImages` (= 4) генераций со `Status = Succeeded` и непустым `ImagePath`, по возрастанию `SortOrder`.
- `Domain/Models/ChatModel` (новый): `Id`, `Name`, `SupportsImageInput`, `ContextLength` (int?), `FetchedAt`. Поля для 027 (цена, инструменты) пока не добавляются.
- `OpenRouter/IOpenRouterChatClient`: новый метод `ListChatModelsAsync(ct) → IReadOnlyList<ChatModel>`. `OpenRouterClient` вызывает `GET models`, берёт `architecture.input_modalities` и `context_length`, пропускает записи без `id`. Ошибки — `ProviderException`, как у остальных методов.
- `Services/ChatModelCatalogService`: `IsImageInputAsync(modelId, ct) → bool`. Кеш в памяти процесса, первая загрузка по требованию. Модели нет в каталоге → `false`.
- `AssistantPrompts.MaxRunImages = 4`. Текст системного промпта не меняется (он дословный, см. задачу 010); картинки просто прикреплены к сообщению.

Псевдокод `AssistantService.BuildRequestAsync` (добавляется к текущему поведению):
```
lastRun = последний прогон сессии (как сейчас)
imageUrls = []
если lastRun не null:
    узнать, принимает ли модель ассистента картинки:
        при ошибке каталога: false, предупреждение в журнал
    если да:
        generations = генерации lastRun
        chosen = RunImageSelector.Select(generations)
        для каждой chosen:
            попытаться files.ReadAsync(ImagePath):
                при ошибке (файла нет): пропустить, предупреждение в журнал
                иначе imageUrls += ImageDataUrl.From(bytes, MediaType)
собрать запрос как сейчас: system, текущий промпт, история TakeLast(20)
если imageUrls не пуст:
    прикрепить imageUrls к последнему сообщению роли user (текущему сообщению пользователя)
```

Потоки данных:
- Запрос ассистенту: `AssistantService` → `ChatModelCatalogService` (признак) → `IRunRepository` (генерации последнего прогона) → `IImageFileStore.ReadAsync` → `OpenRouterClient.CompleteChatAsync` (уже формирует `image_url`).
- Картинки не сохраняются ни в БД, ни в чате: они читаются из файлов на каждый запрос.

Крайние случаи:
- Каталог чат-моделей не загрузился: картинки не отправляются, текстовый ответ важнее. Сообщение ассистента не помечается ошибкой.
- Последний прогон без успешных картинок: картинок нет, в предыдущих прогонах не ищем.
- Файл картинки пропал с диска: эта картинка пропускается, остальные отправляются.
- Сессия без прогонов: поведение как сейчас.
- Ошибка провайдера на запросе с картинками (например, 400 из-за размера): ошибка ассистента, как любая `ProviderException`.

Тест-кейсы (условие → результат):
- Модель с картинками; в последнем прогоне 2 успешные генерации (SortOrder 0 и 1) → последнее сообщение user в запросе содержит 2 data URL в порядке SortOrder, префикс `data:image/png;base64,`.
- Модель без картинок на входе → запрос такой же, как до изменения (без `ImageDataUrls`); `ReadAsync` не вызывался.
- В прогоне 6 успешных генераций → в запросе 4 первые по SortOrder.
- В прогоне есть Failed, Filtered, Queued → эти генерации в запрос не попадают.
- Сессия без прогонов → запрос без картинок, текст как прежде.
- Все генерации последнего прогона неуспешны → запрос без картинок.
- Один файл отсутствует, два есть → в запросе две картинки, ответ ассистента сохранён без `IsError`.
- Каталог чат-моделей бросает `ProviderException` → запрос без картинок, сообщение ассистента сохранено без `IsError`.
- `MediaType` = `image/jpeg` → префикс `data:image/jpeg;base64,`.
- `RunImageSelector`: пустой список → пустой результат; успешная генерация без `ImagePath` пропускается.
- `ChatModelCatalogService`: модель с `image` на входе → `true`; без `image` → `false`; неизвестная модель → `false`.
- `OpenRouterClient.ListChatModelsAsync` (тест с `StubHttpMessageHandler`): разбирает `architecture.input_modalities` и `context_length`; запись без `id` пропускается.
- `FileImageStore.ReadAsync`: сохранённый файл читается байт в байт; отсутствующий файл → `FileNotFoundException`.

Команды проверки:
- `dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj`
- `dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj`
- `dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes` (и для `ImageGen.Core.Tests`, `ImageGen.Data`, `ImageGen.Data.Tests`)
- `scripts/check.sh`
- Живой запрос с ключом не нужен: проверка на подменах. Ручная проверка — владелец или задача 016.

Файлы: `ImageGen/ImageGen.Core/Storage/IImageFileStore.cs`, `ImageGen/ImageGen.Data/FileImageStore.cs`, `ImageGen/ImageGen.Data.Tests/FileImageStoreTests.cs`, `ImageGen/ImageGen.Core/Services/ImageDataUrl.cs` (новый), `ImageGen/ImageGen.Core/Services/RunImageSelector.cs` (новый), `ImageGen/ImageGen.Core/Domain/Models/ChatModel.cs` (новый), `ImageGen/ImageGen.Core/OpenRouter/IOpenRouterChatClient.cs`, `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs`, `ImageGen/ImageGen.Core/Services/ChatModelCatalogService.cs` (новый), `ImageGen/ImageGen.Core/Services/AssistantService.cs`, `ImageGen/ImageGen.Core/Services/AssistantPrompts.cs`, `ImageGen/ImageGen.Core.Tests/Services/AssistantServiceTests.cs`, `ImageGen/ImageGen.Core.Tests/Services/RunImageSelectorTests.cs` (новый), `ImageGen/ImageGen.Core.Tests/Services/ChatModelCatalogServiceTests.cs` (новый), `ImageGen/ImageGen.Core.Tests/OpenRouter/OpenRouterClientTests.cs`, `docs/architecture.md`

## Отчёт

## Комментарии

### 2026-10-28, claude-qnrqyt
Открытые вопросы (план предложен, при ответе поправлю «План»):
1. Признак «модель ассистента принимает картинки» берётся из `GET /api/v1/models` (`architecture.input_modalities`). Этого эндпоинта нет в `docs/architecture.md`. Согласны добавить его в скелет? Нужен ли ADR?
2. Если в прогоне больше 4 успешных картинок: первые 4 по `SortOrder` (предложено) или другое правило?
3. Прикреплять картинки к последнему сообщению пользователя (предложено) или отдельным сообщением user?
4. Размер запроса не измерен: сколько весят 4 картинки из генерации, неизвестно. Достаточно лимита по количеству или нужно уменьшать картинки? Уменьшение требует библиотеку изображений и ADR.
5. Каталог чат-моделей на этом этапе в памяти (предложено). Хранение в SQLite (миграция, как у `image_models`) оставить для 027?
6. Нужна ли подсказка ассистенту в тексте (что картинки — результат последнего прогона)? Предложено не менять системный промпт.

### 2026-10-28, ревью (claude, Windows)
Общий порядок по пересечениям файлов и таблиц (предложение ревью, решает владелец): 035 → 036 → 020 → 017 → 018 → 019 → 027; 025 и 026 после 036 и принятия ADR 0003. Причины: 036 меняет `ImageModel`, `image_models` (ключ `(provider_id, id)`), `SessionSettings.ImageModelIds` (`ModelRef`) и `GenerationService`; 020 вводит полный список параметров модели, из которого 018 берёт лимит референсов; 017 вводит каталог чат-моделей, `IImageFileStore.ReadAsync` и `ImageDataUrl` для 018, 019 и 027. Номера миграций в планах заменены на `NNNN_<смысл>.sql` (правило в `docs/tasks.md`), строка «Среда» добавлена в «Описание».

### 2026-10-28, владелец (ответы в чате, записал ревью)
Порядок выполнения (решение владельца): 035 → 036 → 020 → 017 → 018 → 019 → 027, затем 025 и 026. План одобрен владельцем сейчас, не дожидаясь 016 (статус `approved`); начинать в этом порядке, после мерджа предыдущей задачи сверить план с кодом.
Ответы на вопросы 1–6: принять всё предложенное. `GET /api/v1/models` добавляется в `docs/architecture.md` («Внешние системы») без отдельного ADR; не больше 4 картинок, первые по `SortOrder`; картинки прикрепляются к последнему сообщению пользователя; без уменьшения картинок; каталог чат-моделей в памяти (SQLite — в 027); системный промпт не меняется.
Следствие решения по 036 (выполняется раньше): чат ассистента идёт через общий `Providers/IAssistantChatClient` вместо `IOpenRouterChatClient`. Всё, что этот план добавляет или использует в `IOpenRouterChatClient` (например, `ListChatModelsAsync`, `CompleteChatAsync`), относится к `IAssistantChatClient`; при начале работы сверить план с кодом после 036.
