---
id: 024
status: approved
assignee:
claimed:
created: 2026-10-27
---

# Отмена генерации и прогона

## Описание
Отмена одной генерации и всего прогона из UI (F5.4). Общий queued-путь и remote-cancel при удалении уже проектируются в 121/129; эта задача добавляет пользовательскую команду, её состояние и гонки с результатом.

Зависит от: 121, 129.
Среда: Linux или Windows; обе UI-оболочки.

Ручная проверка 016 не блокирует код. Статус `todo`: план уточняется по итоговым контрактам 121/129, реализация только после одобрения. Отмена локального ожидания не гарантирует остановки провайдера или отсутствия платы.

## Критерии готовности
- Можно запросить отмену одной активной генерации и всех активных генераций прогона; завершённые результаты не меняются.
- `CancelOutcome.Cancelled` и `CancellationRequested` различаются в UI; сообщение не обещает остановку, если провайдер её не гарантирует.
- После подтверждённой локальной отмены terminal state не откатывается в Succeeded, поздний файл не остаётся осиротевшим. Результат, уже зафиксированный до отмены, не удаляется.
- Повторная команда идемпотентна; отмена одного задания не затрагивает остальные; ошибка remote cancel не скрывается как успешная отмена.
- Статусы/ошибки сохраняются и публикуются через общий механизм GenerationService; секреты и тела провайдеров не попадают в operational log.

## Анализ
В коде есть `IQueuedImageProvider.CancelAsync`, `CancelOutcome` и `JobState.Cancelled`; пользовательского API GenerationService и кнопок нет. 121 решает cancel при удалении сессии, где после stop вообще запрещены записи; пользовательская отмена отличается: сессия жива и нужно сохранить terminal/cancellation-requested состояние. Нельзя механически вызвать StopSessionAsync для отмены одного задания.

После 129 все image-провайдеры используют единый queued-путь. Будущий план обязан опираться на его RunningJob/handle/submit settlement и сериализацию terminal-state из 121/113, без второй независимой таблицы активных заданий. Сигнал CancellationRequested не равен гарантированному Cancelled: watch продолжается по контракту провайдера, пока не выбран и документирован явный режим прекращения локального ожидания.


Уточнение 2026-10-31 (claude-audit, по коду `b4d00d9`): 121 и 129 выполнены. В `GenerationService` активные задания — `_running` (`RunningJob`: свой `CancellationTokenSource`, связанный с `SessionJobState.StopSource`, опубликованный handle, `SubmitSettled`, однократный `TryBeginRemoteCancel`). `StopSessionAsync` останавливает всю сессию и запрещает дальнейшие записи — для пользовательской отмены не подходит. `CancelRemoteJobBestEffortAsync` уже вызывает `CancelAsync` ровно один раз. `ApplyUpdateAsync` переводит `JobState.Cancelled` в `GenerationStatus.Cancelled`.

## План
Сначала падающий тест, затем код (ub-mode). Два этапа, отдельные PR.

**Этап A. Ядро.**
1. `GenerationService.CancelGenerationAsync(generationId, ct)` → `GenerationCancelResult { NotActive, Cancelled, CancellationRequested, CancelFailed }`; `CancelRunAsync(runId, ct)` → список результатов по каждой активной генерации прогона (снимок `_running` по RunId). Чужой/несуществующий id → `NotActive`.
2. Флаг `RunningJob.UserCancelRequested` (атомарно, повторный вызов возвращает текущий исход без второго remote cancel).
3. Ветки:
   - до submit (нет handle): отменить токен задания → генерация `Cancelled` (`CompletedAt`), ноль удалённых вызовов;
   - во время submit: дождаться `SubmitSettled`; если handle пришёл — как ниже, если submit упал — исход по ошибке submit;
   - есть handle: `CancelAsync` с отдельным ограниченным токеном (таймаут провайдера). `Cancelled`/`AlreadyCompleted`/`NotFound` — локальное ожидание прекращается, статус `Cancelled` (если ещё не терминальный). `CancellationRequested` — статус остаётся активным, наблюдение продолжается, карточка помечается «cancel requested» (новое поле `Generation.CancelRequestedAt`, миграция `NNNN_generation_cancel_requested.sql`); ошибка `CancelAsync` → `CancelFailed`, наблюдение продолжается.
4. Гонка результата и отмены: точка выбора — сериализованная запись терминального состояния (`PersistStateWithRetryAsync`). Если результат уже записан — `NotActive`, файл и Succeeded остаются. Если отмена записала `Cancelled` первой — поздно пришедший результат не сохраняется в файл (проверка флага перед `files.SaveAsync`), состояние не откатывается.
5. Слот провайдера освобождается по terminal, как сейчас; при `CancellationRequested` слот держится до terminal (честный лимит).

**Этап B. Интерфейс.**
1. `GenerationItemViewModel.CancelCommand` (виден для Queued/Running, недоступен при уже отправленной отмене), `RunItemViewModel.CancelRunCommand` (виден, если есть активные). Тексты: «Cancelled», «Cancel requested — the provider may still finish and charge», «Could not cancel». Без обещания остановки у провайдера.
2. Разметка: кнопка в карточке и в заголовке прогона в обеих оболочках, `SemanticProperties`/`AutomationProperties`.

Тесты Core (подменённые провайдеры и временные задержки): cancel до слота, во время submit с поздним handle, каждый `CancelOutcome`, исключение `CancelAsync`, «результат победил»/«отмена победила», нет позднего файла, повторная отмена (один remote cancel), отмена прогона с одним сбоем среди нескольких, восстановление после перезапуска с `CancelRequestedAt`. Data: roundtrip нового поля. Presentation: видимость/состояния команд и тексты.

Проверки: Core.Tests, Data.Tests, Presentation.Tests, Avalonia.Tests, сборка MAUI Android, `dotnet format`, `scripts/check.sh`. Документы: `docs/specs/chat-sessions.md` (S4 — отмена), `docs/specs/image-client.md` F5.4, `docs/architecture.md` (поток «Отмена»).

Файлы: `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `GenerationCancelResult.cs` (новый), `ImageGen/ImageGen.Core/Domain/Generations/Generation.cs`, `ImageGen/ImageGen.Data/Migrations/NNNN_generation_cancel_requested.sql` (новый), `ImageGen/ImageGen.Data/Repositories/RunRepository.cs`, `ImageGen/ImageGen.Presentation/PageModels/GenerationItemViewModel.cs`, `RunItemViewModel.cs`, `ChatPageModel.cs`, `AppResources.resx/.cs`, `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, тесты Core/Data/Presentation/Avalonia.

## Отчёт

## Комментарии

### 2026-10-29, claude-536f
Заблокировано: Детализация после задачи 016 (ручная проверка у владельца). Задача остаётся без плана до снятия блокера; код не пишется.

### 2026-10-29, claude-audit
Аудит 2026-10-29: часть описания устарела. В ядре уже есть отмена задания провайдера с очередью: `IQueuedImageProvider.CancelAsync`, `JobState.Cancelled`, обработка в `GenerationService` (`ApplyUpdateAsync`). Нет отмены из UI и отмены для OpenRouter (он синхронный, см. задачу 102). При детализации учесть.

### 2026-10-29, codex-backlog-review
Ревью по поручению владельца: устаревшая блокировка ожиданием 016 снята; скелет уже существует. Задача возвращена в `todo` для подготовки одобряемого плана. Текущие границы и критерии уточнены по коду и спецификации; это не одобрение реализации. Исторические комментарии выше сохранены.

### 2026-10-31, claude-audit
План готов, статус planned. Допущения: при `CancellationRequested` наблюдение продолжается до ответа провайдера (результат, пришедший после запроса отмены, но до подтверждения, сохраняется — плата уже списана); новое поле `CancelRequestedAt` переживает перезапуск.

### 2026-10-31, владелец (в чате, записал claude-audit)
План одобрен вместе с допущениями из комментария выше.
