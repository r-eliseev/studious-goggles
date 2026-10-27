# Архитектура

Статус: 2026-10-27. Основной сценарий — сессии с ассистентом (`docs/specs/chat-sessions.md`, ADR 0004). Первый релиз v0.1 — ходячий скелет: только OpenRouter, только MAUI (`tasks/v0.1/`). Целевая раскладка с fal, Replicate и Web — ниже, в разделе «Целевая раскладка»; она строится поверх скелета (`tasks/backlog/`).

## Скелет v0.1: раскладка
Это источник правды для задач `tasks/v0.1/`. Имена проектов, каталогов и типов в задачах совпадают с этим разделом. Ядро, хранилище и их тесты собираются на Linux и Windows, `ImageGen.Maui` только на Windows (`docs/knowledge/dev-environment.md`).

```
.editorconfig                      форматирование (задача 001)
global.json                        режим dotnet test: Microsoft.Testing.Platform (задача 001)
ImageGen/
  ImageGen.slnx
  Directory.Build.props            Nullable, ImplicitUsings, TreatWarningsAsErrors
  Directory.Packages.props         все версии пакетов
  ImageGen.Core/                   net10.0, без UI и без SQLite
    Domain/                        Session, SessionSettings, ChatMessage, ChatRole, Run, RunOrigin,
                                   Generation, GenerationStatus, ErrorKind, ImageModel, ApiKeyStatus, SessionTimeline, TimelineEntry
    Platform/                      ISecretStore, IAppPaths, SecretNames
    Storage/                       ISessionRepository, IChatMessageRepository, IRunRepository,
                                   IImageModelRepository, IImageFileStore
    Providers/                     ProviderException, IImageGenerator, ImageGenerationRequest, ImageGenerationResult
    OpenRouter/                    IOpenRouterClient, OpenRouterClient, OpenRouterImageGenerator, запросы и ответы
    Services/                      ApiKeyService, ModelCatalogService, SessionService, TimelineOrdering, GenerationService,
                                   AssistantService, AssistantPrompts, ISeedSource, RandomSeedSource
    Serialization/                 CoreJsonContext
    CoreAssemblyMarker.cs
    CoreServiceCollectionExtensions.cs   AddImageGenCore()
  ImageGen.Data/                   net10.0, Microsoft.Data.Sqlite: SqliteDatabase, Migrations, репозитории, FileImageStore,
                                   DataServiceCollectionExtensions (AddImageGenData())
  ImageGen.Core.Tests/             xUnit v3 + NSubstitute
  ImageGen.Data.Tests/             xUnit v3, временная БД в файле
  ImageGen.Maui/                   MAUI: Pages/, PageModels/, Services/ (MauiSecretStore, MauiAppPaths), Resources/Strings/
```

Зависимости: `Maui → Data → Core`, `Maui → Core`. `Core` ссылается только на `Microsoft.Extensions.Http`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`. Архитектурный тест в `ImageGen.Core.Tests` проверяет, что `Core` не ссылается на `Microsoft.Maui*`, `Microsoft.AspNetCore*`, `Microsoft.Data.Sqlite*`.

## Скелет v0.1: потоки
**Первый запуск.** `App` инициализирует БД (`SqliteDatabase.InitializeAsync`), помечает незавершённые генерации «Interrupted» (`GenerationService.RecoverInterruptedAsync`). Если ключа OpenRouter нет (`ApiKeyService.HasKeyAsync`), открывается страница ключа; ключ проверяется `GET /api/v1/key` и сохраняется в `SecureStorage` (`ISecretStore`).

**Сообщение в чат.** `ChatPageModel` → `AssistantService.SendUserMessageAsync`:
1. Сообщение пользователя пишется в БД; у новой сессии название берётся из первых 40 символов.
2. Запрос к OpenRouter chat completions: системный промпт, текущий промпт сессии, последние 20 сообщений, инструмент `generate_images`.
3. Вызов инструмента → `GenerationService.StartRunAsync` (прогон) и сообщение ассистента со ссылкой на прогон. Текст без вызова → сообщение ассистента. Ошибка → сообщение ассистента с признаком ошибки.

**Прогон.** `GenerationService` берёт модели и N из настроек сессии, выбирает N сидов (`ISeedSource`), создаёт генерации «модель × сид» в статусе «в очереди» одной транзакцией и запускает их параллельно (не больше 4 одновременно). Каждая генерация: «идёт» → `IImageGenerator.GenerateAsync` (`POST /api/v1/images`) → файл через `IImageFileStore` → запись в БД → событие `GenerationUpdated`. Отказ модерации (403 с `metadata.reasons`) → «отфильтровано».

**Обновление экрана.** `ChatPageModel` после каждого своего действия перечитывает ленту сессии (`SessionService.GetTimelineAsync`), а событие `GenerationUpdated` обновляет одну карточку в UI-потоке (`MainThread.BeginInvokeOnMainThread`).

## Скелет v0.1: хранение
SQLite-файл `imagegen.db` в `IAppPaths.DataDirectory`, схема в `ImageGen.Data/Migrations.cs` (ADR 0002, задача 003). Картинки: `images/<sessionId>/<generationId>.<ext>` в той же папке; в БД хранится относительный путь. Ключ OpenRouter — только `SecureStorage`.

Известное упрощение скелета: удаление сессии удаляет её генерации вместе с ценами. Правило ADR 0002 «удаление картинки не удаляет запись о расходах» вводится вместе с учётом расходов (backlog).

## Целевая раскладка (после скелета)
```
ImageGen.Core       + Providers/Fal, Providers/Replicate за контрактом ADR 0003; Catalog с фильтрами; расходы
ImageGen.Data       + таблица расходов, теги, коллекции
ImageGen.Maui       + Галерея, Сравнение, Расходы, Настройки; сохранение в галерею Android и iOS; ru-локализация
ImageGen.Web        ASP.NET Core: вход по паролю из переменной окружения, Data Protection для секретов,
                    фоновое выполнение прогонов, поток событий браузеру
ImageGen.Web/ClientApp   Vue 3 + Vite + TypeScript
ImageGen.Web.Tests
Dockerfile, docker-compose.yml   том /data
```
- Провайдеры: контракт submit/watch/cancel из ADR 0003 заменит узкий `IImageGenerator`, когда появятся fal и Replicate; тогда же восстановление незавершённых генераций после перезапуска.
- События: для Web подписка через `Channel` вместо события .NET.
- Web: однопользовательский, пароль из переменной окружения, cookie-сессия; секреты в файле под Data Protection; ключи шифрования и данные в томе Docker.
- MAUI: на телефоне вкладки внизу, на широком экране боковая панель.

## Правила слоёв
- `Core` не знает про MAUI, ASP.NET Core, SQLite и UI-типы (`Brush`, `Color`, `Shell`, `Preferences`, `FileSystem`). Платформенное — через `Core/Platform`.
- Код вне `Providers/` и `OpenRouter/` не знает, какой провайдер рисует картинки (исключение скелета: ассистент работает только через OpenRouter, `AssistantService` использует `IOpenRouterClient`).
- Ядро не формирует тексты интерфейса: ошибки и состояния отдаёт кодами (`ErrorKind`, `GenerationStatus`), тексты в `.resx` оболочки.
- Страницы MAUI знают только свой `PageModel`; `PageModel` знает сервисы ядра.

## Внешние системы
OpenRouter (HTTP API: `/api/v1/key`, `/api/v1/images/models`, `/api/v1/images`, `/api/v1/chat/completions`). Позже fal.ai и Replicate.

## Состояние кода
Каркас скелета создан задачей 001: `ImageGen.Core`, `ImageGen.Core.Tests` (архитектурный тест). Задача 002 добавила доменную модель и интерфейсы ядра. Задача 003 добавила `ImageGen.Data` (`SqliteDatabase`: путь к файлу, открытие соединения, миграции через `PRAGMA user_version`; первая миграция создаёт таблицы скелета в `Migrations.cs`) и `ImageGen.Data.Tests` (временная БД в файле). Задача 006 добавила `ImageGen.Core/OpenRouter/` (`OpenRouterClient` для `GET key`, `GET images/models`, `POST images`, `POST chat/completions`; ошибки приводятся к `ProviderException` с `ErrorKind`) и тестовую подмену `StubHttpMessageHandler`. Репозитории и `FileImageStore` пока не написаны (задачи 004 и 005). Пустое `ImageGen.Maui` из шаблона добавляет задача 034, остальное — задачи `tasks/v0.1/`. Сервис сессий `SessionService` и порядок ленты `TimelineOrdering` (строка ленты `TimelineEntry`) — задача 008: они берут репозитории только через интерфейсы, реализаций хранилища пока нет (задачи 003–005). Пустое `ImageGen.Maui` из шаблона добавляет задача 034, остальное — задачи `tasks/v0.1/`.

## Ограничения
- Платформы: MAUI не поддерживает Linux; Linux обслуживает будущая Web-оболочка.
- Безопасность: ключ только в шифрованном хранилище; ключи и пароли не попадают в журналы, экспорт и репозиторий.
- Производительность: генерации идут параллельно, UI-поток не блокируется.
- Ссылки провайдеров истекают, поэтому файл сохраняется сразу (у OpenRouter картинка приходит в ответе).
- Trimming и AOT на iOS: в ядре и хранилище без рефлексии (JSON через `JsonNode` и source generation, SQL через ADO.NET).
