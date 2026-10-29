---
id: 132
status: planned
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
- GET models key probe covered. Exact status mapping: 400/413 BadRequest; 401 InvalidKey; 402 InsufficientFunds; 403 Forbidden; 404/503/529 ModelUnavailable; 429 RateLimited; 504/524 Timeout; 500 Unknown.
- Together safety checker documents HTTP 422 but not a unique sanitized marker. Initial mapping remains Unknown, not falsely Moderated; live fixture/follow-up required before Filtered mapping.
- Initial Together scope text-to-image only: prompt/model/n=1/seed; dimensions/model-specific parameters/reference_images explicitly unsupported/dropped until separate capability task.
- Provider response cost is absent in documented image shape: CostUsd remains null; official catalog prices documented, not presented as actual charge.

## Анализ
Official request supports prompt, model, width/height or model-dependent aspect_ratio, n 1–4, steps, seed, negative_prompt, response_format, output_format; base64 gives b64_json, output default JPEG. Existing ConfiguredImageModel cannot express params and client hardcodes PNG, so generic provider-level `imageResponseFormat`/`imageOutputFormat` are the narrow safe bridge. No Together-specific branch/new ProviderKind.

Official 2026-10-29 representative prices: FLUX.1.1 Pro and Kontext Pro $0.04/MP; Kontext Max $0.08/MP; FLUX.2 Dev from $0.0154/image; Flex/Pro from $0.03/image; Max $0.07/MP at 50 steps. Variable/minimum prices are not exact actual cost.

## План
Для Haiku/Luna: mocked contract only; no real key/CI live call; no UI/built-in.

1. ProviderConfig optional `ImageResponseFormat`, `ImageOutputFormat`; loader fields `imageResponseFormat`/`imageOutputFormat`, only openAiCompatible, nonblank safe strings, null default. Update parser/registry tests.
2. OpenAiCompatible Generate body sends configured values. MediaType `image/png` for png, `image/jpeg` for jpeg; unknown configured output rejected at startup, not guessed.
3. Together fixture tests exact URL/auth/body including response/output format, seed/no-seed; b64 bytes/media; GET models probe. ProviderClientBase status tests exact mapping 413,504,524 plus existing codes; 422 Together image fixture remains Unknown and documented until live marker known.
4. Regression generic config without fields retains current body and default media behavior.
5. Add `docs/knowledge/together-ai.md`: sources/date, input/output, selected model table/prices, default URL caveat, text-only initial limitations, live call not performed. `docs/index.md` менять не нужно: каталог `knowledge/` уже индексирован.
6. Add safe `providers.json` example without key to docs/knowledge; config loader test parses it. Do not add builtin entry.
7. Optional owner smoke after merge: one smallest paid request; record only status/media/dimensions/cost. API keys, Authorization headers and secret values are never logged, traced, saved outside secret store or committed at any level. Sanitized request/response content may appear only at Debug per BodyRedactor; key-probe response body is never logged.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Providers/ProviderConfig.cs`, `ImageGen/ImageGen.Core/Providers/ProviderConfigLoader.cs`, `ImageGen/ImageGen.Core/Providers/ProviderRegistry.cs`, `ImageGen/ImageGen.Core/Providers/ProviderClientBase.cs`, `ImageGen/ImageGen.Core/OpenAiCompatible/OpenAiCompatibleClient.cs`, `ImageGen/ImageGen.Core.Tests/Providers/ProviderConfigLoaderTests.cs`, `ImageGen/ImageGen.Core.Tests/Providers/ProviderRegistryTests.cs`, `ImageGen/ImageGen.Core.Tests/Providers/ProviderClientBaseTests.cs` (новый), `ImageGen/ImageGen.Core.Tests/OpenAiCompatible/OpenAiCompatibleClientTests.cs`, `docs/knowledge/together-ai.md` (новый)

## Отчёт

## Комментарии
