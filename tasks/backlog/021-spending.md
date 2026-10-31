---
id: 021
status: approved
assignee:
claimed:
created: 2026-10-27
---

# Оценка цены и учёт расходов

## Описание
Оценка цены прогона до запуска (F4.4), независимый журнал расходов (ADR 0002, F7.7), экран расходов и предупреждения о месячном лимите (F8.3, F8.4). Нужна: сейчас `Generation.CostUsd` удаляется вместе с сессией, а общей оценки до запуска нет.

Зависит от: —
Среда: Linux или Windows; обе UI-оболочки.

Код скелета уже есть; ручная проверка 016 не нужна для подготовки плана. Статус `todo`: можно брать на анализ и планирование, реализация только после отдельного одобрения плана.

## Критерии готовности
- Удаление сессии или картинки не уменьшает сумму расходов.
- Повторное сохранение состояния одной генерации не удваивает расход; неизвестная цена отличается от нулевой, оценка — от факта.
- До запуска показана суммарная оценка для фактических моделей × числа запусков; неизвестная часть явно обозначена.
- Месячный лимит и порог дорогого запуска предупреждают перед submit, без скрытого запрета или обещания точного счёта провайдера.
- Экран показывает день/неделю/месяц и группировку по провайдеру/модели; интерфейс общий по поведению в MAUI и Avalonia.

## Анализ
Сверено с кодом: `Core/Domain/Generations/Generation.cs` хранит `CostUsd`; `Core/Domain/Models/ImageModel.cs` не содержит универсальной цены, поэтому нельзя просто умножить несуществующее свойство модели. Цена чат-ответа есть в `AssistantChatResult.CostUsd`, но не сохраняется в `ChatMessage`; сводки и описания имеют собственные цены. План должен явно назвать охват журнала и правило учёта неизвестных цен, а не молча выдавать сумму картинок за все расходы.

Точки записи: `Core/Services/GenerationService.cs`, `Data/Repositories/RunRepository.cs`; удаление: `Core/Services/SessionService.cs`. Нужен независимый ledger без каскадного удаления по SessionId/GenerationId; уникальный идентификатор операции и транзакционная идемпотентность. Одной вставки в UI после GenerationUpdated недостаточно: событие может не доставиться.

Размер для Haiku/Luna слишком большой для одной реализации. При планировании предложить отдельные задачи: (1) ledger/миграция/backfill, (2) получение оценки и preflight-предупреждения, (3) экран/настройки. Сначала одобрить контракты между частями. Новые номера и зависимости назначаются владельцем ревью; не начинать все части одним патчем.


Уточнение 2026-10-31 (claude-audit, по коду `b4d00d9`). Решение владельца: журнал учитывает **все платные вызовы** с известной ценой.
- Источники цены сейчас: `Generation.CostUsd` (TEXT `generations.cost_usd`), `ImageDescription.CostUsd`, `ConversationSummary.CostUsd`. Сводка хранится одна на сессию и перезаписывается — цена прежних сводок теряется. Цена ответа ассистента (`AssistantChatResult.CostUsd`) **не сохраняется нигде**.
- Все эти строки удаляются каскадом вместе с сессией (`ON DELETE CASCADE`), поэтому журналу нужна своя таблица без внешних ключей.
- Цена до запуска: каталог OpenRouter даёт `ImageModelPrice` (единицы image / megapixel / token / request); у fal цены нет, у Together — только `CostUsd = null`. Для megapixel оценке нужен размер кадра; связь `resolution`/`aspect_ratio` с пикселями подтверждена только заглушкой (ADR 0011).
- Ассистент запускает генерацию без подтверждения (S2.2, решение владельца) — предупреждение для его прогонов можно показать только до отправки сообщения.

## План
Три этапа, каждый — отдельный PR в этой задаче, по порядку A → B → C. Сначала падающий тест, затем код (ub-mode).

**Этап A. Журнал расходов (Core + Data).**
1. Домен `Domain/Expenses/Expense` (`OperationId` строка, `Kind` — `ExpenseKind { Image, AssistantReply, Summary, Description, Embedding }`, `ProviderId`, `ModelId`, `CostUsd` decimal, `OccurredAt`, `SessionId?`). Записываются только известные цены: `null` — не расход, а «неизвестно», считается отдельно (п. 4).
2. `Storage/IExpenseRepository`: `ListAsync(from, to)`; `SumUnknownAsync(from, to)` не нужен — неизвестные считаются по исходным таблицам, пока они живы (п. 4).
3. Миграция `NNNN_expenses.sql`: таблица `expenses(operation_id TEXT PRIMARY KEY, kind, provider_id, model_id, cost_usd TEXT NOT NULL, occurred_at TEXT NOT NULL, session_id TEXT NULL)` **без FK**, индекс по `occurred_at`; колонка `chat_messages.cost_usd TEXT NULL`. Перенос старых данных той же миграцией: `INSERT OR IGNORE … SELECT` из `generations` (operation id `gen:<id>`, время — `completed_at`), `image_descriptions` (`desc:<id>`), `conversation_summaries` (`sum:<session>:<covers_up_to>`).
4. Запись в той же транзакции, что и источник (идемпотентно, `INSERT … ON CONFLICT(operation_id) DO UPDATE`): `RunRepository` при сохранении генерации с ценой; `ImageDescriptionRepository.AddAsync`; `ConversationSummaryRepository` при upsert (`sum:<session>:<covers_up_to>` — новая сводка = новая операция); `ChatMessageRepository` при добавлении ответа ассистента с ценой (`msg:<id>`). `AssistantService` сохраняет `reply.CostUsd` в `ChatMessage.CostUsd`.
5. Тесты Data (временная SQLite): повторное сохранение терминального состояния не удваивает; удаление сессии не меняет сумму; цена `null` не пишется, `0` пишется; перенос старой БД (миграция поверх данных 0009) без дублей при повторном запуске; две сводки подряд — две записи. Core: `AssistantService` сохраняет цену ответа.

**Этап B. Оценка до запуска и предупреждения.**
1. `Services/CostEstimator`: `Estimate(models × runs, parameters)` → `CostEstimate(KnownUsd, UnknownModels, IsApproximate)`. Image/request — цена × N; megapixel — по размеру из таблицы `resolution × aspect_ratio` (по умолчанию 1K ≈ 1 MP), помечается «≈»; token — неизвестно (нет числа токенов вывода). Нет цены — модель в `UnknownModels`, не ноль.
2. Настройки `Presentation/Spending/SpendingSettings` в `spending.json` в папке данных (по образцу `AppearanceSettingsFile`): месячный лимит и порог «дорогого» запуска, оба по умолчанию выключены; неверный файл → выключены + запись в журнал.
3. `SpendingService.CheckAsync(estimate)` → `None | ExpensiveRun | MonthlyLimitReached` по сумме журнала за текущий календарный месяц (локальное время).
4. Ручные действия (Repeat, New seed, Edit, Other model) при предупреждении просят подтверждение `IDialogService.ConfirmAsync`; отказ → ни прогона, ни submit. Прогоны ассистента: строка над полем ввода чата «Next run ≈ $X (+N unknown)» и предупреждение о лимите — до отправки сообщения; запрета нет.
5. Тесты: N запусков и несколько моделей; неизвестная часть; megapixel помечен как приближённый; порог/лимит; отказ подтверждения → `GenerationService` не вызывается.

**Этап C. Экран расходов.**
1. `SpendingPageModel` (маршрут `spending`, вход из Settings): период день/неделя (ISO, с понедельника)/месяц в локальном времени, итог, группы по провайдеру и модели, число операций с неизвестной ценой за период (из живых исходных таблиц, с пометкой «по существующим сессиям»), поля лимита и порога.
2. Разметка в Avalonia (`Views/SpendingView.axaml`) и MAUI (`Pages/SpendingPage.xaml`), `x:DataType`, семантика, `CultureInfo` для денег — как в `ImagePriceText`.
3. Тесты Presentation: группировка, границы периодов при смене дня/месяца, переход DST, пустой журнал.

Документы: `docs/specs/image-client.md` (F4.4, F7.7, F8.3–F8.4 — что реализовано), `docs/architecture.md` (журнал, правило «удаление не трогает расходы» из ADR 0002 — снять «Известную проблему» в `docs/status.md`).

Проверки каждого этапа: Core.Tests, Data.Tests, Presentation.Tests, Avalonia.Tests, сборка MAUI `-f net10.0-android`, `dotnet format` изменённых проектов, `scripts/check.sh`.

Файлы: этап A — `ImageGen/ImageGen.Core/Domain/Expenses/Expense.cs`, `ExpenseKind.cs` (новые), `ImageGen/ImageGen.Core/Storage/IExpenseRepository.cs` (новый), `ImageGen/ImageGen.Core/Domain/Chat/ChatMessage.cs`, `ImageGen/ImageGen.Core/Services/AssistantService.cs`, `ImageGen/ImageGen.Data/Migrations/NNNN_expenses.sql` (новый), `ImageGen/ImageGen.Data/Repositories/ExpenseRepository.cs` (новый), `RunRepository.cs`, `ChatMessageRepository.cs`, `ImageDescriptionRepository.cs`, `ConversationSummaryRepository.cs`, `ImageGen/ImageGen.Data/DataServiceCollectionExtensions.cs`, тесты Data/Core. Этап B — `ImageGen/ImageGen.Core/Services/CostEstimator.cs`, `CostEstimate.cs`, `ImageGen/ImageGen.Presentation/Spending/SpendingSettings.cs`, `SpendingSettingsFile.cs`, `SpendingService.cs` (новые), `ChatPageModel.cs`, `GenerationItemViewModel.cs`, `ModelPickerPageModel.cs`, `AppResources.resx/.cs`, обе разметки чата. Этап C — `ImageGen/ImageGen.Presentation/PageModels/SpendingPageModel.cs`, `Routes.cs`, `SettingsPageModel.cs`, `ImageGen/ImageGen.Avalonia/Views/SpendingView.axaml(.cs)`, `ImageGen/ImageGen.Maui/Pages/SpendingPage.xaml(.cs)`, регистрация в `AppServices.cs`/`MauiProgram.cs`, документы.

## Отчёт

## Комментарии

### 2026-10-29, claude-536f
Заблокировано: Детализация после задачи 016 (ручная проверка MAUI под Windows, у владельца). Без неё нет кода скелета, по которому писать план. Задача остаётся без плана до снятия блокера; код не пишется.

### 2026-10-29, codex-backlog-review
Ревью по поручению владельца: устаревшая блокировка ожиданием 016 снята; скелет уже существует. Задача возвращена в `todo` для подготовки одобряемого плана. Текущие границы и критерии уточнены по коду и спецификации; это не одобрение реализации. Исторические комментарии выше сохранены.

### 2026-10-31, claude-audit
Планы готовы, статус planned. Допущения, которые владелец может поменять: (1) megapixel-оценка по таблице размеров, 1K ≈ 1 MP, с пометкой «≈»; (2) лимит и порог по умолчанию выключены; (3) для прогонов ассистента — предупреждение над полем ввода, без подтверждения (S2.2); (4) неделя ISO с понедельника, локальное время; (5) неизвестные цены считаются только по живым сессиям.

### 2026-10-31, владелец (в чате, записал claude-audit)
План одобрен вместе с допущениями из комментария выше.
