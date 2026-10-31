---
id: 158
status: approved
assignee:
claimed:
created: 2026-10-31
---

# Сверить документацию с кодом по итогам аудита 2026-10-31

## Описание
Найдено при ревью и аудите 2026-10-31 (`docs/log/2026-10-31-completed-tasks-audit.md`). Только документация: код прав, документы отстали. Где решение за владельцем, это отдельно отмечено.

**`docs/status.md`**
- «Порядок работ»: раздел «Можно брать на реализацию» и «Очереди по зависимостям» перечисляют задачи, которые уже `done` и архивированы (089, 090, 095, 096, 099–101, 103, 104, 106, 110, 112, 115–117, 127, 130, 135, 138, 144–154). Переписать очередь по фактическим статусам (`scripts/list-tasks.sh`).
- «Решения владельца (2026-10-27)», пункт про ADR 0003: «В коде OpenRouter пока синхронный (аудит 2026-10-29, задача 102)» — устарело: `ProviderRouter.cs:41-42` оборачивает OpenRouter и OpenAI-совместимые клиенты в `InMemoryQueuedImageConnection` (задачи 102, 128, 129).
- «Открытые вопросы», первый пункт: «Бесплатная модель только у ассистента: `google/gemma-4-31b-it:free`» — модель ассистента по умолчанию `anthropic/claude-haiku-5.5` (`SessionSettings.cs:10`, задача 049). То же в `docs/specs/chat-sessions.md`, «Открытые вопросы»: «Бесплатная модель используется только для ассистента».
- «Открытые вопросы», пункт про задачу 036: «ответ с URL не поддержан» верно, но `response_format`/`output_format` уже настраиваются (ADR 0023); уточнить формулировку.

**`docs/architecture.md`**
- «Текущая раскладка»: нет типов, которые есть в коде: `Domain/Models` — `ChatModel`, `ModelParameter`, `ParameterKind`; `Domain/Chat` — `ChatAttachment`, `ConversationSummary`; `Domain/Sessions` — `GenerationParameters`; `Domain/Generations` — `ParameterOutcome*`, `ParameterDropReason`; `Providers/` — `ProviderIds`, `ProviderResponseLimits`, `BoundedHttpContentReader`, `BoundedBase64ImageDecoder`; `Fal/` — `FalUrlValidator`; `Presentation/Services` — просмотрщик (`IImageViewerService`, `ImageViewerNavigationState`, `ImageViewerHitTest`), `IFolderLauncher`, `CurrentSessionTracker`; Avalonia и MAUI — `*ImageViewerService`, `*FullScreenController`, `*FolderLauncher`, `MauiSecretPersistence`.
- Абзац «Журнал»: архитектурный тест `Core` перечислен без `Avalonia`, а `CoreDependencyTests.cs:10` его запрещает (раздел «Правила слоёв» указывает верно).
- «Скелет v0.1: хранение»: «Ключ OpenRouter хранится только за `ISecretStore`» — так хранятся ключи всех провайдеров (`SecretNames.ForProvider`, ADR 0009 п. 7).
- «Целевая раскладка»: Fal за контрактом ADR 0003, общий queued lifecycle (ADR 0020) и Together (ADR 0017) уже в коде и в «Текущей раскладке»; путь `Providers/Fal` не совпадает с `Fal/`.
- «Правила слоёв»: в списке каталогов, которые знают провайдера, нет `Fal/`. Задокументированные исключения из правила: `ModelVendors` (префикс `fal-ai`, S5.5), быстрый опрос локального adapter в `GenerationService` (ADR 0020), first-run по ключу OpenRouter. Выбор модели ассистента — задача 156.

**`docs/conventions.md`**
- «Стиль и нейминг»: «Если в корне появится `.editorconfig`» — он уже есть.
- «MAUI и MVVM»: `FireAndForgetSafeAsync(errorHandler)` — фактическая сигнатура без обработчика, ошибка пишется в журнал (`Presentation/Utilities/TaskExtensions.cs:15`).
- «Зависимости»: «когда проектов станет больше одного»; «Тесты»: «Тестовые проекты заводятся задачами 001 и 003» — формулировки времён начала проекта.

**Прочее**
- `docs/index.md`: в строке `knowledge/` нет `together-ai.md`.
- `docs/knowledge/dev-environment.md`, «Логи», Avalonia Windows: «до задачи 048 headless-тесты Avalonia пишут сюда же» — 048 выполнена, тесты работают во временной папке.
- `docs/specs/debug-trace.md`, «Что не пишется»: «Тело не обрезается» не упоминает лимиты ADR 0022 (`ProviderResponseLimits`): при включённом трейсе тело больше лимита не пишется, запрос завершается ошибкой. Для проверки ключа и `SkipResponseBody` код не пишет запись `http-response` целиком, включая статус (`LoggingHttpHandler.cs:79,124`), а спецификация говорит только о теле и заголовках.
- `docs/specs/image-client.md`, «Открытые вопросы», решения владельца: платформы «MAUI … и Web API + Vue» без Avalonia (ADR 0006).
- ADR 0022 и 0023 — статус «предложено», но код их выполняет (задачи 112, 127, 132, 150, 152), а `docs/conventions.md` ссылается на 0022 как на действующее правило. Принять или нет — решает владелец; вопрос записан в `docs/status.md`. Статус ADR без решения владельца не менять.

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- Все пункты «Описания» исправлены в документах или записаны вопросом в `docs/status.md`; код не меняется.
- ADR 0022 и 0023 переведены в «принято» только после ответа владельца.
- `scripts/check.sh` проходит.

## Анализ
Все пункты «Описания» перепроверены на `c6f8948` + архивация 2026-10-31: код прав, документы отстали. Решения владельца 2026-10-31 уже записаны (ADR 0022/0023 приняты, модель ассистента — задача 156). Код не меняется.

Пересечения: 156 будет править `docs/architecture.md` и `docs/specs/chat-sessions.md` (выбор модели ассистента), 157 — `docs/conventions.md`. Эта задача делается первой; в 156/157 при rebase сохранить её правки. Здесь пункты про выбор модели ассистента не трогать — их закроет 156.

## План
Правки только по существу, без переформатирования (`docs/tasks.md`, «Общие файлы»). Каждый пункт сверить с кодом в момент правки — если код изменился, писать по коду.

1. **`docs/status.md`.**
   - «Порядок работ»: переписать «Можно брать на реализацию», «Очереди по зависимостям» и «Планирование, реальные блокеры» по `scripts/list-tasks.sh`: убрать архивированные задачи, оставить живые (021–024, 028, 030–033, 046, 088, 105, 131, 134, 139–141, 148, 156–158) с их зависимостями из файлов задач. Абзацы аудитов 2026-10-30/31 оставить как историю.
   - «Решения владельца (2026-10-27)», пункт ADR 0003: заменить «В коде OpenRouter пока синхронный…» на ссылку на ADR 0020 и общий in-memory adapter.
   - «Открытые вопросы»: модель по умолчанию у ассистента — `anthropic/claude-haiku-5.5` (бесплатная `google/gemma-…:free` — только как вариант выбора, если так в каталоге; иначе убрать); пункт задачи 036 — уточнить, что `response_format`/`output_format` настраиваются (ADR 0023), URL-ответ по-прежнему не поддержан.
2. **`docs/specs/chat-sessions.md`**, «Открытые вопросы»: убрать «Бесплатная модель используется только для ассистента», оставить факт об отсутствии бесплатных моделей картинок.
3. **`docs/architecture.md`.**
   - «Текущая раскладка»: дописать типы из «Описания» в соответствующие строки (Domain, Providers, Fal, Presentation/Services, Avalonia/Services, Maui/Services), без пересказа их работы.
   - Абзац «Журнал»: добавить `Avalonia*` в перечень архитектурного теста `Core`.
   - «Скелет v0.1: хранение»: «Ключи провайдеров хранятся только за `ISecretStore` (`SecretNames.ForProvider`)».
   - «Целевая раскладка»: убрать из строки `ImageGen.Core` уже реализованное (Fal, общий queued lifecycle, Together); оставить «Catalog с фильтрами; расходы».
   - «Правила слоёв»: добавить `Fal/` в список; перечислить задокументированные исключения: first-run по ключу OpenRouter, `ModelVendors` (префикс `fal-ai`, S5.5), быстрый опрос локального adapter (ADR 0020).
4. **`docs/conventions.md`.** `.editorconfig` есть и главный; сигнатура `FireAndForgetSafeAsync()` — ошибка в журнал через `TaskExtensions.Logger`; «единый список в `Directory.Packages.props`»; тестовые проекты — четыре, перечислены в `AGENTS.md`.
5. **`docs/index.md`**: `together-ai.md` в строку `knowledge/`.
6. **`docs/knowledge/dev-environment.md`**, «Логи»: убрать фразу «до задачи 048 headless-тесты Avalonia пишут сюда же».
7. **`docs/specs/debug-trace.md`**, «Что не пишется»: тело пишется целиком в пределах `ProviderResponseLimits` (ADR 0022), при превышении лимита запрос завершается ошибкой без частичной записи; для проверки ключа и запросов с `SkipResponseBody` запись `http-response` не создаётся.
8. **`docs/specs/image-client.md`**, решения владельца в «Открытых вопросах»: дописать «пересмотрено 2026-10-28: Avalonia-десктоп (ADR 0006)», исходный текст решения не переписывать.

Проверка: `scripts/check.sh`, `scripts/audit.sh` (раздел 5 — только ADR 0019); выборочная сверка ссылок на файлы и строки.

Файлы: `docs/status.md`, `docs/specs/chat-sessions.md`, `docs/architecture.md`, `docs/conventions.md`, `docs/index.md`, `docs/knowledge/dev-environment.md`, `docs/specs/debug-trace.md`, `docs/specs/image-client.md`.

## Отчёт

## Комментарии
### 2026-10-31, владелец (в чате, записал claude-audit)
ADR 0022 и 0023 приняты; их статус уже изменён на «принято (2026-10-31)», вопрос в `docs/status.md` перенесён в «Решения владельца». Пункт про ADR из «Описания» выполнен, остальные пункты остаются.

### 2026-10-31, claude-audit
План готов, статус planned. Выполнять первой из 156–158: остальные две правят те же документы.

### 2026-10-31, владелец (в чате, записал claude-audit)
План одобрен.
