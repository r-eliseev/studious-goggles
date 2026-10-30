---
id: 132
status: done
assignee:
claimed:
created: 2026-10-29
---

# Поддержать Together AI contract в OpenAI-compatible client

## Описание
Владелец попросил проверить официальный Together API/цены и создать подключение. Документация подтверждает Bearer + `https://api.together.ai/v1`, `GET /models`, `POST /images/generations`, OpenAI-shaped errors. Важное расхождение: image response default=`url`, а текущий client читает только `b64_json`; Together принимает `response_format="base64"`. Эта задача добавляет generic config knobs и Together contract tests/example, без built-in/UI (135/136).

Источники: [OpenAI compatibility](https://docs.together.ai/docs/inference/openai-compatibility), [image parameters](https://docs.together.ai/docs/inference/images/parameters), [models and pricing](https://docs.together.ai/reference/models), [reference images](https://docs.together.ai/docs/inference/images/reference-images), [error codes](https://docs.together.ai/docs/error-codes). Сведения пересказаны.

Зависит от: 116, 127, 129.
Среда: Linux или Windows.

## Критерии готовности
- Configurable openAiCompatible provider can set image response value `base64` and output `png`; client sends them and reads `data[0].b64_json` as image/png.
- Existing providers with absent knobs keep prior request/response behavior.
- Together example: id together, base URL, timeout, no key in JSON, selected model ids, seed support.
- GET models key probe covered. Exact status mapping: 400/413 BadRequest; 401 InvalidKey; 402 InsufficientFunds; 403 без moderation metadata → Forbidden; 403 с moderation reasons → Moderated; 404/502/503/529 ModelUnavailable; 408/504/524 Timeout; 429 RateLimited; 500 Unknown. MapErrorKind общий для всех провайдеров: существующие 408, 502 и 403-moderation сохраняются.
- Together safety checker documents HTTP 422 but not a unique sanitized marker. Initial mapping remains Unknown, not falsely Moderated; live fixture/follow-up required before Filtered mapping.
- Initial Together scope text-to-image only: prompt/model/n=1/seed; dimensions/model-specific parameters/reference_images explicitly unsupported/dropped until separate capability task.
- Provider response cost is absent in documented image shape: CostUsd remains null; official catalog prices documented, not presented as actual charge.

## Анализ
Official request supports prompt, model, width/height or model-dependent aspect_ratio, n 1–4, steps, seed, negative_prompt, response_format, output_format; base64 gives b64_json, output default JPEG. Existing ConfiguredImageModel cannot express params and client hardcodes PNG, so generic provider-level `imageResponseFormat`/`imageOutputFormat` are the narrow safe bridge. No Together-specific branch/new ProviderKind.

Official 2026-10-29 representative prices: FLUX.1.1 Pro and Kontext Pro $0.04/MP; Kontext Max $0.08/MP; FLUX.2 Dev from $0.0154/image; Flex/Pro from $0.03/image; Max $0.07/MP at 50 steps. Variable/minimum prices are not exact actual cost.

## План
Для Haiku/Luna: mocked contract only; no real key/CI live call; no UI/built-in.

1. ProviderConfig optional `ImageResponseFormat`, `ImageOutputFormat`; loader fields `imageResponseFormat`/`imageOutputFormat`, только openAiCompatible. Добавить к ProviderConfig последние необязательные string? параметры с null default, чтобы старые конструкторы компилировались. Набор разрешённых case-sensitive значений: ImageResponseFormat — null, `base64`, `b64_json`; ImageOutputFormat — null, `png`, `jpeg`. Поле отсутствует/null → прежнее поведение; пустое, пробельное, неверный JSON type, неизвестное значение или поле у другого kind → ProviderConfigException до HTTP. Добавить имена в KnownFields и валидацию и парсеру, и ProviderRegistry (он может создаваться напрямую). Новый ProviderRegistryTests покрывает прямое создание и ошибку формата, loader tests — JSON; не переименовывать существующие тесты.
2. OpenAiCompatible Generate body sends configured values. MediaType `image/png` for png, `image/jpeg` for jpeg; unknown configured output rejected at startup, not guessed.
3. Новые Together-сценарии в существующем OpenAiCompatibleClientTests (config id=together, kind=OpenAiCompatible): exact URL/auth/body including response/output format, seed/no-seed; b64 bytes/media; GET models probe. Новый ProviderClientBaseTests тестирует публичный static MapErrorKind: полная таблица критериев, отдельные 403 с/без reasons, 408 и 502. Это намеренное расширение общего mapping, не Together-specific switch. Через существующие тесты всех клиентов сохранить прежние ошибки; 422 Together image fixture remains Unknown and documented until live marker known.
4. Regression generic config without fields retains current body and default media behavior.
5. Add `docs/knowledge/together-ai.md`: sources/date, input/output, selected model table/prices, default URL caveat, text-only initial limitations, live call not performed. `docs/index.md` менять не нужно: каталог `knowledge/` уже индексирован.
6. Пример JSON в docs/knowledge/together-ai.md: together/OpenAiCompatible, https://api.together.ai/v1/, response_format base64/output png через новые camelCase поля, timeout 180s, только документированная выбранная модель из таблицы. Для теста loader хранить эквивалентную fixture без секрета; не парсить markdown из теста. Built-in entry не добавлять.
7. Создать новый ADR `NNNN-provider-image-formats.md` (следующий свободный номер) с дополнением схемы ADR0009; прежний ADR не переписывать. Обновить docs/knowledge о провайдерах при наличии таблицы полей: enum whitelist, null/default поведение и отсутствие поддержки URL-response.
8. Optional owner smoke after merge: one smallest paid request; record only status/media/dimensions/cost. API keys, Authorization headers and secret values are never logged, traced, saved outside secret store or committed at any level. Sanitized request/response content may appear only at Debug per BodyRedactor; key-probe response body is never logged.

9. Заполнить «Отчёт» и завершить по docs/tasks.md, `scripts/release-task.sh 132`. Без ключа задача закрывается по mocked contract; непроверенный live marker 422/стоимость остаётся явным ограничением.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Providers/ProviderConfig.cs`, `ImageGen/ImageGen.Core/Providers/ProviderConfigLoader.cs`, `ImageGen/ImageGen.Core/Providers/ProviderRegistry.cs`, `ImageGen/ImageGen.Core/Providers/ProviderClientBase.cs`, `ImageGen/ImageGen.Core/OpenAiCompatible/OpenAiCompatibleClient.cs`, `ImageGen/ImageGen.Core.Tests/Providers/ProviderConfigLoaderTests.cs`, `ImageGen/ImageGen.Core.Tests/Providers/ProviderRegistryTests.cs` (новый), `ImageGen/ImageGen.Core.Tests/Providers/ProviderClientBaseTests.cs` (новый), `ImageGen/ImageGen.Core.Tests/OpenAiCompatible/OpenAiCompatibleClientTests.cs`, `docs/knowledge/together-ai.md` (новый), `docs/decisions/NNNN-provider-image-formats.md` (новый)

## Отчёт

Добавлены опциональные `imageResponseFormat` и `imageOutputFormat` для `openAiCompatible` с whitelist-проверкой при разборе JSON и прямой регистрации провайдера. Клиент передаёт указанные форматы, декодирует `data[0].b64_json` и выбирает `image/png` или `image/jpeg`; конфигурации без параметров сохраняют прежнее тело запроса и PNG media type. Общая карта статусов расширена для 413, 404, 504 и 524; 422 оставлен `Unknown`. Добавлены Together contract/key-probe tests, пример конфигурации, справка по моделям/ограничениям и ADR 0023. Живой вызов и списание не выполнялись: тесты используют HTTP заглушки, а `CostUsd` остаётся `null`.

Проверки: Core tests 627/627; `dotnet format` Core и Core.Tests — прошли. `bash scripts/check.sh` запускается при закрытии задачи.

## Комментарии

### 2026-10-29, codex-review
Задача нужна как ограниченное расширение generic-клиента перед включением Together. Уточнены whitelist конфигурации, совместимость старых конструкторов и общий error mapping без потери 408/502/403 moderation; неподтверждённый 422 не классифицируется как фильтрация.
