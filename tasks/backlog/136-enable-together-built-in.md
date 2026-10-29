---
id: 136
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Включить Together AI как встроенный image provider

## Описание
После Together wire support 132 и generic key UI 135 добавить built-in provider/model list. Это завершает пользовательское подключение без ручного providers.json. Chat assistant Together не включается: configured client не даёт verified tool/context/image capability catalog.

Зависит от: 093, 132, 135.
Среда: Linux или Windows; optional paid smoke owner.

## Критерии готовности
- builtin provider id `together`, kind openAiCompatible, base `https://api.together.ai/v1/`, response base64, output png, timeout 180.
- Initial models: `black-forest-labs/FLUX.1.1-pro`, `black-forest-labs/FLUX.2-dev`, `black-forest-labs/FLUX.2-flex`, `black-forest-labs/FLUX.2-pro`; exact case preserved; SupportsSeed true per official parameter matrix; text-to-image only.
- Together appears in Settings key list and image model catalog with provider label; no key in config/repo.
- Without key startup works; selecting model gives existing InvalidKey. With valid key mocked generation sends/decodes correct contract.
- Prices documented: exact units/rates and `+ varies`; UI does not present variable minimum as exact actual cost. Generation CostUsd remains unknown until provider supplies actual usage or separate estimator exists.
- Existing built-in order/tests/docs updated; Together not offered as assistant model.

## Анализ
Official catalog requires positive balance. Price snapshot 2026-10-29: FLUX1.1 Pro $0.04/MP; FLUX2 Dev $0.0154+ per image; Flex/Pro $0.03+. Current task093 price model cannot represent «from/varies» reliably, so builtin configured models keep Prices empty/unknown; knowledge doc holds source rates.

## План
Для Haiku/Luna: JSON/config/catalog/docs, no new ProviderKind/client. Do not add Together chat models.

1. Add provider entry to builtin-providers.json with fields from criteria and four imageModels, name, supportsSeed true, maxImagesPerRequest=1 because app issues one generation/request and initial scope no references.
2. Update ProviderConfigLoader built-in tests exact provider count/order/config/models. Composition test resolves OpenAiCompatible connection and model catalog ProviderId=together.
3. Presentation tests after135: Settings row/key route, model rows provider label; first-run only OpenRouter.
4. Mocked end-to-end Core generation Together: key secret name, POST path/body, b64 PNG saved, status/cost unknown.
5. Update docs/knowledge/together-ai.md from132: built-in enabled, date/prices remain snapshot/live unverified. Update architecture external systems, product/spec active provider wording, status remove «task not created» and list live smoke unknown.
6. Update README task118 dependency/content so Together listed built-in only after this task.
7. Optional owner live smoke cheapest chosen model; if official model id removed or response differs, blocked/follow-up, no silent model substitution.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Providers/builtin-providers.json`, `ImageGen/ImageGen.Core.Tests/Providers/ProviderConfigLoaderTests.cs`, `ImageGen/ImageGen.Core.Tests/Providers/ProviderRouterTests.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`, `ImageGen/ImageGen.Data.Tests/CompositionTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SettingsPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionModelsPageModelTests.cs`, `ImageGen/ImageGen.Avalonia.Tests/Flows/StartFlowTests.cs`, `docs/knowledge/together-ai.md`, `docs/architecture.md`, `docs/product.md`, `docs/specs/image-client.md`, `docs/status.md`, `tasks/backlog/118-root-readme.md`

## Отчёт

## Комментарии
