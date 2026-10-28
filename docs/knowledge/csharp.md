# Справочник: C# и .NET для этого проекта

Написан по знаниям модели, не по сверке с живой документацией в этой сессии. Если факт важен для решения, сверьте с learn.microsoft.com и поправьте файл. Версии: .NET 10 (`10.0.402` стоит на машине), как в проекте-примере.

Источники для сверки: https://learn.microsoft.com/dotnet/csharp/ , https://learn.microsoft.com/dotnet/core/ , https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/

## Версии и возможности языка (то, что встречается в коде проекта)
- Nullable reference types (`<Nullable>enable</Nullable>`): `string?` для допустимого null, `!` только когда null исключён и это доказано, `[NotNullWhen(false)]` и подобные атрибуты описывают поток (как в `IsNullOrNew` примера).
- Collection expressions: `List<Tag> Tags { get; set; } = [];` (C# 12).
- Primary constructors для классов и структур (C# 12), `required` члены (C# 11), `record` и `record struct` для неизменяемых данных, `init`-свойства.
- Partial-свойства (C# 13): объявление `public partial string Name { get; set; }` + реализация генератором. Именно так `[ObservableProperty]` работает в `CommunityToolkit.Mvvm` 8.4 в примере (вместо полей `_name`).
- Ключевое слово `field` в свойствах и extension members (C# 14, .NET 10): допустимы, но их поддержка инструментами и анализаторами зависит от версии SDK; использовать, только если сборка и `dotnet format` проходят.
- Pattern matching: `is`, `switch`-выражения, списковые паттерны. Предпочитать `switch`-выражение таблице `if/else` при отображении статусов.
- `global using` и `ImplicitUsings` сокращают шапки файлов; свои глобальные using — в `GlobalUsings.cs`.

## Асинхронность
- `async/await` по всей цепочке; `Task`/`ValueTask` возвращать, не блокировать (`.Result`, `.Wait()` запрещены, особенно в UI-потоке).
- `CancellationToken` пробрасывать вниз до `HttpClient`; отмену не превращать в ошибку (ловить `OperationCanceledException` отдельно).
- Параллельный запуск нескольких моделей: список задач `Task`, `await Task.WhenAll(...)` с независимой обработкой ошибок каждой (ошибка одной не должна терять результаты остальных: либо `try/catch` внутри каждой задачи, либо разбор `Task.Status` после `WhenAll`). Ограничение числа одновременных запросов на провайдера: `SemaphoreSlim` или `Parallel.ForEachAsync` с `MaxDegreeOfParallelism`.
- Для потока обновлений статуса из задач в UI: `IProgress<T>` или `System.Threading.Channels`; в MAUI обновлять привязанные свойства из нужного потока (`MainThread.InvokeOnMainThreadAsync`), CommunityToolkit.Mvvm сам по себе потоки не переключает.
- `async void` только для обработчиков событий. Фоновый вызов без ожидания: обёртка `FireAndForgetSafeAsync` из примера.
- `ConfigureAwait(false)` в библиотеках (Core), не в коде UI.
- `SemaphoreSlim(1,1)` как асинхронный замок: `await _lock.WaitAsync(); try { ... } finally { _lock.Release(); }` (так в `SeedDataService` и `ModalErrorHandler`).

## HTTP и провайдеры
- `HttpClient` не создавать на каждый запрос. Использовать `IHttpClientFactory` (`Microsoft.Extensions.Http`): именованный или типизированный клиент на провайдера, базовый адрес и заголовки авторизации задаются при регистрации или на запросе. Ключ брать из хранилища секретов в момент запроса, не из статических полей.
- Авторизация по таблице спецификации: fal — `Authorization: Key <ключ>`, Replicate и OpenRouter — `Authorization: Bearer <ключ>`.
- Устойчивость: повторы только для временных ошибок (сеть, 429, 5xx), с ограничением числа и задержкой с джиттером; библиотека `Microsoft.Extensions.Http.Resilience` (на базе Polly). Не повторять запросы, создающие платные генерации, без идемпотентности; повтор должен укладываться в лимит расходов (F9.3).
- Скачивание результатов: потоком (`ResponseHeadersRead`, `CopyToAsync` в файл), не загружая в память целиком.
- Тестировать через подменённый `HttpMessageHandler`; реальных ключей в тестах нет.
- Заголовки и тела с ключами не логировать.

## JSON
- `System.Text.Json`. Для типов провайдеров — модели `record` и `[JsonPropertyName]`; для скорости и обрезки (trimming) — source generation: `[JsonSerializable]` на `JsonSerializerContext` (в примере `JsonContext`).
- Необязательные поля — nullable-свойства; неизвестные поля игнорируются по умолчанию. Для динамических схем параметров моделей (fal, Replicate) — `JsonElement`/`JsonNode`, не `dynamic`.
- Разбор данных пользователя валидировать и бросать понятную ошибку (образец — `GetRequiredString` в `SeedDataService`).

## Конфигурация, секреты, настройки
- `Microsoft.Extensions.Configuration` и Options pattern (`IOptions<T>`) для настроек. В MAUI своя встроенная конфигурация не подключена по умолчанию; ядро получает настройки через интерфейс.
- Секреты разработки: .NET User Secrets (`dotnet user-secrets`) — только для разработки, не для хранения ключей пользователей.
- Web-оболочка на ASP.NET Core: `Microsoft.AspNetCore.DataProtection` для шифрования файла секретов; ключи шифрования должны переживать перезапуск контейнера (том Docker), иначе секреты станут нечитаемыми. Пароль входа берётся из переменной окружения (в `appsettings` его не хранить).
- MAUI: `SecureStorage` (Keychain на iOS и Mac, Android Keystore, Windows DPAPI) — см. `docs/knowledge/maui.md`.

## Хранение данных
- Пример использует `Microsoft.Data.Sqlite` с ручным ADO.NET. Альтернативы: Dapper (тонкий слой над ADO.NET) или EF Core с SQLite (миграции, LINQ). Выбор не сделан, нужен ADR; критерии: миграции схемы, тестируемость, работа на всех оболочках.
- SQLite на MAUI требует нативный провайдер (`SQLitePCLRaw.bundle_e_sqlite3`, как в примере). Для Web/Docker достаточно того же пакета.
- Файлы результатов хранить отдельно от БД (в БД только путь и метаданные), имя файла по идентификатору результата.

## DI
- `Microsoft.Extensions.DependencyInjection`: `Singleton` (общее состояние, кэш каталога моделей), `Scoped` (запрос в ASP.NET Core), `Transient` (страницы с параметрами). Не хранить `Scoped` в `Singleton`.
- Зависимости через конструктор, на интерфейсы. Сервис-локатор (`IServiceProvider` в бизнес-коде) не использовать.

## Логирование
- `ILogger<T>` из `Microsoft.Extensions.Logging`; структурные сообщения (`_logger.LogError(e, "... {Id}", id)`), не интерполяция. Ключи, пароли, содержимое промптов не логировать.

## Тесты
- Выбрано (ADR 0004): xUnit v3 (`xunit.v3`) и NSubstitute для подмен. Ядро на `net10.0`, тесты не требуют MAUI-нагрузок. Тесты PageModel возможны, если навигация и сообщения вынесены в интерфейсы.
- xUnit v3 на .NET 10 SDK работает только в новом режиме `dotnet test` (Microsoft.Testing.Platform): в корне репозитория `global.json` с `{"test": {"runner": "Microsoft.Testing.Platform"}}`, тестовый проект с `<OutputType>Exe</OutputType>`. Иначе ошибка «Testing with VSTest target is no longer supported». Запуск: `dotnet test --project <путь к csproj>` (проверено 2026-10-27).
- В режиме Microsoft.Testing.Platform `dotnet test --solution <slnx>` собирает все проекты решения, включая MAUI; для ядра запускать `--project`.
- HTTP в тестах подменяется своим наследником `HttpMessageHandler` (NSubstitute не подменяет защищённый `SendAsync`).
- На Windows открытый файл SQLite нельзя удалить (`IOException: … being used by another process`), на Linux можно: тест, который удаляет временную папку, пока фоновая генерация ещё пишет в базу, падает только на Windows. `SqliteConnection.ClearAllPools()` не помогает, если соединение занято; нужно дождаться работы (`GenerationService.WhenIdleAsync`) (проверено 2026-10-28, задача 053).
- `CultureInfo.CurrentCulture` на машине владельца — `pl-PL` (запятая в дробных числах); CI — английская культура. Форматирование «по текущей культуре» проходит в CI и падает локально (проверено 2026-10-28).

## Команды dotnet
```
dotnet --info                       версия SDK и установленные нагрузки
dotnet build <sln>                  сборка
dotnet test <sln>                   тесты
dotnet format <sln> --verify-no-changes   проверка форматирования
dotnet add package <имя> --version <v>    пакет с явной версией
dotnet list package --vulnerable    проверка уязвимостей
dotnet user-secrets set <ключ> <значение> секреты для разработки
```

## Частые ошибки
- Забытый `await` (предупреждение CS4014) → задача теряется вместе с исключением.
- `async` лямбда в `List.ForEach` → `async void`; использовать `foreach` с `await` или `Task.WhenAll`.
- Общий `Random`, `HttpClient` или `List` из нескольких потоков без синхронизации.
- Сравнение денег и цен в `double`: для денег использовать `decimal` (цены провайдеров, суммы расходов).
- Строки дат без часового пояса: хранить в UTC (`DateTimeOffset`).

## Логирование и имена (проверено 2026-10-28)
- Serilog (`Serilog.Sinks.File`) в шаблоне `{Message:lj}` берёт значения в JSON-форме: строки и перечисления выходят в кавычках (`is "Succeeded"`). Для читаемой строки использовать `{Message:l}`.
- `TaskExtensions` в .NET 10 конфликтует с `System.Threading.Tasks.TaskExtensions` при `using ImageGen.Presentation.Utilities;` в тестах. Ссылаться через алиас (`using AppTaskExtensions = ImageGen.Presentation.Utilities.TaskExtensions;`).
- `HttpContent.TryComputeLength` доступен только внутри сборки. Размер ответа в обработчике: `(await content.ReadAsByteArrayAsync()).Length`, после этого тело остаётся читаемым.

