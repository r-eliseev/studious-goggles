---
id: 156
status: approved
assignee:
claimed:
created: 2026-10-31
---

# Выбор модели ассистента только из OpenRouter

## Описание
Найдено при аудите 2026-10-31 (`docs/log/2026-10-31-completed-tasks-audit.md`). Документация и код расходятся, какая сторона права, неизвестно: сначала нужен ответ владельца.

- Документация: ADR 0009, п. 3 и 6 — `openAiCompatible` (картинки и чат) и `anthropicCompatible` (только чат) подключаются через `providers.json`; `docs/architecture.md`, «Внешние системы» — «Другие OpenAI-compatible API (`chat/completions`) и Anthropic Messages API (чат) подключаются через `providers.json`». Правило слоёв (`docs/architecture.md`, «Правила слоёв») — код вне `Providers/` и клиентов провайдеров не знает, какой провайдер отвечает ассистенту.
- Ядро это поддерживает: `ChatModelCatalogService.ListAssistantModelsAsync(providerId, …)`, `AssistantService` берёт клиент по `session.Settings.AssistantModel.ProviderId` (`ProviderRouter.GetChatClient`).
- Интерфейс — нет: `ImageGen/ImageGen.Presentation/PageModels/SessionModelsPageModel.cs:226-230` запрашивает модели ассистента только у `ProviderIds.OpenRouter` и строит `ModelRef(ProviderIds.OpenRouter, …)`. Чат-модель провайдера из `providers.json` выбрать на экране моделей сессии нельзя.
- Together по решению владельца не предлагается как модель ассистента (ADR 0017, `docs/architecture.md`); это ограничение нужно сохранить.

Зависит от: —
Среда: Linux или Windows (логика в `Presentation`, тесты на Linux).

## Критерии готовности
- Есть решение владельца: вкладка «Assistant model» показывает чат-модели всех настроенных провайдеров, поддерживающих чат (кроме image-only, например Together), либо остаётся только OpenRouter.
- Если всех провайдеров: модели сгруппированы и подписаны провайдером, как картинки (S5.5, S5.7 `docs/specs/chat-sessions.md`); сохраняется пара provider id + model id; провайдер без ключа или с ошибкой каталога не ломает список остальных.
- Если только OpenRouter: ADR 0009 и `docs/architecture.md` явно говорят, что чат-провайдеры из конфига в интерфейсе не выбираются (новый ADR или пометка в существующем).
- Тесты `Presentation` на состав списка моделей ассистента; поведение одинаково в MAUI и Avalonia.

## Анализ
Решение владельца 2026-10-31: модели ассистента — из всех настроенных chat-провайдеров.

Что есть в коде (на `c6f8948`):
- Чат умеют `OpenRouterClient`, `OpenAiCompatibleClient` и `AnthropicCompatibleClient` (`IAssistantChatClient`); `AssistantService` уже берёт клиент по `AssistantModel.ProviderId`, `ModelRef` с префиксом провайдера сохраняется (`ModelRefJsonConverter`). Запуск чата не надо менять.
- **Главный пробел:** у `OpenAiCompatibleClient.ListChatModelsAsync` и `AnthropicCompatibleClient.ListChatModelsAsync` всегда пустой список. Значит, даже если убрать `ProviderIds.OpenRouter` из `SessionModelsPageModel`, моделей других провайдеров не появится, а `ChatModelCatalogService.IsImageInputAsync`/`ContextLengthAsync` для них дают «картинки не принимает» и 32 000 токенов.
- Откуда взять список: ADR 0009 п. 9 уже отказался от `/models` OpenAI-совместимых API для картинок (не описывает возможности). Для чата то же: `/models` не говорит о tools, контексте и картинках на входе, а у Anthropic-совместимых `GET /models` не проверен (`docs/status.md`). Поэтому список чат-моделей — в конфиге, по образцу `imageModels`: новое поле `chatModels`. Это изменение схемы конфига → новый ADR 0024 (дополняет 0009, его не переписывает).
- Together: `kind: openAiCompatible`, то есть технически `IAssistantChatClient`. Сейчас он не предлагается только потому, что UI спрашивает лишь OpenRouter. После задачи он не предлагается, потому что во встроенном `builtin-providers.json` у него нет `chatModels` (ADR 0017 сохраняется). Пользовательская запись `together` с `chatModels` — осознанный выбор пользователя, по общим правилам override.
- `ChatModelCatalogService` кэширует список по провайдеру на всю жизнь процесса; `ProviderException` при загрузке → `null` и запись в журнал. Для конфигурационных моделей ключ не нужен, для OpenRouter без ключа — пустой список, как сейчас.
- Интерфейс: `SessionModelsPageModel.LoadAsync` (строки 226–230) спрашивает только OpenRouter; `AssistantModelOptionViewModel` не показывает провайдера, в отличие от `ModelOptionViewModel.ProviderText` (S5.7). Группы по производителю — `ModelVendors` по префиксу id, разные провайдеры могут попасть в одну группу (S5.5).
- `SaveAsync` уже сохраняет прежнюю модель ассистента, если в списке ничего не выбрано (`?? session.Settings.AssistantModel`) — модель удалённого из конфига провайдера не теряется молча.
- First-run (`SessionsPageModel`, ключ OpenRouter) не меняется: модель по умолчанию остаётся OpenRouter. Сессия с моделью другого провайдера без ключа получает обычную ошибку `InvalidKey` в чате (S2.5).
- `Refresh` сейчас перечитывает только каталог картинок; кэш чат-моделей не сбрасывается. В эту задачу не входит: для конфигурационных моделей перечитывать нечего.

Пересечения: задача 157 правит обработчики ошибок в том же `SessionModelsPageModel` (строки 159, 239), 158 — `docs/architecture.md`. Порядок: 158 → 156 → 157, при rebase сохранить обе правки.

## План
1. **ADR 0024 «Чат-модели провайдеров в конфиге»** (`docs/decisions/0024-configured-chat-models.md`, статус «предложено» до одобрения плана, затем «принято»): поле `chatModels` для `openAiCompatible` и `anthropicCompatible`; OpenRouter берёт список из API, как сейчас; Together встроен без `chatModels`; альтернативы — `/models` провайдера (нет tools/контекста), отдельный флаг `chat: false` (не нужен: отсутствие списка уже значит «не чат»).
2. **Core, конфиг.**
   - Новый тип `Providers/ConfiguredChatModel` (record): `Id`, `Name` (по умолчанию id), `SupportsImageInput` (по умолчанию false), `ContextLength` (необязательно, > 0).
   - `ProviderConfig` получает `ChatModels` (необязательный параметр в конце, по умолчанию пустой список) — существующие вызовы конструктора не ломаются.
   - `ProviderConfigLoader`: `chatModels` в списке разрешённых полей; разбор как у `imageModels`, неизвестные поля записи отвергаются по имени; ошибка формы → `ProviderConfigException` с провайдером и полем.
   - `ProviderRegistry`: `chatModels` только у `openAiCompatible` и `anthropicCompatible`; id моделей уникальны в провайдере, непустые; `contextLength` > 0.
3. **Core, клиенты.** `OpenAiCompatibleClient.ListChatModelsAsync` и `AnthropicCompatibleClient.ListChatModelsAsync` возвращают `Config.ChatModels` как `ChatModel` с `SupportsTools = true` (модель в списке ассистента считается умеющей tools; цена неизвестна) без HTTP-запроса и без ключа.
4. **Core, маршрутизатор.** `IProviderRouter.ChatProviderIds`: провайдеры, чей клиент реализует чат (`openRouter`, `openAiCompatible`, `anthropicCompatible`) в порядке реестра; `ChatModelCatalogService.ListAllAssistantModelsAsync` обходит их, сбой одного провайдера пропускается с Warning без текста исключения (детали Debug, ADR 0022), результат — пары (provider id, `ChatModel`), сортировка по имени, затем provider id. Существующий `ListAssistantModelsAsync(providerId)` остаётся.
5. **Presentation.**
   - `SessionModelsPageModel.LoadAsync` строит `_assistants` из `ListAllAssistantModelsAsync`; `ModelRef` — из provider id строки, `ProviderIds.OpenRouter` отсюда уходит.
   - `AssistantModelOptionViewModel` получает display name провайдера: `ProviderText` = «DisplayName (id)» и то же в `AutomationName`, как у `ModelOptionViewModel`.
   - Группировка, поиск и раскрытие — существующие (`ModelVendors`, S5.5), без новых правил.
6. **Оболочки.** `ImageGen.Avalonia/Views/SessionModelsView.axaml` и `ImageGen.Maui/Controls/SessionModelsView.xaml`: в строке модели ассистента под названием — `ProviderText`, как у строк картинок; `x:DataType` и семантика сохраняются.
7. **Документы.** `docs/specs/chat-sessions.md` S1.3 (выбор из моделей всех chat-провайдеров с tools) и S5.7 (провайдер и у моделей ассистента); `docs/architecture.md` («Внешние системы», `ConfiguredChatModel` в раскладке); ADR 0009 не переписывать — ссылка на 0024 через новый ADR; пример `chatModels` в `docs/knowledge/` рядом с описанием `providers.json` (если раздела нет — в ADR 0024).

Тест-кейсы (сначала падающий тест, ub-mode):
- Загрузчик: `chatModels` у `openAiCompatible` и `anthropicCompatible` разбирается, значения по умолчанию; неизвестное поле записи, пустой id, повтор id, `contextLength` ≤ 0 → `ProviderConfigException`; `chatModels` у `openRouter`/`fal` → ошибка реестра.
- Встроенный конфиг: у `together` нет chat-моделей → Together не попадает в список ассистента.
- Клиенты: `ListChatModelsAsync` возвращает модели конфига без HTTP-вызова (stub handler не вызывается) и без ключа.
- `ChatModelCatalogService`: OpenRouter + configured → объединённый список; сбой OpenRouter (нет ключа) не скрывает конфигурационные модели; `IsImageInputAsync`/`ContextLengthAsync` берут значения из конфига.
- `SessionModelsPageModel`: на вкладке есть модели двух провайдеров с подписью провайдера; выбор модели другого провайдера сохраняет `ModelRef(providerId, modelId)`; сохранённая модель отсутствующего провайдера не теряется при Save без выбора; одинаковые имена разных провайдеров различаются.
- Avalonia headless: строка ассистента показывает провайдера.

Проверка: тесты Core, Data, Presentation, Avalonia; `dotnet format … --verify-no-changes` для изменённых проектов; MAUI — сборка `-f net10.0-android` и CI (Windows); `scripts/check.sh`. Ручная проверка вкладки в MAUI и Avalonia — пункты в 016 и 046.

Файлы: `docs/decisions/0024-configured-chat-models.md` (новый), `ImageGen/ImageGen.Core/Providers/ConfiguredChatModel.cs` (новый), `ImageGen/ImageGen.Core/Providers/ProviderConfig.cs`, `ImageGen/ImageGen.Core/Providers/ProviderConfigLoader.cs`, `ImageGen/ImageGen.Core/Providers/ProviderRegistry.cs`, `ImageGen/ImageGen.Core/Providers/IProviderRouter.cs`, `ImageGen/ImageGen.Core/Providers/ProviderRouter.cs`, `ImageGen/ImageGen.Core/OpenAiCompatible/OpenAiCompatibleClient.cs`, `ImageGen/ImageGen.Core/AnthropicCompatible/AnthropicCompatibleClient.cs`, `ImageGen/ImageGen.Core/Services/ChatModelCatalogService.cs`, `ImageGen/ImageGen.Presentation/PageModels/SessionModelsPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/AssistantModelOptionViewModel.cs`, `ImageGen/ImageGen.Avalonia/Views/SessionModelsView.axaml`, `ImageGen/ImageGen.Maui/Controls/SessionModelsView.xaml`, тесты в `ImageGen.Core.Tests` (Providers, OpenAiCompatible, AnthropicCompatible, Services), `ImageGen.Presentation.Tests/PageModels/SessionModelsPageModelTests.cs`, `ImageGen.Avalonia.Tests`, `docs/specs/chat-sessions.md`, `docs/architecture.md`, `tasks/v0.1/016-skeleton-manual-check.md`, `tasks/backlog/046-desktop-manual-check.md`.

## Отчёт

## Комментарии
### 2026-10-31, claude-audit
Вопрос владельцу: показывать ли на вкладке «Assistant model» чат-модели всех настроенных chat-провайдеров (`openAiCompatible`, `anthropicCompatible`) или выбор ассистента намеренно только из OpenRouter? До ответа задача не планируется. Вопрос также записан в `docs/status.md`, «Открытые вопросы».
### 2026-10-31, владелец (в чате, записал claude-audit)
Ответ на вопрос: показывать чат-модели **всех** настроенных chat-провайдеров. Действует первый вариант критериев готовности; вариант «только OpenRouter» снят. Image-only провайдеры (Together) по-прежнему не предлагаются (ADR 0017). Задача остаётся `todo`: нужны «Анализ» и «План».

### 2026-10-31, claude-audit
План готов, статус planned. Допущения, которые владелец может переопределить:
1. Список чат-моделей других провайдеров задаётся в `providers.json` (`chatModels`), а не запрашивается у API провайдера — как `imageModels` (ADR 0009 п. 9). Без этого моделям неоткуда взяться: клиенты сейчас возвращают пустой список.
2. Модель из `chatModels` считается умеющей tools; цена у таких моделей не показывается (неизвестна).
3. Together остаётся без чат-моделей во встроенном конфиге; пользователь может добавить их сам через override.
4. `Refresh` кэш чат-моделей не сбрасывает (вне объёма).

### 2026-10-31, владелец (в чате, записал claude-audit)
План одобрен.
