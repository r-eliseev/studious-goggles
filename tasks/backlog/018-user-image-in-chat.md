---
id: 018
status: approved
assignee:
claimed:
created: 2026-10-27
---

# Своя картинка в чате

## Описание
Кнопка «прикрепить картинку» в чате: картинка уходит ассистенту и может стать референсом генерации (Img2Img, F4.5). Не в первой версии (решение владельца).

Детализировать (точные файлы, код, тесты, как в задачах v0.1) после задачи 016, когда будет код скелета.

Среда: только Windows (есть MAUI-часть: выбор файла и экран чата).

## Критерии готовности
- Модели без поддержки референсов помечаются и пропускаются с причиной.
- Прикреплённые картинки (до 4 на сообщение, до 10 МБ каждая, PNG, JPEG, WebP) уходят ассистенту вместе с сообщением.
- Картинки сообщения становятся референсами прогона, который запустил ассистент; это видно на карточке генерации.
- Перед первой отправкой картинки показывается предупреждение, что картинки уходят провайдеру (F10.6).

## Анализ
Проверено 2026-10-28 по `origin/main`. Задачи 012–016 пока не закрыты (см. 017): MAUI-часть плана может поменяться после их слияния.

Среда: строки «Среда» нет, значит MAUI-часть только Windows (`docs/tasks.md`). Ядро и Data выполнимы на Linux.

Что уже есть:
- `ChatMessage` без вложений. `ChatPageModel.SendAsync` передаёт в ассистента только `InputText`.
- `AssistantService.SendUserMessageAsync` требует непустой текст (`ArgumentException.ThrowIfNullOrWhiteSpace`): сообщение только с картинкой сейчас невозможно.
- `OpenRouterClient.BuildMessage` умеет `image_url` (нужно для ассистента, см. 017).
- `ImageGenerationRequest(ModelId, Prompt, Seed)` без референсов; `OpenRouterClient.GenerateAsync` шлёт `model`, `prompt`, `n`, `seed`.
- `ImageModel.SupportsImageInput` и `MaxImagesPerRequest`. Лимита референсов в модели нет: `MaxImagesPerRequest` берётся из `n.max`.
- `FileImageStore.SaveAsync` привязан к `generationId`; `DeleteSessionFilesAsync` удаляет всю папку `images/<sessionId>`, поэтому вложения сессии удалятся вместе с ней.
- Каталог `GET /api/v1/images/models` (2026-10-28): у 59 из 61 моделей параметр `input_references` с `max > 0` (у FLUX.2 Klein 0–4). Признак `image` во входе у 60 из 61 и для референсов не годится.

Чего не хватает:
- Хранения вложений: таблицы, репозитория, миграции (сейчас только `0001_initial.sql`).
- Сохранения своих картинок: метод файлового хранилища для вложения (путь не привязан к генерации) и чтения по id вложения.
- Выбора файла в MAUI (Windows и Android): нет сервиса выбора файла, нет превью вложения в поле ввода.
- Лимита референсов в `ImageModel` (поле `MaxReferenceImages` из `supported_parameters.input_references.max`) и сброса кеша `image_models` при миграции: иначе после неё все модели покажутся без референсов до обновления каталога.
- Референсов в `ImageGenerationRequest` и в теле `POST /api/v1/images`. Формат `input_references` (ссылка или data URL, массив) взят из `docs/specs/image-client.md`; точный JSON не проверен.
- Связи генерации с вложением: `Repeat`, `NewSeed`, `Edit` (S4) должны повторять тот же референс, поэтому генерация хранит id вложения.
- Пропуска моделей с причиной: `GenerationService.StartRunAsync` сейчас создаёт генерации для всех моделей сессии.

Критерии готовности: в них только пропуск моделей без референсов. Описание также требует, чтобы картинка ушла ассистенту и стала референсом генерации. Критерии не переписываю (правило задачи), вопрос в комментариях.

Конфликты и порядок:
- С 017: оба меняют `AssistantService.BuildRequestAsync` и добавляют чтение файлов и `ImageDataUrl`. Сначала 017, 018 переиспользует её помощники.
- С 019: обе добавляют миграцию (номер — следующий свободный при реализации, `docs/tasks.md`) и меняют один и тот же компонент ввода картинки в `ChatPage`.
- С 020 (параметры генерации): оба меняют `ImageGenerationRequest` и `OpenRouterClient.GenerateAsync`. Порядок слияния согласовать.

## План
Цель: картинка, прикреплённая к сообщению, уходит ассистенту (через 017) и может стать референсом генерации. Модели без референсов в прогоне пропускаются с причиной.

Типы и ответственность:
- `Domain/Chat/ChatAttachment` (новый): `Id`, `SessionId`, `MessageId` (nullable до отправки), `RelativePath`, `MediaType`, `CreatedAt`.
- `Storage/IChatAttachmentRepository`: `AddAsync`, `GetAsync(id)`, `ListByMessageAsync(messageId)`.
- `Storage/IImageFileStore` (дополнение): `SaveAttachmentAsync(sessionId, attachmentId, bytes, mediaType) → relativePath`, путь `images/<sessionId>/attachments/<attachmentId>.<ext>`. Чтение `ReadAsync` — из 017.
- `Services/AttachmentService`: `AddImageAsync(sessionId, bytes, mediaType)`. Проверяет тип (PNG, JPEG, WebP), пустоту и лимит размера (лимит — вопрос), пишет файл, затем строку в БД (порядок ADR 0002). `ReadDataUrlAsync(attachmentId)` для ассистента и генерации.
- `Domain/Models/ImageModel`: новое поле `MaxReferenceImages` (int, 0 если параметра нет). `OpenRouterClient.ListImageModelsAsync` читает `supported_parameters.input_references.max`.
- Миграция `NNNN_chat_attachments.sql` (номер — следующий свободный при реализации): колонка `image_models.max_reference_images` (по умолчанию 0), `DELETE FROM image_models` (кеш пересоберётся при следующем обращении), колонка `generations.reference_attachment_id` (NULL), таблица `chat_attachments`, индекс по `message_id`.
- `Providers/ImageGenerationRequest`: новое поле `ReferenceImageDataUrls` (список, по умолчанию пустой). `OpenRouterClient.GenerateAsync` при непустом списке добавляет `input_references` (формат — вопрос).
- `Services/GenerationService.StartRunAsync(sessionId, prompt, attachments)` (список, может быть пустым; решение владельца: несколько картинок, до 4): каждой модели уходит не больше `MaxReferenceImages` первых вложений, лишние не отправляются и называются в сообщении о прогоне. Для каждой модели сессии, если вложения есть и `MaxReferenceImages == 0`, модель пропускается с кодом причины `NoReferenceSupport`. Остальные модели получают генерации с `reference_attachment_id`. Результат: `StartRunResult(Run?, IReadOnlyList<SkippedModel>)`; `SkippedModel(ModelId, Reason)`. Если все модели пропущены, прогон не создаётся.
- `Domain/Generations/Generation`: поле `ReferenceAttachmentIds` (список Guid, пустой по умолчанию); в БД — колонка `generations.reference_attachment_ids_json` вместо одной `reference_attachment_id`.
- `Services/AssistantService.SendUserMessageAsync(sessionId, text, attachmentIds)`: текст может быть пустым, если есть вложения (до 4; больше — `ArgumentException`). Сообщение пользователя хранит вложение. Запрос ассистенту — как в 017 (картинка прикрепляется к сообщению).
- MAUI: `Services/IFilePickerService` (`PickImageAsync → (bytes, mediaType)?`) и реализация через MAUI Essentials; `PageModels/ChatPageModel`: команды `AttachImage` и `RemoveAttachment`, состояние `PendingAttachment`; `SendCommand` доступна при тексте или вложении. `PageModels/ChatItemTemplateChoice`: строка сообщения с миниатюрой. `PageModels/GenerationItemViewModel`: пометка «референс» на карточке. `Resources/Strings/AppResources.resx`: тексты кнопок, причины пропуска (ядро отдаёт код, текст в оболочке).

Потоки данных:
- Прикрепление: UI выбирает файл → `AttachmentService.AddImageAsync` → файл → строка `chat_attachments` → id в `PendingAttachment`.
- Отправка: `ChatPageModel.SendAsync` → `AssistantService.SendUserMessageAsync(text, attachmentId)` → сообщение user с вложением → ассистент (картинка в запросе, см. 017) → если вызван `generate_images`, `StartRunAsync(prompt, attachmentId)`.
- Генерация: `GenerationService` → `AttachmentService.ReadDataUrlAsync` → `ImageGenerationRequest.ReferenceImageDataUrls` → `OpenRouterClient.GenerateAsync`.

Решение по автоматике: картинка сообщения становится референсом прогона, который запустил ассистент (предложено, видно на карточке и в сообщении, F9.4). Альтернатива — отдельная кнопка «использовать как референс» (вопрос).

Крайние случаи:
- Файл не картинка или тип не поддерживается: отказ до записи, текст и вложение остаются в поле ввода, сообщение не уходит.
- Все модели сессии без референсов: прогон не создаётся, в ответе ассистента причина пропуска (`IsError` — предложено `true`, т.к. прогон не запущен).
- Часть моделей без референсов: одна генерация пропущена, другие создаются; причина видна в сообщении о прогоне.
- Файл вложения пропал с диска к моменту генерации: эта генерация получает `Failed` с текстом, остальные не затрагиваются.
- Миграция: до загрузки каталога все модели без референсов. `ModelCatalogService` загружает каталог при первом обращении, поэтому при старте прикрепления каталог обновляется принудительно, если кеш пуст.
- Repeat, NewSeed и Edit генерации с референсом повторяют `reference_attachment_id`; если вложение удалено вместе с сессией, действие недоступно (сессия тоже удалена).
- Удаление сессии: папка вложений удаляется вместе с `images/<sessionId>`.

Тест-кейсы (условие → результат):
- Модель с `MaxReferenceImages = 0`, вложение есть → модель в `SkippedModels` с `NoReferenceSupport`; генерация не создана; `IImageGenerator` не вызван для неё.
- Модель с `MaxReferenceImages = 1`, вложение PNG → генерация создана, `ReferenceImageDataUrls` содержит один data URL `image/png`; у генерации `ReferenceAttachmentId` заполнен.
- Одна модель с референсами, одна без → одна генерация, одна пропущена; текст сообщения о прогоне содержит причину.
- Все модели без референсов → прогон не создан, `Run` null; сообщение ассистента с причиной.
- Вложение `image/gif` или текстовый файл → отказ до записи; файл не создан; строка в БД не добавлена.
- Вложение сохраняется: файл лежит по пути `images/<sessionId>/attachments/`; при ошибке записи в БД файл удаляется (или помечается как осиротевший — вопрос).
- Repeat генерации с референсом → новый прогон с тем же `ReferenceAttachmentId` и `ReferenceImageDataUrls` из того же файла.
- Новая миграция на базе с данными предыдущих: `image_models` очищается; колонка `max_reference_images` есть со значением 0; существующие генерации получают NULL в `reference_attachment_id`.
- `OpenRouterClient.GenerateAsync` с одним референсом: тело запроса содержит `input_references` (формат — после проверки, см. вопрос).
- Удаление сессии с вложением → папка `images/<sessionId>` удалена.
- Сообщение только с вложением (пустой текст) → сохраняется; ассистенту уходит без текста только при наличии 017.
- Отсутствующий файл вложения при старте генерации → генерация `Failed`, остальные генерации прогона выполнены.

Команды проверки:
- `dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj`
- `dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj`
- `dotnet format` по `ImageGen.Core`, `ImageGen.Core.Tests`, `ImageGen.Data`, `ImageGen.Data.Tests`, `ImageGen.Maui` (`--verify-no-changes`)
- Windows: `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0` и `-f net10.0-android`; ручной сценарий на Windows: прикрепить, отправить, увидеть пропуск модели без референсов.
- `scripts/check.sh`

Файлы: `ImageGen/ImageGen.Core/Domain/Chat/ChatAttachment.cs` (новый), `ImageGen/ImageGen.Core/Storage/IChatAttachmentRepository.cs` (новый), `ImageGen/ImageGen.Core/Storage/IImageFileStore.cs`, `ImageGen/ImageGen.Core/Domain/Models/ImageModel.cs`, `ImageGen/ImageGen.Core/Domain/Generations/Generation.cs`, `ImageGen/ImageGen.Core/Providers/ImageGenerationRequest.cs`, `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs`, `ImageGen/ImageGen.Core/Services/AttachmentService.cs` (новый), `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core/Services/AssistantService.cs`, `ImageGen/ImageGen.Core/Services/StartRunResult.cs` (новый), `ImageGen/ImageGen.Data/Migrations/NNNN_chat_attachments.sql` (новый), `ImageGen/ImageGen.Data/Migrations.cs`, `ImageGen/ImageGen.Data/FileImageStore.cs`, `ImageGen/ImageGen.Data/Repositories/ChatAttachmentRepository.cs` (новый), `ImageGen/ImageGen.Data/Repositories/RunRepository.cs`, `ImageGen/ImageGen.Data/Repositories/ImageModelRepository.cs`, `ImageGen/ImageGen.Data.Tests/Repositories/ChatAttachmentRepositoryTests.cs` (новый), `ImageGen/ImageGen.Data.Tests/FileImageStoreTests.cs`, `ImageGen/ImageGen.Core.Tests/Services/AttachmentServiceTests.cs` (новый), `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`, `ImageGen/ImageGen.Core.Tests/Services/AssistantServiceTests.cs`, `ImageGen/ImageGen.Core.Tests/OpenRouter/OpenRouterClientTests.cs`, `ImageGen/ImageGen.Maui/Services/IFilePickerService.cs` (новый), `ImageGen/ImageGen.Maui/Services/MauiFilePickerService.cs` (новый), `ImageGen/ImageGen.Maui/PageModels/ChatPageModel.cs`, `ImageGen/ImageGen.Maui/PageModels/ChatItemTemplateChoice.cs`, `ImageGen/ImageGen.Maui/PageModels/GenerationItemViewModel.cs`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Maui/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Maui/MauiProgram.cs`, `docs/architecture.md` (раздел «Хранение»: папка вложений)

## Отчёт

## Комментарии

### 2026-10-28, claude-qnrqyt
Открытые вопросы (план предложен, при ответе поправлю «План»):
1. Критерии: в них только пропуск моделей без референсов. Описание также требует, чтобы картинка ушла ассистенту и стала референсом генерации. Расширить критерии (это решение владельца)?
2. Одна картинка на сообщение (предложено) или несколько (до лимита модели)?
3. Разрешить сообщение только с картинкой и пустым текстом (предложено да)?
4. Пропуск модели: строка с причиной в сообщении о прогоне (предложено) или видимая карточка «пропущено» (новый `GenerationStatus`, это решение с ADR)?
5. Формат `input_references` в `POST /api/v1/images` (массив строк: ссылки или data URL) не проверен. Нужен один живой запрос с ключом. Без него часть «референс» не реализуется. Владелец или задача 016 проверяет?
6. Картинка сообщения становится референсом прогона, который запустил ассистент, автоматически (предложено) или только по отдельной кнопке?
7. Номер миграции: 018 берёт `0002`, 019 берёт `0003` (предложено), или наоборот — по порядку слияния?
8. Типы файлов: PNG, JPEG, WebP (предложено). Лимит размера в байтах — неизвестно, владелец задаёт?
9. Предупреждение F10.6 (картинки уходят провайдеру) — в этой задаче или позже?


### 2026-10-28, ревью (claude, Windows)
Общий порядок по пересечениям файлов и таблиц (предложение ревью, решает владелец): 035 → 036 → 020 → 017 → 018 → 019 → 027; 025 и 026 после 036 и принятия ADR 0003. Причины: 036 меняет `ImageModel`, `image_models` (ключ `(provider_id, id)`), `SessionSettings.ImageModelIds` (`ModelRef`) и `GenerationService`; 020 вводит полный список параметров модели, из которого 018 берёт лимит референсов; 017 вводит каталог чат-моделей, `IImageFileStore.ReadAsync` и `ImageDataUrl` для 018, 019 и 027. Номера миграций в планах заменены на `NNNN_<смысл>.sql` (правило в `docs/tasks.md`), строка «Среда» добавлена в «Описание».
По вопросу 7: номер миграции назначается при реализации (следующий свободный). Пересечение с 020: если 020 сделана раньше, отдельная колонка `image_models.max_reference_images` и `DELETE FROM image_models` не нужны — лимит берётся из `ImageModel.Parameters` (параметр `input_references`, `Max`), а устаревший кеш 020 уже перезагружает. План надо будет поправить по порядку, который выберет владелец.

### 2026-10-28, владелец (ответы в чате, записал ревью)
Порядок выполнения (решение владельца): 035 → 036 → 020 → 017 → 018 → 019 → 027, затем 025 и 026. План одобрен владельцем сейчас, не дожидаясь 016 (статус `approved`); начинать в этом порядке, после мерджа предыдущей задачи сверить план с кодом.
Ответы: (1) критерии расширяются — см. «Критерии готовности»; (2) **несколько картинок** на сообщение, не больше 4, каждая не больше 10 МБ; в генерацию уходит не больше `input_references.max` модели (лишние не отправляются, причина в сообщении о прогоне); (3) сообщение только с картинками разрешено; (4) пропуск модели — строка с причиной в сообщении о прогоне, нового статуса нет; (5) форма `input_references` — по допущению: массив data URL в теле `POST /api/v1/images` под именем из каталога; проверка на живом API — вручную после реализации; (6) картинки сообщения автоматически становятся референсами прогона, который запустил ассистент; (7) номер миграции — следующий свободный; (8) типы PNG, JPEG, WebP; (9) предупреждение F10.6 — в этой задаче. План поправлен по пункту 2 («Типы и ответственность»: список вложений).
