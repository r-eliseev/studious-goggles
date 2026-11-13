---
id: 237
status: blocked
assignee:
claimed:
created: 2026-11-07
---

# Together AI: список моделей картинок из API, а не из встроенного файла

## Описание
Вопрос владельца 2026-11-07 о захардкоженных списках: модели картинок Together AI — пять FLUX, вписанных в `ImageGen/ImageGen.Core/Providers/builtin-providers.json` (`imageModels`), их выдаёт `OpenAiCompatibleClient.ListImageModelsAsync` из конфига. Нужно загружать список из API Together, как у OpenRouter и fal.

Формат ответа API списка моделей Together в документации (https://docs.together.ai, 2026-11-07) не найден: есть таблицы каталога в документации и SDK `client.models.list()`, но описания полей ответа нет. Поэтому первый шаг — живой запрос с ключом Together владельца; без ключа задача не двигается дальше шага 1.

Зависит от: —
Среда: Linux или Windows; живой запрос — с ключом Together AI владельца (бесплатный: список моделей).

## Критерии готовности
- Шаг 1 (факты): `GET https://api.together.xyz/v1/models` (или `https://api.together.ai/v1/models` — как `baseAddress` в `builtin-providers.json`) с ключом — записать в `docs/knowledge/` (новый раздел «Together AI» в `docs/knowledge/media-generation.md` или файл рядом): форма ответа (массив или объект), поля модели (`id`, вид модели — например `type`, имя, цена и её единица), какие модели — картинки. Без ключа — остановиться и записать в «Комментарии», что нужен ключ.
- Если в ответе есть признак модели картинок: `OpenAiCompatibleClient.ListImageModelsAsync` для провайдера с новым признаком конфига `imageModelsFromApi: true` (Together) загружает модели картинок из API (имя, цена если есть, `SupportsSeed` — как для остальных провайдеров без явного признака: `false`, если API его не даёт); `imageModels` в конфиге Together остаётся запасным списком на случай ошибки API (с Warning). Другие OpenAI-совместимые провайдеры из пользовательского `providers.json` — без изменений.
- Если признака модели картинок в ответе нет — список из конфига остаётся, факт записан, задача закрывается с этим выводом.
- Тесты на фикстуре реального ответа (сократить до 3–5 моделей, ключ и личные данные не сохранять): модели картинок отобраны, остальные — нет; ошибка API → список из конфига и Warning.

## Анализ
`ImageGen/ImageGen.Core/OpenAiCompatible/OpenAiCompatibleClient.cs` (`ListImageModelsAsync` из `Config.ImageModels`), `ImageGen/ImageGen.Core/Providers/builtin-providers.json`, тип конфига провайдера (`ProviderConfig`, валидация в `ProviderRegistry` — новое поле должно проходить проверку и попасть в спецификацию конфига `docs/specs/image-client.md`, если там описан формат `providers.json`).

## План
1. Живой запрос и запись фактов (шаг 1).
2. По результату — падающие тесты на фикстуре, признак конфига, загрузка из API с запасным списком; либо закрытие с выводом.
3. Документация конфига.

Файлы: `ImageGen/ImageGen.Core/OpenAiCompatible/OpenAiCompatibleClient.cs`, `ImageGen/ImageGen.Core/Providers/builtin-providers.json`, `ImageGen/ImageGen.Core/Providers/ProviderConfig.cs` (или где описан конфиг; `rg -n "class ProviderConfig" ImageGen`), `ImageGen/ImageGen.Core/Providers/ProviderRegistry.cs`, тесты `ImageGen/ImageGen.Core.Tests/`, `docs/knowledge/media-generation.md`, при необходимости `docs/specs/image-client.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
bash scripts/check.sh
```

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы» и названные тесты; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): падающий тест → запуск, убедиться, что он падает именно по описанной причине → минимальный фикс → тест зелёный.
- Не меняй файлы вне строки «Файлы», не переименовывай существующие члены сверх плана, не чини соседние находки — они в отдельных задачах.
- Если код расходится с планом (нет такого метода, другая сигнатура, тест уже есть), не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: что изменено, команды и результаты, отличия от плана.

## Отчёт

## Комментарии

### 2026-11-13, codex-gpt6
The required live API check cannot proceed because this environment has no Together AI credential (checked environment variable names; no Together key is configured). The plan explicitly requires a live authenticated request as step 1 and says to stop without a key. No request was sent.

Unblock when the owner provides/configures a Together AI key for this session; then perform the documented free model-list request and continue from step 1.


### 2026-11-07, claude-audit
Создано по вопросу владельца о захардкоженных списках (проверено: Together — 5 FLUX в builtin-providers.json). Формат ответа API Together не найден в документации — нужен живой запрос с ключом. План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
Предложение одобрено («Да»); план одобрен ранее данным поручением «заапрувь все таски которые будут».

### 2026-11-13, owner (in the chat, recorded by claude-planner)
Asked whether to add a Together key, postpone or cancel: "Postpone". The task stays `blocked` until the owner provides a Together key; the plan is unchanged.
