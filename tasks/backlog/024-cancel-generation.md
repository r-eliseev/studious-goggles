---
id: 024
status: todo
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

## План
Этап подготовки плана после 121/129, код пока не писать:
1. Назвать `CancelGenerationAsync` и `CancelRunAsync` в GenerationService, тип возвращаемого outcome для UI и правила nonexistent id/session ownership. Найти все пути до submit, во время submit, polling, download/save и после terminal commit.
2. Описать атомарную точку выбора победителя «cancel или result». Для каждого CancelOutcome задать persisted state, продолжение watch, текст UI и освобождение slot. CancellationRequested не превращать автоматически в Cancelled.
3. Перенести безопасную публикацию handle/SubmitSettled из 121, использовать отдельный bounded token remote call; не перезапускать submit и не отменять caller token так, чтобы потерять handle. Для cancel до submit гарантировать zero remote calls.
4. Для отмены прогона использовать snapshot его активных GenerationId и отменять каждую независимо; один сбой не пропускает остальные и отображается отдельным исходом. Повторный клик не даёт повторных remote cancel.
5. Определить команды/CanExecute в GenerationItemViewModel и RunItemViewModel, тексты AppResources и обе разметки. Проверки: queued before slot, late handle, every CancelOutcome, provider exception, result wins/cancel wins, late file cleanup, repeated cancel, один failed cancel среди нескольких.
6. Подготовить точную строку Файлы, разбить Core state machine и UI на маленькие последовательные части. После согласования поставить `planned` и ждать одобрения. Будущие проверки Core.Tests, Data.Tests для persisted-state сценариев, Presentation.Tests, Avalonia.Tests, MAUI Android build, format, `bash scripts/check.sh`; завершение по `docs/tasks.md`.

Файлы для анализа: `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core/Providers/CancelOutcome.cs`, `ImageGen/ImageGen.Core/Storage/IRunRepository.cs`, `ImageGen/ImageGen.Presentation/PageModels/GenerationItemViewModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/RunItemViewModel.cs`, `tasks/backlog/121-cancel-remote-job-on-session-delete.md`, `tasks/backlog/129-unify-generation-queued-path.md`. Финальный список зависит от реализованных контрактов prerequisite-задач.

## Отчёт

## Комментарии

### 2026-10-29, claude-536f
Заблокировано: Детализация после задачи 016 (ручная проверка у владельца). Задача остаётся без плана до снятия блокера; код не пишется.

### 2026-10-29, claude-audit
Аудит 2026-10-29: часть описания устарела. В ядре уже есть отмена задания провайдера с очередью: `IQueuedImageProvider.CancelAsync`, `JobState.Cancelled`, обработка в `GenerationService` (`ApplyUpdateAsync`). Нет отмены из UI и отмены для OpenRouter (он синхронный, см. задачу 102). При детализации учесть.

### 2026-10-29, codex-backlog-review
Ревью по поручению владельца: устаревшая блокировка ожиданием 016 снята; скелет уже существует. Задача возвращена в `todo` для подготовки одобряемого плана. Текущие границы и критерии уточнены по коду и спецификации; это не одобрение реализации. Исторические комментарии выше сохранены.
