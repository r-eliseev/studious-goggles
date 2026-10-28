# Архитектура

Основной сценарий — сессии с ассистентом (`docs/specs/chat-sessions.md`, ADR 0004). Первый релиз v0.1 — ходячий скелет: только OpenRouter, только MAUI. Целевая раскладка с fal, Replicate и Web строится поверх скелета (раздел «Целевая раскладка»).

Документ описывает задуманное устройство, а не ход работ: что уже сделано, видно по статусам задач. Правится, только когда код разошёлся с описанием (`AGENTS.md`, «Когда что обновлять»).

## Скелет v0.1: раскладка
Источник правды для задач скелета: имена проектов, каталогов и типов в задачах совпадают с этим разделом. Где какой проект собирается: `docs/knowledge/dev-environment.md`.

```
.editorconfig                      форматирование
global.json                        режим dotnet test: Microsoft.Testing.Platform
ImageGen/
  ImageGen.slnx
  Directory.Build.props            Nullable, ImplicitUsings, TreatWarningsAsErrors
  Directory.Packages.props         все версии пакетов
  ImageGen.Core/                   net10.0, без UI и без SQLite
    Domain/Sessions/               Session, SessionSettings, SessionTimeline, TimelineEntry
    Domain/Chat/                   ChatMessage, ChatRole
    Domain/Generations/            Run, RunOrigin, Generation, GenerationStatus, ErrorKind
    Domain/Keys/                   ApiKeyStatus, SecretNames
    Domain/Models/                 ImageModel
    Platform/                      ISecretStore, IAppPaths
    Storage/                       ISessionRepository, IChatMessageRepository, IRunRepository,
                                   IImageModelRepository, IImageFileStore
    Providers/                     ProviderException, IImageGenerator, ImageGenerationRequest, ImageGenerationResult
    Providers/IModelProvider       слой абстракции над провайдером моделей (реализация: OpenRouterClient)
    OpenRouter/                    OpenRouterClient, IOpenRouterChatClient (чат ассистента), запросы и ответы чата
    Services/                      ApiKeyService, ModelCatalogService, SessionService, TimelineOrdering, GenerationService,
                                   AssistantService, AssistantPrompts, ISeedSource, RandomSeedSource
    Serialization/                 CoreJsonContext
    CoreServiceCollectionExtensions.cs   AddImageGenCore()
  ImageGen.Data/                   net10.0, Microsoft.Data.Sqlite: SqliteDatabase, Migrations, репозитории, FileImageStore,
                                   DataServiceCollectionExtensions (AddImageGenData())
  ImageGen.Core.Tests/             xUnit v3 + NSubstitute
  ImageGen.Data.Tests/             xUnit v3, временная БД в файле
  ImageGen.Presentation/           net10.0, CommunityToolkit.Mvvm: PageModels/, Services/ (INavigationService, IDialogService,
                                   IMainThread, AppInitializer), Routes, Utilities/, Resources/Strings/ (AppResources.resx)
  ImageGen.Presentation.Tests/     xUnit v3 + NSubstitute: модели страниц, маршруты, строки, архитектурный тест
  ImageGen.Maui/                   MAUI: Pages/, PageModels/ (только ChatItemTemplateSelector), Services/ (MauiSecretStore,
                                   MauiAppPaths, MauiMainThread, Shell-реализации), Converters/
```

Зависимости: `Maui → Presentation → Data → Core`; `Maui` ссылается также на `Core` и `Data` (регистрация в `MauiProgram`); `Presentation → Core`. `Core` ссылается только на `Microsoft.Extensions.Http`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`. Архитектурный тест в `ImageGen.Core.Tests` проверяет, что `Core` не ссылается на `Microsoft.Maui*`, `Microsoft.AspNetCore*`, `Microsoft.Data.Sqlite*`; тест в `ImageGen.Presentation.Tests` проверяет, что `Presentation` не ссылается на `Microsoft.Maui*`, `Avalonia*`, `Microsoft.AspNetCore*`.

Логика страниц (PageModels, маршруты, строки, `AppInitializer`) живёт в `ImageGen.Presentation` и не использует типы UI-фреймворков: тестируется обычными тестами на Linux, без ссылок на файлы MAUI. Типы MAUI (`IQueryAttributable`, `MainThread`, `ImageSource`) остаются в страницах, конвертерах и реализациях интерфейсов (`IMainThread`, `INavigationService`, `IDialogService`) в `ImageGen.Maui`.

## Скелет v0.1: потоки
**Первый запуск.** `App` инициализирует БД (`SqliteDatabase.InitializeAsync`), помечает незавершённые генерации «Interrupted» (`GenerationService.RecoverInterruptedAsync`). Если ключа OpenRouter нет (`ApiKeyService.HasKeyAsync`), открывается страница ключа; ключ проверяется `GET /api/v1/key` и сохраняется в `SecureStorage` (`ISecretStore`).

**Сообщение в чат.** `ChatPageModel` → `AssistantService.SendUserMessageAsync`:
1. Сообщение пользователя пишется в БД; у новой сессии название берётся из первых 40 символов.
2. Запрос к OpenRouter chat completions: системный промпт, текущий промпт сессии, последние 20 сообщений, инструмент `generate_images`.
3. Вызов инструмента → `GenerationService.StartRunAsync` (прогон) и сообщение ассистента со ссылкой на прогон. Текст без вызова → сообщение ассистента. Ошибка → сообщение ассистента с признаком ошибки.

**Прогон.** `GenerationService` берёт модели и N из настроек сессии, выбирает N сидов (`ISeedSource`), создаёт генерации «модель × сид» в статусе «в очереди» одной транзакцией и запускает их параллельно (не больше 4 одновременно). Каждая генерация: «идёт» → `IImageGenerator.GenerateAsync` (`POST /api/v1/images`) → файл через `IImageFileStore` → запись в БД → событие `GenerationUpdated`. Отказ модерации (403 с `metadata.reasons`) → «отфильтровано».

**Обновление экрана.** `ChatPageModel` после каждого своего действия перечитывает ленту сессии (`SessionService.GetTimelineAsync`), а событие `GenerationUpdated` обновляет одну карточку в UI-потоке (`MainThread.BeginInvokeOnMainThread`).

## Скелет v0.1: хранение
SQLite-файл `imagegen.db` в `IAppPaths.DataDirectory`, схема в `ImageGen.Data/Migrations/NNNN_имя.sql` — встроенные ресурсы, применяются по номеру без списка в коде (`Migrations.cs`, ADR 0002). Картинки: `images/<sessionId>/<generationId>.<ext>` в той же папке; в БД хранится относительный путь. Ключ OpenRouter — только `SecureStorage`.

Известное упрощение скелета: удаление сессии удаляет её генерации вместе с ценами. Правило ADR 0002 «удаление картинки не удаляет запись о расходах» вводится вместе с учётом расходов (backlog).

## Целевая раскладка (после скелета)
```
ImageGen.Core       + Providers/Fal, Providers/Replicate за контрактом ADR 0003; Catalog с фильтрами; расходы
ImageGen.Data       + таблица расходов, теги, коллекции
ImageGen.Presentation    net10.0, CommunityToolkit.Mvvm: модели страниц и строк, INavigationService, IDialogService,
                    IMainThread, Routes, AppInitializer, строки .resx (ADR 0006); без типов MAUI и Avalonia
ImageGen.Presentation.Tests
ImageGen.Avalonia   десктоп Linux, Windows, macOS: Views/, свой маршрутизатор по Routes, диалоговые окна,
                    ключи в DPAPI / Keychain / Secret Service, без них в памяти (ADR 0006)
ImageGen.Avalonia.Tests  headless (Avalonia.Headless.XUnit), идут на Linux
ImageGen.Maui       + Галерея, Сравнение, Расходы, Настройки; сохранение в галерею Android и iOS; ru-локализация
ImageGen.Web        ASP.NET Core: вход по паролю из переменной окружения, Data Protection для секретов,
                    фоновое выполнение прогонов, поток событий браузеру
ImageGen.Web/ClientApp   Vue 3 + Vite + TypeScript
ImageGen.Web.Tests
Dockerfile, docker-compose.yml   том /data
```
- Провайдеры: контракт submit/poll/cancel (ADR 0003, ADR 0005) заменит узкий `IImageGenerator`, когда появятся fal и Replicate; тогда же восстановление незавершённых генераций после перезапуска. OpenRouter в нём — задание в фоне в памяти процесса (`OpenRouterJobRunner`), статус всех провайдеров читается общим опросом (`ProviderJobPoller`).
- События: для Web подписка через `Channel` вместо события .NET.
- Web: однопользовательский, пароль из переменной окружения, cookie-сессия; секреты в файле под Data Protection; ключи шифрования и данные в томе Docker.
- MAUI: на телефоне вкладки внизу, на широком экране боковая панель.
- Оболочки MAUI и Avalonia: логика экранов одна (`Presentation`), разметка своя в каждой; каждая экранная фича делается в обеих. Зависимости: `Maui → Presentation → Data → Core`, `Avalonia → Presentation`.

## Правила слоёв
- `Core` не знает про MAUI, ASP.NET Core, SQLite и UI-типы (`Brush`, `Color`, `Shell`, `Preferences`, `FileSystem`). Платформенное — через `Core/Platform`.
- Код вне `Providers/` и `OpenRouter/` не знает, какой провайдер рисует картинки (исключение скелета: ассистент работает только через OpenRouter, `AssistantService` использует `IOpenRouterChatClient`).
- Ядро не формирует тексты интерфейса: ошибки и состояния отдаёт кодами (`ErrorKind`, `GenerationStatus`), тексты в `.resx` слоя представления (`Presentation`, общий для MAUI и Avalonia).
- Страницы MAUI знают только свой `PageModel`; `PageModel` знает сервисы ядра.

## Внешние системы
OpenRouter (HTTP API: `/api/v1/key`, `/api/v1/images/models`, `/api/v1/images`, `/api/v1/chat/completions`). Позже fal.ai и Replicate.

## Ограничения
- Платформы: у MAUI нет цели Linux; Linux-десктоп обслуживает Avalonia-оболочка (ADR 0006), браузер и Docker — Web-оболочка. Цель Windows у MAUI собирается только на Windows (`docs/knowledge/dev-environment.md`).
- Безопасность: ключ только в шифрованном хранилище; ключи и пароли не попадают в журналы, экспорт и репозиторий.
- Производительность: генерации идут параллельно, UI-поток не блокируется.
- Ссылки провайдеров истекают, поэтому файл сохраняется сразу (у OpenRouter картинка приходит в ответе).
- Trimming и AOT на iOS: в ядре и хранилище без рефлексии (JSON через `JsonNode` и source generation, SQL через ADO.NET).
