---
id: 036
status: planned
assignee:
claimed:
created: 2026-10-27
---

# Провайдер моделей: адрес и тип API в конфиге

## Описание
Вынести базовый адрес OpenRouter (`OpenRouterClient.BaseAddress`) и другие параметры провайдера в конфигурацию, чтобы подключать любые OpenAI- и Anthropic-совместимые API без правки кода.

Исходная точка: `IModelProvider` (`ImageGen/ImageGen.Core/Providers/IModelProvider.cs`) и `OpenRouterClient` (`ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs`). Источник: ревью PR #1.

Среда: Linux или Windows (в MAUI меняются только модели страниц, они тестируются в MauiLinked; сборку MAUI проверяет CI на Windows).

## Критерии готовности

## Анализ

Это общий слой для 025 (fal), 026 (Replicate) и самой 036. Раздел «План» ниже общий: 025 и 026 ссылаются на него и описывают только своё.

### Что в коде сейчас (main, проверено 2026-10-28)
- `OpenRouterClient.BaseAddress` — `public static readonly Uri` (`ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs`). Адрес и таймаут 3 минуты задаются в `AddImageGenCore()` (`ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`).
- Один провайдер на весь контейнер: `IModelProvider` и `IImageGenerator` в DI разрешаются в `OpenRouterClient`. `ApiKeyService` и `GenerationService` получают одну реализацию.
- Адрес — не единственная привязка к OpenRouter. Провайдера не знают ни модель, ни генерация, ни сессия:
  - `ImageModel` и `Generation` не имеют провайдера. Первичный ключ `image_models` — `id` (`ImageGen/ImageGen.Data/Migrations/0001_initial.sql`); у fal и Replicate одинаковые id моделей возможны.
  - `ModelCatalogService` вызывает `ImageModelRepository.ReplaceAllAsync`, который удаляет всю таблицу. Обновление одного провайдера стёрло бы каталог остальных.
  - `SessionSettings.ImageModelIds` — список строк без провайдера. В `settings_json` уже лежат сессии со значениями вида `black-forest-labs/flux.2-klein-4b`, их нужно читать.
  - `SecretNames` — одна константа `openrouter_api_key`; `ApiKeyService` работает с одним ключом.
- Тип API сейчас один: OpenRouter (`POST /images` синхронно, `chat/completions` для ассистента). Тип API хранится для провайдера целиком, а не для адреса.

### Совместимость API (проверено 2026-10-28)
- Anthropic Messages API (`https://platform.claude.com/docs/en/api/messages`; старый адрес `docs.anthropic.com` перенаправляет туда). Прочитаны первые 100 000 из примерно 1,29 млн символов страницы. Выход с картинкой не описан: в ответе перечислены text, tool_use, thinking. Значит, Anthropic-совместимый тип не даёт генерации картинок и относится только к чату ассистента. Полный ответ не проверен.
- Чат ассистента сейчас в формате OpenAI (`IOpenRouterChatClient`, `OpenRouterClient.CompleteChatAsync`, `chat/completions`).
- OpenAI `POST /v1/images/generations`: страница `platform.openai.com/docs/api-reference/images/create` вернула 403, формат не проверен. Формат ответа OpenRouter (`data[].b64_json`) уже есть в коде.

### Связанные задачи и ограничения
- 035 (миграции из папки): новая миграция `provider_id` попадает в `ImageGen/ImageGen.Data/Migrations.cs`. Если 035 не сделана раньше, миграция добавляется в явный список.
- 037 (время жизни `HttpClient`): та же регистрация клиентов в `AddImageGenCore`. Предлагается закрыть в 036.
- SQLite не меняет первичный ключ через `ALTER`: `image_models` пересоздаётся с ключом `(provider_id, id)`.
- Строки «Среда» в задаче нет. По правилу `docs/tasks.md` задача не MAUI, достаточно Linux (`dotnet test` для Core и Data). Предлагается строка `Среда: Linux или Windows (нужен .NET 10 SDK)` (вопрос 6).
- Задача 016 (ручная проверка скелета) в статусе `planned`, задачи 012–015 в `in-progress`. Описания 025, 026, 036 велят детализировать после 016. План написан по коду main и предварительный (вопрос 7).
- В задаче нет «Критерии готовности» (раздел пустой). План их не придумывает; предлагаемые критерии в «Комментариях».

## План

### Порядок (общий для 025, 026, 036)
1. 035 (миграции из папки), если владелец согласен: иначе миграция `provider_id` вписывается в явный список.
2. 036 (этот план): общий слой. Поведение OpenRouter не меняется.
3. Владелец принимает ADR 0003 (статус «предложено» сейчас, блокер для 025).
4. 025 (контракт `IImageProvider` и fal).
5. 026 (Replicate): зависит от контракта и хранения handle из 025.
6. 037 закрывается в шаге 2.

### Типы (`ImageGen/ImageGen.Core/Providers/`, новые)
- `ProviderKind` (enum): `OpenRouter` сейчас; `Fal` добавляется в 025, `Replicate` в 026. `OpenAiCompatible` и Anthropic-тип не добавляются до ответа на вопрос 2.
- `ProviderConfig` (record): `Id` (стабильная строка: `openrouter`, `fal`, `replicate`), `Kind`, `DisplayName`, `BaseAddress` (`Uri`, нормализуется с завершающим `/`), `Timeout`. Секретов в конфиге нет: только имя секрета, производное от `Id`.
- `ProviderRegistry`: держит список `ProviderConfig`, отдаёт `Get(id)` и `All`. Проверяет при создании: `Id` непустой и уникальный; `Kind` известен; адрес абсолютный и `https` (кроме `localhost`, вопрос 4). Ошибка — исключение с понятным текстом без значений ключей.
- `ModelRef` (record): `ProviderId`, `ModelId`. Строковая форма для хранения — по новому ADR (вопрос 3).

### Домен и хранение
- `ImageModel.ProviderId` и `Generation.ProviderId`.
- `SessionSettings.ImageModelIds`: список строк → список `ModelRef`. Строка без префикса читается как провайдер `openrouter`. Массово `settings_json` не переписывается: новая форма пишется при следующем сохранении сессии.
- `SecretNames.For(providerId)`: для `openrouter` возвращает прежнее `openrouter_api_key`, чтобы сохранённый ключ не потерялся; для остальных — `provider.<id>.api_key`.
- Миграция `NNNN_providers.sql` (номер — следующий свободный при реализации): `generations.provider_id TEXT NOT NULL DEFAULT 'openrouter'`; `image_models` пересоздаётся с первичным ключом `(provider_id, id)`; старые строки получают `openrouter`.

### Сервисы
- `ApiKeyService(ProviderRegistry, ISecretStore, ProviderRouter)`: `HasKeyAsync(providerId)`, `SaveAsync(providerId, key)`, `RemoveAsync(providerId)`, `GetMaskedKeyAsync(providerId)`. Проверка ключа идёт через провайдера `providerId`.
- `ProviderRouter` (новый): `Get(providerId)` возвращает реализацию по `Kind` (в 036 одна: OpenRouter). Неизвестный или не настроенный `Id` → `ProviderException(ErrorKind.ModelUnavailable, "Provider <id> is not configured.")`.
- `ModelCatalogService.GetImageModelsAsync(providerId, forceRefresh)`: `ImageModelRepository.ReplaceAllAsync(providerId, models)` заменяет только строки этого провайдера.
- `GenerationService`: вместо одного `IImageGenerator` получает `ProviderRouter` и вызывает провайдера по `generation.ProviderId`.

### Регистрация (DI)
- `AddImageGenCore` принимает список `ProviderConfig`; встроенный список (OpenRouter с адресом `https://openrouter.ai/api/v1/` и таймаутом 3 минуты) используется по умолчанию (вопрос 1).
- Для каждого провайдера `AddHttpClient(config.Id)` с `BaseAddress` и `Timeout` из конфига.
- Синглтоны не держат `HttpClient`: клиент берётся из `IHttpClientFactory.CreateClient(id)` на каждый запрос. Так закрывается 037.
- Константа `OpenRouterClient.BaseAddress` остаётся значением по умолчанию во встроенном списке; клиент её не читает.

### Потоки данных
1. Хост передаёт список `ProviderConfig`; `ProviderRegistry` валидирует его при старте.
2. Обновление каталога для провайдера P: вызвать `P.ListImageModels`. При ошибке и непустом кэше P вернуть кэш P. Иначе в одной транзакции удалить строки P и вставить новые.
3. Генерация: `generation.ProviderId` → `ProviderRouter.Get` → `GenerateAsync`. Остальной поток не меняется.

### Крайние случаи
- Два провайдера с одним `Id` → старт падает с текстом об ошибке конфига.
- `ProviderId` сессии отсутствует в конфиге → генерация `Failed` с `ModelUnavailable`, сеть не вызывается.
- Адрес без завершающего `/` → нормализуется, иначе `images/models` уйдёт не туда.
- Одинаковый `ModelId` у двух провайдеров → две строки каталога, конфликта нет.
- Ключ fal или Replicate не затирает и не читает ключ OpenRouter.

### Тест-кейсы (условие → результат)
- Пустой `Id` или дубликат `Id` в конфиге → ошибка регистрации.
- `BaseAddress` относительный или `http://` не для `localhost` → ошибка регистрации.
- Неизвестный `Kind` при чтении конфига → ошибка с именем `Kind`.
- `BaseAddress` без `/` → запрос `images/models` уходит на `<адрес>/images/models`.
- JSON сессии со строкой `"black-forest-labs/flux.2-klein-4b"` → `ModelRef(openrouter, black-forest-labs/flux.2-klein-4b)`.
- Обновление каталога `openrouter` не удаляет строки другого провайдера в тестовом наборе.
- Ошибка `ListImageModels` при непустом кэше провайдера → возвращается кэш этого провайдера.
- Сохранение ключа `fal` не меняет статус ключа `openrouter`.
- Ключ `openrouter` читается по имени `openrouter_api_key` (сохранённый до миграции ключ работает).
- Генерация с `ProviderId = X` вызывает провайдера X, второй провайдер не вызывается (подмены через NSubstitute).
- Генерация с незнакомым `ProviderId` → `Failed`, `ModelUnavailable`, HTTP-подмена не вызвана.
- Новая миграция на базе с данными: строки `image_models` и `generations` получают `openrouter`; два провайдера с одинаковым `id` модели уживаются.
- Существующие тесты `OpenRouterClientTests` проходят; меняется только конструктор.

### Команды проверки
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
scripts/check.sh
```
Оболочка MAUI (`ImageGen.Maui`) использует `ApiKeyService`, `SessionSettings` и `ModelCatalogService`: её собрать только на Windows (`dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0`).

Файлы: `ImageGen/ImageGen.Core/Providers/ProviderKind.cs` (новый), `ImageGen/ImageGen.Core/Providers/ProviderConfig.cs` (новый), `ImageGen/ImageGen.Core/Providers/ProviderRegistry.cs` (новый), `ImageGen/ImageGen.Core/Providers/ProviderRouter.cs` (новый), `ImageGen/ImageGen.Core/Domain/Models/ModelRef.cs` (новый), `ImageGen/ImageGen.Core/Domain/Models/ImageModel.cs`, `ImageGen/ImageGen.Core/Domain/Generations/Generation.cs`, `ImageGen/ImageGen.Core/Domain/Sessions/SessionSettings.cs`, `ImageGen/ImageGen.Core/Domain/Keys/SecretNames.cs`, `ImageGen/ImageGen.Core/OpenRouter/OpenRouterClient.cs`, `ImageGen/ImageGen.Core/Services/ApiKeyService.cs`, `ImageGen/ImageGen.Core/Services/ModelCatalogService.cs`, `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core/Storage/IImageModelRepository.cs`, `ImageGen/ImageGen.Core/CoreServiceCollectionExtensions.cs`, `ImageGen/ImageGen.Data/Migrations/NNNN_providers.sql` (новый), `ImageGen/ImageGen.Data/Repositories/ImageModelRepository.cs`, `ImageGen/ImageGen.Data/Repositories/RunRepository.cs`, `ImageGen/ImageGen.Data/DataServiceCollectionExtensions.cs` (если меняется регистрация), `ImageGen/ImageGen.Core.Tests/Providers/ProviderRegistryTests.cs` (новый), `ImageGen/ImageGen.Core.Tests/Services/ApiKeyServiceTests.cs`, `ImageGen/ImageGen.Core.Tests/Services/ModelCatalogServiceTests.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`, `ImageGen/ImageGen.Core.Tests/OpenRouter/OpenRouterClientTests.cs`, `ImageGen/ImageGen.Core.Tests/Domain/SessionSettingsTests.cs`, `ImageGen/ImageGen.Data.Tests/Repositories/ImageModelRepositoryTests.cs`, `ImageGen/ImageGen.Data.Tests/Repositories/RunRepositoryTests.cs`, `ImageGen/ImageGen.Data.Tests/SqliteDatabaseTests.cs`, `ImageGen/ImageGen.Maui/PageModels/SessionModelsPageModel.cs`, `ImageGen/ImageGen.Maui/PageModels/ApiKeyPageModel.cs`, `docs/decisions/0005-provider-config-and-model-ref.md` (новый ADR, статус «предложено»), `docs/architecture.md` (раскладка: ProviderRegistry, ProviderRouter, ModelRef)

## Отчёт

## Комментарии

### 2026-10-28, claude-qnrqyt
План написан (статус `planned`), реализация не начата. Вопросы владельцу (нумерация совпадает со ссылками в плане):

1. Где живёт конфиг провайдеров? Предлагаю встроенный список `ProviderConfig` в ядре (OpenRouter, fal, Replicate с адресами); пользовательские подключения (F1.4, P1) позже. Файла конфига в v1 нет. Согласны?
2. «Anthropic-совместимый» тип: Messages API не генерирует картинки (в прочитанной части `platform.claude.com/docs/en/api/messages` выхода-картинки нет; прочитано 100 000 из ~1,29 млн символов). Какой смысл у этого типа в задаче? Варианты: (а) только для чата ассистента, тогда нужен отдельный интерфейс чата; (б) убрать из критериев. OpenAI-совместимый тип для картинок (`/v1/images/generations`) не проверен: страница вернула 403.
3. Строковая форма `ModelRef` в `settings_json` и в таблицах: предлагаю `provider:model` (например `openrouter:black-forest-labs/flux.2-klein-4b`), строка без префикса = `openrouter`. Или объект `{"provider":..., "model":...}`?
4. Адрес провайдера: только `https`, кроме `localhost`? Нужны ли не-https адреса (локальный совместимый сервер)?
5. Порядок: 035, затем 036, затем 025 и 026 (после принятия ADR 0003). 037 закрываем в 036 (та же регистрация клиентов). Согласны?
6. Строки «Среда» нет. Предлагаю `Среда: Linux или Windows (нужен .NET 10 SDK)`: задача касается только ядра и хранилища. Ок?
7. Задача 016 в статусе `planned`, задачи 012–015 в `in-progress`. Описания велят детализировать после 016. Перепроверить план после 016, или владелец принимает план по коду main сейчас?
8. «Критерии готовности» пустые. Предлагаю добавить: (а) `ProviderConfig` валидируется при старте, невалидный конфиг не запускает приложение; (б) генерация OpenRouter ведёт себя как до задачи (тесты `OpenRouterClientTests` проходят); (в) миграция старых сессий и каталога без потери данных. Раздел «Критерии» не трогаю: решение за владельцем.

### 2026-10-28, ревью (claude, Windows)
Общий порядок по пересечениям файлов и таблиц (предложение ревью, решает владелец): 035 → 036 → 020 → 017 → 018 → 019 → 027; 025 и 026 после 036 и принятия ADR 0003. Причины: 036 меняет `ImageModel`, `image_models` (ключ `(provider_id, id)`), `SessionSettings.ImageModelIds` (`ModelRef`) и `GenerationService`; 020 вводит полный список параметров модели, из которого 018 берёт лимит референсов; 017 вводит каталог чат-моделей, `IImageFileStore.ReadAsync` и `ImageDataUrl` для 018, 019 и 027. Номера миграций в планах заменены на `NNNN_<смысл>.sql` (правило в `docs/tasks.md`), строка «Среда» добавлена в «Описание».
Задача 037 (время жизни `HttpClient`) действительно закрывается здесь, если клиент берётся из `IHttpClientFactory` на каждый запрос; тогда 037 стоит отменить при мердже 036.
