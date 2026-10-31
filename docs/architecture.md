# Архитектура

Основной сценарий — сессии с ассистентом (`docs/specs/chat-sessions.md`, ADR 0004). Первый релиз v0.1 — ходячий скелет: только OpenRouter, только MAUI. Поверх скелета вынесен общий слой представления и добавлена оболочка Avalonia (ADR 0006): они входят в раздел «Текущая раскладка». Вместе с fal и OpenRouter встроен Together AI для генерации изображений; Web остаётся целевой раскладкой (раздел «Целевая раскладка»). Together не предлагается как модель ассистента.

Документ описывает задуманное устройство, а не ход работ: что уже сделано, видно по статусам задач. Правится, только когда код разошёлся с описанием (`AGENTS.md`, «Когда что обновлять»).

## Текущая раскладка
Скелет v0.1 и вынесенные после него `ImageGen.Presentation` и `ImageGen.Avalonia` (ADR 0006). Источник правды для задач: имена проектов, каталогов и типов в задачах совпадают с этим разделом. Где какой проект собирается: `docs/knowledge/dev-environment.md`.

```
.editorconfig                      форматирование
global.json                        режим dotnet test: Microsoft.Testing.Platform
ImageGen/
  ImageGen.slnx
  Directory.Build.props            Nullable, ImplicitUsings, TreatWarningsAsErrors
  Directory.Packages.props         все версии пакетов
  ImageGen.Core/                   net10.0, без UI и без SQLite
    Domain/Sessions/               Session, SessionSettings, SessionTimeline, TimelineEntry, GenerationParameters
    Domain/Chat/                   ChatMessage, ChatRole, ChatAttachment, ConversationSummary
    Domain/Generations/            Run, RunOrigin, Generation, GenerationStatus, ErrorKind, ParameterOutcome, ParameterOutcomeKind, ParameterDropReason
    Domain/Keys/                   ApiKeyStatus, SecretNames (ключ по провайдеру)
    Domain/Models/                 ImageModel, ChatModel, ImageModelPrice, ImagePriceUnit (цена из каталога), ModelRef (ссылка «провайдер:модель»), ModelParameter, ParameterKind
    Domain/Descriptions/           описания картинок (ADR 0013)
    Platform/                      ISecretStore, IAppPaths
    Storage/                       ISessionRepository, IChatMessageRepository, IRunRepository, IConversationSummaryRepository,
                                   IImageModelRepository (кэш по провайдеру), IImageFileStore, IChatAttachmentRepository,
                                   IImageDescriptionRepository
    Providers/                     ProviderIds, ProviderResponseLimits, BoundedHttpContentReader, BoundedBase64ImageDecoder, ProviderConfig, ProviderKind, ConfiguredImageModel, ConfiguredChatModel, ProviderConfigLoader (встроенный
                                   builtin-providers.json и providers.json в DataDirectory), ProviderRegistry (проверка),
                                   IProviderRouter / ProviderRouter (клиент по id провайдера), ProviderClientBase (HTTP,
                                   таймаут из конфига, коды ошибок), ProviderException, ProviderConfigException
    Providers/                     IProviderConnection (проверка ключа), IModelProvider (каталог),
                                   ISynchronousImageTransport (HTTP transport внутри queued adapter), IAssistantChatClient (чат),
                                   AssistantChatMessage/Request/Result, AssistantTool,
                                   AssistantToolCall (нейтральный формат чата), ImageGenerationRequest/Result
    Providers/                     IQueuedImageProvider (submit/poll/cancel, ADR 0003), InMemoryQueuedImageProvider и
                                   InMemoryQueuedImageConnection (адаптер синхронного транспорта), ProviderJobHandle,
                                   JobState, JobUpdate, CancelOutcome, ProviderJobPoller (общий опрос заданий)
    Fal/                           FalImageProvider (очередь fal, IQueuedImageProvider), FalUrlValidator
    Http/                          LoggingHttpHandler, BodyRedactor (журнал HTTP без секретов)
    Diagnostics/                   IDebugTrace, TraceRedaction (отладочный трейс, docs/specs/debug-trace.md)
    OpenRouter/                    OpenRouterClient (картинки и чат)
    OpenAiCompatible/              OpenAiCompatibleClient (картинки и чат), OpenAiChatWire (формат chat/completions)
    AnthropicCompatible/           AnthropicCompatibleClient (только чат, Messages API)
    Services/                      ApiKeyService, ModelCatalogService, SessionService, SessionChangeNotifier, TimelineOrdering, GenerationService,
                                   AssistantService, AssistantPrompts, ISeedSource, RandomSeedSource,
                                   ConversationSummaryService (сводка старой истории в фоне; одна задача на сессию),
                                   AttachmentService (вложения, ADR 0012), ImageDescriptionService (ADR 0013),
                                   ChatModelCatalogService (модели ассистента), GenerationParameterPlanner (ADR 0011)
    Search/                        SessionSearchService (поиск по названиям, промптам и описаниям), FuzzyMatcher (нечёткие
                                   совпадения слов), ISearchTextSource (тексты всех сессий; реализация в Data)
    Context/                       ContextBudget (бюджет истории), ContextBuilder (выбор сообщений), TokenEstimator,
                                   TokenCorrection (поправка по usage), SummaryPlanner и SummaryPlan (когда и что сводить, C3)
    Serialization/                 CoreJsonContext
    CoreServiceCollectionExtensions.cs   AddImageGenCore()
  ImageGen.Data/                   net10.0, Microsoft.Data.Sqlite, MetadataExtractor (ADR 0021): SqliteDatabase, Migrations,
                                   репозитории, FileImageStore (чтение размеров из заголовков файла в ограниченном фоне),
                                   DataServiceCollectionExtensions (AddImageGenData())
  ImageGen.Core.Tests/             xUnit v3 + NSubstitute
  ImageGen.Data.Tests/             xUnit v3, временная БД в файле
  ImageGen.Presentation/           net10.0, CommunityToolkit.Mvvm: PageModels/, Services/ (INavigationService, IDialogService, IImageViewerService, ImageViewerNavigationState, ImageViewerHitTest, IFolderLauncher, ICurrentSessionTracker, CurrentSessionTracker,
                                   IMainThread, ISecretPersistence, AppInitializer, буфер обмена, выбор файлов, сохранение картинок), Routes,
                                   Utilities/, Resources/Strings/ (AppResources.resx), Appearance/ (стиль и тема, AppearanceService),
                                   Logging/ (файловый журнал, FileDebugTrace)
  ImageGen.Presentation.Tests/     xUnit v3 + NSubstitute: модели страниц, маршруты, строки, архитектурный тест
  ImageGen.Avalonia/               десктоп (Avalonia 12.1, WinExe): App, Views/ (MainWindow, SessionsView, SessionsListView,
                                   ApiKeyView, ChatView, SessionModelsView, SessionParametersView, ModelPickerView, SettingsView,
                                   StarterExamples), Themes/ (стили по макетам), Composition/AppServices, Converters/ (FilePathToBitmap),
                                   Markup/ (расширение строк), Platform/ (DesktopAppPaths, InMemorySecretStore), Services/
                                   (AvaloniaMainThread, AvaloniaDialogService, буфер, выбор файлов, сохранение картинок,
                                   AvaloniaAppearanceApplier, AvaloniaImageViewerService, AvaloniaFullScreenController, AvaloniaFolderLauncher, Logging/, Navigation/ — стек страниц и маршруты),
                                   Services/Secrets/ (DesktopSecretStore и бэкенды: SecretServiceBackend для Linux,
                                   DpapiSecretBackend для Windows, KeychainSecretBackend для macOS; ADR 0008)
  ImageGen.Avalonia.Tests/         headless (Avalonia.Headless.XUnit), xunit.v3 3.2.2 (ADR 0007)
  ImageGen.Maui/                   MAUI: Pages/, Controls/ (SessionsListView, StarterExamples, ChatEditor, SessionModelsView — общий экран моделей страницы и панели), PageModels/ (только
                                   ChatItemTemplateSelector), Services/ (MauiSecretStore, MauiAppPaths, MauiMainThread,
                                   Shell-реализации, буфер, выбор файлов, MauiImageSaver, MauiAppearanceApplier, MauiImageViewerService, MauiFullScreenController, MauiFolderLauncher, MauiSecretPersistence), Converters/,
                                   Platforms/
```

Зависимости: `Maui → Presentation → Data → Core`; `Maui` ссылается также на `Core` и `Data` (регистрация в `MauiProgram`); `Avalonia → Presentation → Data → Core` (также ссылается на `Core` и `Data`, регистрация в `AppServices`); `Presentation → Core`.

Журнал: `Core`, `Data` и `Presentation` пишут через `ILogger<T>`. Файл `{DataDirectory}/logs/imagegen-yyyyMMdd.log` настраивает `ImageGen.Presentation/Logging` (`AddImageGenFileLogging`, Serilog; ADR 0010), оболочки его вызывают. Уровень: `IMAGEGEN_LOG_LEVEL` или `logging.json` в папке данных. Отладочный трейс (включается отдельно: `IMAGEGEN_DEBUG_TRACE` или `debugTrace` в `logging.json`) пишет вводы пользователя и HTTP-обмен с провайдерами в `debug/<дата>/` через `Core/Diagnostics/IDebugTrace`; реализация — `Presentation/Logging/FileDebugTrace`, спецификация `docs/specs/debug-trace.md`. `Core` ссылается только на `Microsoft.Extensions.Http`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`. Архитектурный тест в `ImageGen.Core.Tests` проверяет, что `Core` не ссылается на `Microsoft.Maui*`, `Microsoft.AspNetCore*`, `Microsoft.Data.Sqlite*`, `Avalonia*`; тест в `ImageGen.Presentation.Tests` проверяет, что `Presentation` не ссылается на `Microsoft.Maui*`, `Avalonia*`, `Microsoft.AspNetCore*`.

Логика страниц (PageModels, маршруты, строки, `AppInitializer`) живёт в `ImageGen.Presentation` и не использует типы UI-фреймворков: тестируется обычными тестами на Linux, без ссылок на файлы MAUI. Типы MAUI (`IQueryAttributable`, `MainThread`, `ImageSource`) остаются в страницах, конвертерах и реализациях интерфейсов (`IMainThread`, `INavigationService`, `IDialogService`) в `ImageGen.Maui`.

## Скелет v0.1: потоки
**Первый запуск.** `App` оболочки вызывает `AppInitializer` (`ImageGen.Presentation`): он инициализирует БД (`SqliteDatabase.InitializeAsync`) и восстанавливает незавершённые генерации (`GenerationService.RecoverInterruptedAsync`). Для Fal сохранённый хэндл позволяет продолжить polling без повторного submit. Хэндлы in-memory адаптера действуют только пока работает процесс: после перезапуска такой запрос завершается как прерванный, без повторной платной генерации. Генерации провайдера, которого больше нет в конфигурации, завершаются «ModelUnavailable», остальные без восстанавливаемого хэндла помечаются «Interrupted». Только OpenRouter обязателен для first-run, поскольку это provider текущей модели ассистента; если его ключа нет (`ApiKeyService.HasKeyAsync`), навигация открывает экран ключа с валидированным provider id. Ключ проверяется connection выбранного провайдера и сохраняется через `ISecretStore` (реализации — в разделе «Скелет v0.1: хранение»); остальные настроенные ключи optional и управляются из Settings через `ApiKeyService`. Settings показывает маскированный статус, а Remove удаляет только секрет выбранного провайдера; OpenRouter removal предупреждает, что ассистент временно останется без ключа.

**Сообщение в чат.** `ChatPageModel` → `AssistantService.SendUserMessageAsync`:
1. Сообщение пользователя пишется в БД; у новой сессии название берётся из первых 40 символов.
2. Запрос к OpenRouter chat completions: системный промпт, текущий промпт сессии, при наличии — сводка старой части истории, затем сообщения сессии по бюджету контекста модели (текущее сообщение пользователя — дословно), инструмент `generate_images`. Правила бюджета и сводки — `docs/specs/assistant-context.md`, ADR 0015.
3. Вызов инструмента → `GenerationService.StartRunAsync` (прогон) и сообщение ассистента со ссылкой на прогон. Текст без вызова → сообщение ассистента. Ошибка → сообщение ассистента с признаком ошибки.

**Прогон.** `GenerationService` берёт модели и N из настроек сессии, выбирает N сидов (`ISeedSource`), создаёт генерации «модель × сид» в статусе «в очереди» одной транзакцией и запускает их параллельно: не более 4 локально отслеживаемых jobs на provider id. Provider A не блокирует provider B. Все провайдеры проходят общий путь: `IQueuedImageProvider.SubmitAsync` → сохранённый в БД хэндл → `ProviderJobPoller` → терминальное состояние → файл через `IImageFileStore`, запись в БД и событие `GenerationUpdated`. Fal хранит удалённый хэндл и продолжает polling после перезапуска. OpenRouter и OpenAI-совместимые клиенты выполняют синхронный HTTP transport (`ISynchronousImageTransport`) внутри `InMemoryQueuedImageProvider`; их handle живёт только в памяти процесса, поэтому после перезапуска генерация прерывается без повторного submit. Локальный адаптер быстро опрашивается (50–250 мс), native provider — с интервалом 1–5 секунд. Лимит provider slot удерживается до terminal status; при удалении сессии локальный adapter также ожидает завершения отменённого transport. Если remote cancel не удался, лимит гарантирует только локально отслеживаемую работу, а не остановку удалённой операции. Ошибка polling завершает генерацию: «Moderated» — «отфильтровано», остальные ошибки провайдера — «ошибка» с его кодом. Ошибка записи результата в файл завершает генерацию с кодом `ImageSaveFailed`; ошибка записи terminal state в БД не подменяется этим кодом и остаётся ошибкой persistence. Отказ модерации (403 с `metadata.reasons`) → «отфильтровано».

**Обновление экрана.** `ChatPageModel` после каждого своего действия перечитывает ленту сессии (`SessionService.GetTimelineAsync`), а событие `GenerationUpdated` обновляет одну карточку в UI-потоке (`IMainThread.BeginInvokeOnMainThread`; реализации `MauiMainThread` и `AvaloniaMainThread`). `SessionService` и `GenerationService` сообщают об изменении сессии через `SessionChangeNotifier`, и `SessionsPageModel` перечитывает одну сессию и обновляет её строку в UI-потоке.

**Удаление сессии.** `SessionService` вызывает `ISessionGenerationCoordinator` до удаления записей и файлов; в production этот интерфейс и `GenerationService` разрешаются одним singleton instance. До начала остановки проверяется cancellation caller-а. Остановка отменяет локальные tokens и ждёт submit phases; для уже известных queued jobs запрашивается remote cancel с timeout провайдера. Remote cancel является best effort: при ошибке провайдерская операция может продолжиться. После остановки локальные записи гарантированно прекращены, затем удаляются строки БД и каталог файлов и отправляется уведомление об изменении сессии. Cleanup выполняется с `CancellationToken.None`; ошибка шага останавливает цепочку, повторное удаление может продолжить идемпотентную очистку. Задача, которую процесс ещё не зарегистрировал, этим механизмом не отменяется.

## Скелет v0.1: хранение
SQLite-файл `imagegen.db` в `IAppPaths.DataDirectory`, схема в `ImageGen.Data/Migrations/NNNN_имя.sql` — встроенные ресурсы, применяются по номеру без списка в коде (`Migrations.cs`, ADR 0002). Картинки: `images/<sessionId>/<generationId>.<ext>` в той же папке; в БД хранится относительный путь. Вложения сообщений (картинки, которые пользователь прикрепил в чате, ADR 0012) лежат в `images/<sessionId>/attachments/<attachmentId>.<ext>`, строки в `chat_attachments`. Ключи провайдеров хранятся только за `ISecretStore` (`SecretNames.ForProvider`) и в открытом виде не попадают ни в БД, ни в настройки, ни в файлы: в MAUI — `SecureStorage` (`MauiSecretStore`), в Avalonia — `DesktopSecretStore` (Secret Service на Linux, DPAPI на Windows, Keychain на macOS; без них — только в памяти до выхода, ADR 0008).

Известное упрощение скелета: удаление сессии удаляет её генерации вместе с ценами. Правило ADR 0002 «удаление картинки не удаляет запись о расходах» вводится вместе с учётом расходов (backlog).

## Целевая раскладка (после скелета)
```
ImageGen.Core       + Catalog с фильтрами; расходы
ImageGen.Data       + таблица расходов, теги, коллекции
ImageGen.Presentation, ImageGen.Avalonia   + модели и экраны новых фич (раскладка — «Текущая раскладка»)
ImageGen.Maui       + Галерея, Сравнение, Расходы, Настройки; сохранение в галерею Android и iOS; ru-локализация
ImageGen.Web        ASP.NET Core: вход по паролю из переменной окружения, Data Protection для секретов,
                    фоновое выполнение прогонов, поток событий браузеру
ImageGen.Web/ClientApp   Vue 3 + Vite + TypeScript
ImageGen.Web.Tests
Dockerfile, docker-compose.yml   том /data
```
- Provider connections retain catalog/key/chat responsibilities; synchronous image HTTP calls are transport-only and wrapped by `InMemoryQueuedImageProvider` before orchestration sees them. ADR 0020 replaces ADR 0005's proposed OpenRouter-specific runner with this shared adapter for OpenRouter and OpenAI-compatible clients.
- События: для Web подписка через `Channel` вместо события .NET.
- Web: однопользовательский, пароль из переменной окружения, cookie-сессия; секреты в файле под Data Protection; ключи шифрования и данные в томе Docker.
- MAUI: на телефоне вкладки внизу, на широком экране боковая панель.
- Оболочки MAUI и Avalonia: логика экранов одна (`Presentation`), разметка своя в каждой; каждая экранная фича делается в обеих. Зависимости: `Maui → Presentation → Data → Core`, `Avalonia → Presentation`.

## Правила слоёв
- `Core` не знает про MAUI, Avalonia, ASP.NET Core, SQLite и UI-типы (архитектурный тест `CoreDependencyTests` запрещает сборки с префиксами `Microsoft.Maui`, `Microsoft.AspNetCore`, `Microsoft.Data.Sqlite`, `Avalonia`) (`Brush`, `Color`, `Shell`, `Preferences`, `FileSystem`). Платформенное — через `Core/Platform`.
- Код вне `Providers/`, `Fal/`, `OpenRouter/`, `OpenAiCompatible/`, `AnthropicCompatible/` не знает, какой провайдер рисует картинки или отвечает ассистенту: провайдер берётся по `ModelRef` (провайдер и модель) через `IProviderRouter`. Адреса и типы API провайдеров задаёт конфиг (ADR 0009), ключи — только в `ISecretStore`. Исключения: `ModelVendors` распознаёт префикс `fal-ai` (S5.5), `GenerationService` быстро опрашивает локальный adapter (ADR 0020), first-run проверяет ключ OpenRouter.
- Ядро не формирует тексты интерфейса: ошибки и состояния отдаёт кодами (`ErrorKind`, `GenerationStatus`), тексты в `.resx` слоя представления (`Presentation`, общий для MAUI и Avalonia).
- Страницы MAUI и view Avalonia знают только свой `PageModel`; `PageModel` знает сервисы ядра.

## Внешние системы
Встроенные providers: OpenRouter (HTTP API: `/api/v1/key`, `/api/v1/models`, `/api/v1/images/models`, `/api/v1/images`, `/api/v1/chat/completions`), fal.ai (queued API, ADR 0003) и Together AI (OpenAI-compatible `images/generations`, только изображения). Другие OpenAI-compatible API (`images/generations`, `chat/completions`) и Anthropic Messages API (чат) подключаются через `providers.json`. У Together встроенный каталог FLUX; пользовательский entry с `id=together` заменяет его по общим правилам config override. Список Together не добавляет чат-модели.

## Ограничения
- Платформы: у MAUI нет цели Linux; Linux-десктоп обслуживает Avalonia-оболочка (ADR 0006), браузер и Docker — Web-оболочка. Цель Windows у MAUI собирается только на Windows (`docs/knowledge/dev-environment.md`).
- Безопасность: ключ только в шифрованном хранилище; ключи и пароли не попадают в журналы, экспорт и репозиторий.
- Производительность: генерации идут параллельно, UI-поток не блокируется.
- Ссылки провайдеров истекают, поэтому файл сохраняется сразу (у OpenRouter картинка приходит в ответе).
- Trimming и AOT на iOS: в ядре и хранилище без рефлексии (JSON через `JsonNode` и source generation, SQL через ADO.NET).
