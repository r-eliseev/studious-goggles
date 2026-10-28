---
id: 019
status: approved
assignee:
claimed:
created: 2026-10-27
---

# Img2Text: описание по картинке

## Описание
Текстовое описание или промпт по готовой картинке (своей или результату), F4.9. Подстановка в поле ввода только действием пользователя, цена учитывается.

Детализировать (точные файлы, код, тесты, как в задачах v0.1) после задачи 016, когда будет код скелета.

Среда: только Windows (есть MAUI-часть: экран чата).

## Критерии готовности
- Провайдер, модель и цена описания видны.

## Анализ
Проверено 2026-10-28 по `origin/main`. Задачи 012–016 пока не закрыты (см. 017): MAUI-часть может поменяться.

Среда: строки «Среда» нет, значит MAUI-часть только Windows. Ядро и Data выполнимы на Linux.

Что уже есть:
- `OpenRouterChatMessage.ImageDataUrls` и `OpenRouterClient.BuildMessage` формируют запрос с картинкой; `IOpenRouterChatClient.CompleteChatAsync` возвращает текст и `CostUsd` (из `usage.cost`).
- `Generation.ImagePath` и `MediaType` для картинок результатов.
- `ChatPageModel.InputText` — поле ввода; `GenerationItemViewModel` — действия карточки (повтор, новый сид, правка).

Чего не хватает:
- Сервиса описания. Промпта для описания тоже нет.
- Признака «модель принимает картинки» для модели описания: каталога чат-моделей в коде пока нет (вводит 017).
- Чтения картинки результата как data URL (`IImageFileStore.ReadAsync` и `ImageDataUrl` вводит 017).
- Хранения описания и его цены. `CostUsd` сейчас нигде не сохраняется; отдельной таблицы расходов нет (`tasks/backlog/021-spending.md`, ещё не сделана). Без сохранения цена пропадёт при перезапуске, а критерий «цена видна» — про показ.
- Выбора модели описания. `SessionSettings` содержит только `ImageModelIds`, `RunsPerModel`, `AssistantModelId`.
- Своей картинки (F4.9: «своей или результату»): вложения делает 018.

Зависимости: 017 (каталог чат-моделей, `ImageDataUrl`, `ReadAsync`) — обязательна. 018 нужна только для «своей» картинки; вариант «сначала результаты» возможен без неё.

Конфликты:
- С 018: миграция (номер), `ChatPageModel` и `ChatPage` (оба меняют ввод и карточки).
- С 027: выбор модели описания, если он появится в 027, поменяет тот же экран настроек сессии.
- С 021 (расходы): таблица описаний с ценой — частичный дубль будущего учёта расходов.

## План
Цель: по готовой картинке (результату прогона) создаётся текстовое описание или промпт. Результат подставляется в поле ввода только действием пользователя. Провайдер, модель и цена видны.

Типы и ответственность:
- `Domain/Descriptions/DescriptionMode` (enum): `Description` (описание картинки), `Prompt` (промпт, которым можно воспроизвести картинку).
- `Domain/Descriptions/ImageDescription` (новый): `Id`, `SessionId`, `SourceGenerationId` (Guid?), `SourceAttachmentId` (Guid?, для 018), `Mode`, `ModelId`, `ProviderName` (всегда `OpenRouter` в скелете), `Text`, `CostUsd` (decimal?), `CreatedAt`.
- `Storage/IImageDescriptionRepository`: `AddAsync`, `ListBySessionAsync` (для истории по сессии, вопрос).
- `Services/DescriptionPrompts` (статический): системный текст для каждого режима. Текст пишется при реализации и сохраняется в файле; в плане только смысл: `Prompt` — один промпт на английском; `Description` — описание, язык — вопрос.
- `Services/ImageDescriptionService.DescribeAsync(sessionId, source, mode, ct) → ImageDescription`. Источник — `GenerationSource(generationId)`; вложение добавляется в 018.

Модель описания (предложено): модель ассистента сессии (`session.Settings.AssistantModelId`), отдельной настройки в 019 нет. Если модель ассистента не принимает картинки, описание не запускается, пользователь видит причину.

Псевдокод `DescribeAsync`:
```
session = сессия по sessionId (нет → ошибка)
model = session.Settings.AssistantModelId
если не chatModels.IsImageInputAsync(model):
    ошибка ModelDoesNotAcceptImages (запрос не отправляется, стоимости нет)
dataUrl = ReadDataUrl(source) (файла нет → ошибка SourceMissing, запрос не отправляется)
request = модель model; сообщения: системный промпт режима, user с текстом и dataUrl; без tools
result = chat.CompleteChatAsync(request)   (ProviderException → пробросить, описание не сохраняется)
если result.Text пуст: ошибка EmptyResponse, описание не сохраняется
description = ImageDescription(Text = trim(result.Text), ModelId = model, CostUsd = result.CostUsd)
repo.AddAsync(description)
вернуть description
```

Потоки данных:
- Карточка → `ChatPageModel.DescribeCardAsync(card, mode)` → `ImageDescriptionService.DescribeAsync` → `ChatModelCatalogService` → `IImageFileStore.ReadAsync` → `IOpenRouterChatClient` → `IImageDescriptionRepository` → результат на экран.
- Подстановка: диалог с текстом, моделью, провайдером и ценой; кнопка «Подставить в поле ввода» вызывает `InputText = text` только по нажатию. Автоматической подстановки нет.

UI (MAUI, Windows и Android):
- `GenerationItemViewModel`: новая команда «Описать» (с выбором режима, два пункта меню).
- `ChatPageModel`: команда `DescribeCard`, показывает результат через `IDialogService` (текст, «OpenRouter / <модель>», цена `$X.XXXX` или «неизвестно» при `CostUsd == null`), кнопки «Подставить» и «Закрыть».
- Если в поле ввода уже есть текст: подстановка с подтверждением (предложено: заменить или дописать — вопрос).
- Доступность: `SemanticProperties.Description` на кнопках «Описать» и «Подставить»; цену читать как текст, не только цветом.
- Тексты в `AppResources.resx`.

Миграция и хранение (предложено сейчас, вопрос ниже): `NNNN_image_descriptions.sql` (номер — следующий свободный при реализации), таблица `image_descriptions` с индексом по `session_id`. Удаление сессии удаляет описания каскадно (как генерации; известное упрощение скелета).

Крайние случаи:
- Модель ассистента без поддержки картинок: кнопка доступна, но запрос не отправляется; показывается причина.
- Файл картинки результата пропал: ошибка до запроса.
- Ошибка провайдера: описание не сохраняется, пользователю показывается ошибка. Стоимость неизвестна (неудачные запросы в chat completions, по `docs/specs/image-client.md`, не тарифицируются — для chat это не проверено).
- Пустой текст ответа: ошибка `EmptyResponse`, описание не сохраняется; `CostUsd` показывается, если провайдер его вернул (предложено: не сохранять).
- `CostUsd == null`: описание сохраняется, цена показывается как «неизвестно», не как `0`.
- Повторное нажатие «Описать» на той же картинке: новый запрос (предложено; кеша нет).

Тест-кейсы (условие → результат):
- Модель с картинками, картинка результата 1 → один вызов `CompleteChatAsync`; модель запроса = модель ассистента сессии; user-сообщение содержит data URL `image/png`.
- Модель без картинок на входе → ошибка `ModelDoesNotAcceptImages`; `CompleteChatAsync` не вызван.
- Файл картинки отсутствует → ошибка `SourceMissing`; `CompleteChatAsync` не вызван.
- Ответ с `CostUsd = 0.0004` → описание сохранено с ценой; `ModelId` и `ProviderName` заполнены.
- Ответ с `CostUsd = null` → описание сохранено с `CostUsd = null`; текст диалога «неизвестно» (тест на ViewModel).
- Ответ с пустым текстом → ошибка, описание не сохранено.
- `ProviderException` → ошибка, описание не сохранено.
- Режим `Prompt` → системный текст режима `Prompt` в запросе (не `Description`).
- На экране результата: при появлении диалога `InputText` не меняется; после «Подставить» `InputText` равен тексту описания (тест на `ChatPageModel`).
- Репозиторий: сохранённое описание читается с ценой `null` и без неё (Data.Tests на временной БД).

Команды проверки:
- `dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj`
- `dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj`
- `dotnet format` для `ImageGen.Core`, `ImageGen.Core.Tests`, `ImageGen.Data`, `ImageGen.Data.Tests`, `ImageGen.Maui` (`--verify-no-changes`)
- Windows: `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0` и `-f net10.0-android`; вручную на Windows: описать результат, проверить, что ввод не меняется до «Подставить».
- `scripts/check.sh`

Файлы: `ImageGen/ImageGen.Core/Domain/Descriptions/DescriptionMode.cs` (новый), `ImageGen/ImageGen.Core/Domain/Descriptions/ImageDescription.cs` (новый), `ImageGen/ImageGen.Core/Storage/IImageDescriptionRepository.cs` (новый), `ImageGen/ImageGen.Core/Services/DescriptionPrompts.cs` (новый), `ImageGen/ImageGen.Core/Services/ImageDescriptionService.cs` (новый), `ImageGen/ImageGen.Data/Migrations/NNNN_image_descriptions.sql` (новый; номер — следующий свободный), `ImageGen/ImageGen.Data/Migrations.cs`, `ImageGen/ImageGen.Data/Repositories/ImageDescriptionRepository.cs` (новый), `ImageGen/ImageGen.Data/DataServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Data.Tests/Repositories/ImageDescriptionRepositoryTests.cs` (новый), `ImageGen/ImageGen.Core.Tests/Services/ImageDescriptionServiceTests.cs` (новый), `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Maui/PageModels/GenerationItemViewModel.cs`, `ImageGen/ImageGen.Maui/PageModels/ChatPageModel.cs`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Maui/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Core.Tests/MauiLinked/ChatPageModelTests.cs`, `docs/architecture.md` (раздел «Хранение»: таблица описаний)

## Отчёт

## Комментарии

### 2026-10-28, claude-qnrqyt
Открытые вопросы (план предложен, при ответе поправлю «План»):
1. Модель описания: модель ассистента сессии (предложено, без нового поля и экрана) или отдельная настройка? Если отдельная, это уже часть 027.
2. Режимы: «описание» и «промпт» обе нужны в скелете, или только промпт? Язык описания: английский (как промпты, S2.2) или язык пользователя?
3. Стоимость описания: сохранять в отдельной таблице `image_descriptions` сейчас (предложено), или ждать `tasks/backlog/021-spending.md`? Без сохранения цена не переживёт перезапуск.
4. Поведение при уже введённом тексте в поле ввода: заменить с подтверждением или дописать (предложено: спросить)?
5. Нужен ли повтор описания на той же картинке без запроса (кеш)? Предложено: нет, новый запрос.
6. Неудачные запросы chat completions тарифицируются ли? В `docs/specs/image-client.md` написано только про картинки. Неизвестно.
7. Источник «своей» картинки: ждать 018 или сделать сначала описание результатов (предложено: результаты сначала)?


### 2026-10-28, ревью (claude, Windows)
Общий порядок по пересечениям файлов и таблиц (предложение ревью, решает владелец): 035 → 036 → 020 → 017 → 018 → 019 → 027; 025 и 026 после 036 и принятия ADR 0003. Причины: 036 меняет `ImageModel`, `image_models` (ключ `(provider_id, id)`), `SessionSettings.ImageModelIds` (`ModelRef`) и `GenerationService`; 020 вводит полный список параметров модели, из которого 018 берёт лимит референсов; 017 вводит каталог чат-моделей, `IImageFileStore.ReadAsync` и `ImageDataUrl` для 018, 019 и 027. Номера миграций в планах заменены на `NNNN_<смысл>.sql` (правило в `docs/tasks.md`), строка «Среда» добавлена в «Описание».

### 2026-10-28, владелец (ответы в чате, записал ревью)
Порядок выполнения (решение владельца): 035 → 036 → 020 → 017 → 018 → 019 → 027, затем 025 и 026. План одобрен владельцем сейчас, не дожидаясь 016 (статус `approved`); начинать в этом порядке, после мерджа предыдущей задачи сверить план с кодом.
Ответы на вопросы 1–7: принять всё предложенное. Модель описания — модель ассистента сессии; два режима, «описание» и «промпт»; **язык описания — язык интерфейса приложения** (сейчас английский), промпт всегда на английском; описание и цена хранятся в `image_descriptions` сейчас; при тексте в поле ввода — спросить «заменить или дописать»; без кеша; сначала только картинки-результаты. Вопрос 6 (тарифицируются ли неудачные chat completions) остаётся «неизвестно»: цена неудачного запроса показывается как «неизвестно».
Следствие решения по 036 (выполняется раньше): чат ассистента идёт через общий `Providers/IAssistantChatClient` вместо `IOpenRouterChatClient`. Всё, что этот план добавляет или использует в `IOpenRouterChatClient` (например, `ListChatModelsAsync`, `CompleteChatAsync`), относится к `IAssistantChatClient`; при начале работы сверить план с кодом после 036.
