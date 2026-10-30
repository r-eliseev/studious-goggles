---
id: 136
status: approved
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
- Initial candidates: `black-forest-labs/FLUX.1.1-pro`, `black-forest-labs/FLUX.2-dev`, `black-forest-labs/FLUX.2-flex`, `black-forest-labs/FLUX.2-pro`; exact case preserved; SupportsSeed true per official parameter matrix; text-to-image only. Кандидат, официально подтверждённый как удалённый, исключается без замены. При отсутствии такого подтверждения сохраняется исходный список; недоступность каталога/баланса не означает удаления модели.
- Together appears in Settings key list and image model catalog with provider label; no key in config/repo.
- Without key startup works; selecting model gives existing InvalidKey. With valid key mocked generation sends/decodes correct contract.
- Prices documented: exact units/rates and `+ varies`; UI does not present variable minimum as exact actual cost. Generation CostUsd remains unknown until provider supplies actual usage or separate estimator exists.
- Existing built-in order/tests/docs updated; Together not offered as assistant model. Если официально удалены все кандидаты, сам provider и key UI сохраняются, его imageModels пуст и каталог пуст; другие модели автоматически не подставляются.

## Анализ
Official catalog requires positive balance. Price snapshot 2026-10-29: FLUX1.1 Pro $0.04/MP; FLUX2 Dev $0.0154+ per image; Flex/Pro $0.03+. Current task093 price model cannot represent «from/varies» reliably, so builtin configured models keep Prices empty/unknown; knowledge doc holds source rates.

## План
Для Haiku/Luna: JSON/config/catalog/docs, no new ProviderKind/client. Do not add Together chat models.

1. Add provider entry to builtin-providers.json with fields from criteria and исходными четырьмя imageModels (с вычитанием только официально подтверждённых удалённых ID по шагу7), name, supportsSeed true, maxImagesPerRequest=1 because app issues one generation/request and initial scope no references. Никаких fallback/substitute model ids; пустой список после подтверждённого удаления всех кандидатов допустим.
2. Добавить Together в конец существующего списка (openrouter, fal, together), чтобы сохранить порядок предыдущих provider. ProviderConfigLoader built-in tests проверяют ровно этот count/order, форматные knobs, case-sensitive исходные четыре model id либо их оставшееся после официально подтверждённого удаления подмножество; каждое исключение имеет источник в отчёте. Отдельно проверить допустимость пустого imageModels без потери provider/key UI. Сохранить override semantics: пользовательский entry id=together полностью заменяет built-in по действующим правилам, дубликаты внутри одного файла отклоняются. Composition test разрешает generic openAiCompatible connection и model catalog ProviderId=together; после129 он представлен queued wrapper, поэтому не требовать GetConnection(id) is OpenAiCompatibleClient: проверять публичный IQueuedImageProvider и HTTP contract. Провайдерский wire test остаётся на OpenAiCompatibleClient из132.
3. Presentation tests after135: Settings row/key route, model rows provider label; first-run only OpenRouter.
4. В Core GenerationServiceTests подключить registry/router как после129 с in-memory queued adapter, fake ISecretStore и StubHttpMessageHandler без живого ключа. Scenario: fake key under SecretNames.ForProvider(together), один POST images/generations с base64/png/seed, ответ с маленьким валидным PNG → дождаться WhenIdleAsync, один SaveAsync с теми же bytes/media image/png, terminal Succeeded, CostUsd=null; пустой ключ → Failed/InvalidKey и HTTP не вызывается. Data.Tests/CompositionTests только проверяет реальную DI-композицию и регистрацию каталога, без живой сети. Не ожидать конкретный внутренний wire-тип от router после129.
5. Update docs/knowledge/together-ai.md from132: built-in enabled, date/prices remain snapshot/live unverified. Update architecture external systems, product/spec active provider wording, status remove «task not created» and list live smoke unknown. Обязательно AGENTS.md: в116 Together был выбранным/manual-config, теперь отметить built-in для image-only; не оставить утверждение «Together не встроен». Оставить ограничения по assistant, reference input/parameters, actual cost и непроверенный live ответ явными. Документацию менять по фактической реализации, не копировать устаревшие абсолютные даты как текущую цену.
6. Task118 уже зависит от136; файл задачи118 не менять, она создаст README по фактическому built-in catalog после merge136. Если README к моменту реализации уже существует, обновить в нём фактический список провайдеров и ограничения Together; иначе новый README здесь не создавать.
7. Optional owner live smoke — только после его явного разрешения на платный запрос и с собственноручным вводом ключа. Без запроса live результат остаётся «не проверено», mocked-contract задача завершается. Официально подтверждённое удаление ID (официальный каталог/deprecation announcement с точным case-sensitive id и ссылкой) обрабатывается без вопроса владельцу: исключить только этот id, зафиксировать источник и согласованно обновить fixtures/docs. 401/403/недоступный endpoint/отсутствие balance/неудачный smoke сами по себе не доказывают удаления. Если удалены все четыре, оставить provider/key UI с пустым imageModels и явным отсутствием доступных моделей; не выдумывать замену. Подтверждённое изменение wire contract адаптировать по официальной документации в границах выбранных base64/png/text-to-image возможностей и повторить mocked tests; неподдержанную возможность не рекламировать. Никакой неподтверждённый live-результат не считать пройденной проверкой.
8. Заполнить «Отчёт», перечислить живые проверки/ограничения отдельно; завершить по docs/tasks.md, release-task.sh136 и scripts/check.sh.

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

Файлы: `ImageGen/ImageGen.Core/Providers/builtin-providers.json`, `ImageGen/ImageGen.Core.Tests/Providers/ProviderConfigLoaderTests.cs`, `ImageGen/ImageGen.Core.Tests/Providers/ProviderRouterTests.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`, `ImageGen/ImageGen.Data.Tests/CompositionTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SettingsPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SessionModelsPageModelTests.cs`, `ImageGen/ImageGen.Avalonia.Tests/Flows/StartFlowTests.cs`, `docs/knowledge/together-ai.md`, `docs/architecture.md`, `docs/product.md`, `docs/specs/image-client.md`, `docs/status.md`, `AGENTS.md`, `README.md` (только если уже создан задачей118 к моменту реализации)

## Отчёт

## Комментарии

### 2026-10-29, codex-review
Задача нужна отдельно от generic wire support132 и key UI135. Уточнён queued router после129 и сохранение порядка/override. По согласованию с ревью116 AGENTS.md обязательно переключает Together из manual-config в built-in. Чужой план118 не переписывается; платный smoke остаётся необязательным и требует отдельного разрешения владельца.
