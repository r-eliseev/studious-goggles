---
id: 253
status: planned
assignee:
claimed:
created: 2026-11-09
---

# Приёмочные сценарии: мир приложения — сценарный фейк сети, время, сиды, перезапуск

## Описание
Каркас из 252 запускает одно приложение с простой заглушкой OpenRouter. Сценариям каталога (`docs/specs/acceptance-scenarios.md`) нужен управляемый «мир» (ADR 0031, пп. 3–4):
- вся сеть идёт в один сценарный фейк, неизвестный адрес проваливает сценарий;
- ответы провайдера задаются шагами «Дано» по модели и по очереди вызовов, ответ можно задержать до команды шага;
- время и сиды детерминированы;
- приложение можно «перезапустить» на той же папке данных и том же хранилище ключей;
- буфер обмена, диалоги и сохранение файла подменены и записывают, что у них просили.

Сейчас подменить снаружи можно только сеть OpenRouter (`AppServices.Create(openRouterHandler: …)`, `services.AddHttpClient(ProviderIds.OpenRouter)`); у fal свой транспорт (`FalImageProvider.TransportName`), остальные клиенты — `AddHttpClient()` без имени. Поэтому нужен тестовый шов в композиции.

Зависит от: 252.
Среда: Linux или Windows (headless).

## Критерии готовности
- Тестовый шов: у `AppServices.Create` (desktop) необязательный последний параметр `Action<IServiceCollection>? configureTestServices`, вызывается после `RegisterDesktop`. Приложение его не передаёт. Тест в `ImageGen.Avalonia.Tests/Composition/AppServicesTests.cs`: регистрация из шва заменяет платформенную службу (например, `IClipboardService`).
- Шаг факта (до кода мира): через шов и `ConfigureHttpClientDefaults` поставить фейк основным обработчиком для всех `HttpClient`. Проверить тестом, что запросы OpenRouter, fal (`queue.fal.run`) и Together (`api.together.ai`) из настоящих клиентов доходят до фейка. Если именованная настройка fal перебивает обработчик по умолчанию, записать это в `docs/knowledge/acceptance-tests.md` и в шве зарегистрировать фейк и для `FalImageProvider.TransportName` (имя `internal` — доступ через `InternalsVisibleTo`, если его нет — вопрос в «Комментарии», не делать `public`).
- `Support/FakeNetwork` — один `HttpMessageHandler` на мир:
  - маршруты по хосту и пути; без маршрута — ответ не отправляется, сценарий падает с текстом «неожиданный запрос <метод> <адрес>» (KEY-10);
  - журнал запросов: метод, адрес, тело (для проверок «модель без seed — seed не отправлен»), без заголовка `Authorization` в журнале;
  - запросы с ключом в теле или адресе в журнал и вывод теста не попадают.
- `Support/FakeOpenRouter` — маршруты OpenRouter по образцу `ImageGen.Avalonia.Tests/TestDoubles/OpenRouterFakes.cs` и по коду клиентов (`ImageGen/ImageGen.Core/OpenRouter/`): проверка ключа, каталог моделей картинок и цены (`/endpoints`), chat completions, генерация картинки. Управление:
  - каталог моделей — из таблицы шага;
  - ответ ассистента — очередь: текст | вызов `generate_images` с аргументами | вызов `generate_video` | ошибка с кодом и телом;
  - ответ генерации по модели: картинка (PNG заданного размера, по умолчанию 64×64) с `usage.cost` | ошибка с кодом и телом | фильтрация (как `imagesFiltered` в `OpenRouterFakes`) | «держать» — ответ ждёт, пока шаг не отпустит (`TaskCompletionSource`), отмена запроса освобождает ожидание;
  - видео (для 265) — только заготовка маршрутов, если форма запросов ясна из кода; иначе оставить 265.
- `Support/AppWorld` (контекст Reqnroll, один на сценарий):
  - временная папка данных, хранилище ключей в памяти (`DesktopSecretStore(backend: null)` — как в `ShellHarness`);
  - `FakeTimeProvider` (пакет `Microsoft.Extensions.TimeProvider.Testing`, версия с nuget.org — 10.11.0 на 2026-11-09) и детерминированный `ISeedSource` (очередь заданных сидов, по умолчанию 1, 2, 3, …) через шов;
  - фейки `IClipboardService`, `IDialogService` (ответы на подтверждения задаются шагом, по умолчанию — «да»), `IImageSaver`, `IFilePickerService` — записывают запросы;
  - `Start`, `Restart` (освободить композицию и окно, дождаться `GenerationService.WhenIdleAsync` и `IImageFileStore.WhenIdleAsync`, собрать новую на тех же папке и хранилище), `Dispose` (удалить папку; см. правило про Windows в `docs/conventions.md`);
  - ожидание условия с прокачкой UI-цикла и таймаутом (как `ShellHarness.WaitUntilAsync`); в сообщении таймаута — описание условия.
- Общие шаги `Steps/CommonSteps.cs` — словарь из `docs/specs/acceptance-scenarios.md`, раздел «Словарь мира». Каждый шаг — с одной формулировкой; таблицы — через `DataTable` Reqnroll.
- Самопроверки мира — обычные xUnit-тесты в `ImageGen.Acceptance.Tests/Support/` (`AppWorldTests`, `FakeNetworkTests`): неизвестный адрес → ошибка; удержанный ответ отпускается шагом; перезапуск видит сохранённую в первом запуске сессию; журнал не содержит ключа.
- KEY-01 из 252 переведён на мир; KEY-10 зелёный.
- Минимальная копия двойников из 252 удалена.

## Анализ
- Шов и композиция: `ImageGen/ImageGen.Avalonia/Composition/AppServices.Desktop.cs` (`Create`, `RegisterDesktop`), `AppServices.cs` (`CreateShared`, `openRouterHandler`).
- Сеть ядра: `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs` (`AddHttpClient()`, `AddHttpClient(FalImageProvider.TransportName)`, `TimeProvider.System`, `ISeedSource`), адреса провайдеров — `builtin-providers.json`.
- Образцы: `ImageGen/ImageGen.Avalonia.Tests/TestDoubles/ShellHarness.cs`, `OpenRouterFakes.cs`, `StubHttpMessageHandler.cs`, `TempAppPaths.cs`.
- Правка `AppServices.Desktop.cs` пересекается с незавершёнными задачами, если они меняют композицию Avalonia (207, 224, 234): перед claim проверить строки «Файлы» их планов; при пересечении — сначала смерджить ту, что раньше в очереди.

## План
1. Шов в `AppServices.Create` (TDD, тест в `AppServicesTests`).
2. Факт про `ConfigureHttpClientDefaults` и транспорт fal (тест), запись в knowledge.
3. `FakeNetwork` с журналом и «неожиданным запросом» (самопроверки сначала).
4. `FakeOpenRouter`: маршрут за маршрутом, каждый — сначала самопроверка.
5. `AppWorld`: время, сиды, фейки платформы, перезапуск (самопроверки сначала).
6. `CommonSteps`, перевод KEY-01, KEY-10.

Файлы:
- `ImageGen/ImageGen.Avalonia/Composition/AppServices.Desktop.cs`, `ImageGen/ImageGen.Avalonia.Tests/Composition/AppServicesTests.cs`;
- при необходимости `InternalsVisibleTo` для `ImageGen.Acceptance.Tests` в `ImageGen/ImageGen.Core/ImageGen.Core.csproj`;
- `ImageGen/ImageGen.Acceptance.Tests/Support/` (`AppWorld.cs`, `FakeNetwork.cs`, `FakeOpenRouter.cs`, `FakePlatform.cs`, `SequenceSeedSource.cs`, `AppWorldTests.cs`, `FakeNetworkTests.cs`), `Steps/CommonSteps.cs`, `Steps/FirstRunSteps.cs`, `Features/FirstRunAndKeys.feature`, `ImageGen.Acceptance.Tests.csproj`;
- `ImageGen/Directory.Packages.props`;
- `docs/knowledge/acceptance-tests.md`, `docs/specs/acceptance-scenarios.md` (словарь и статусы KEY-01, KEY-10).

### Проверка
```
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы» и раздела «Анализ»; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): падающий тест → запуск, убедиться, что он падает именно по описанной причине → минимальный код → тест зелёный.
- Формы запросов и ответов провайдера бери из кода клиентов и `OpenRouterFakes`, а не из памяти; что не нашёл в коде — не выдумывай, вопрос в «Комментарии».
- Шов — единственная правка кода приложения. Других правок в `ImageGen.Avalonia`, `ImageGen.Core`, `ImageGen.Presentation` не делай.
- Не меняй файлы вне строки «Файлы», не чини соседние находки — они в отдельных задачах.
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: что изменено, команды и результаты, отличия от плана.

## Отчёт

## Комментарии

### 2026-11-09, claude-bdd-plan
Создано вместе с 252 по просьбе владельца. План ждёт одобрения.
