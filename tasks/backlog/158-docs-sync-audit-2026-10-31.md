---
id: 158
status: todo
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

## План

## Отчёт

## Комментарии
