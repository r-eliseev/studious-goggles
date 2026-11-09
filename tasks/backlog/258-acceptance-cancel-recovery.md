---
id: 258
status: planned
assignee:
claimed:
created: 2026-11-09
---

# Приёмочные сценарии: отмена генераций и восстановление после перезапуска

## Описание
Автоматизировать раздел «Отмена и восстановление» каталога: CAN-01 … CAN-05. Файл `Features/CancelAndRecovery.feature`, тег `@cancel`.

Зависит от: 253, 257 (шаги прогона и удержания ответа).
Среда: Linux или Windows (headless).

## Критерии готовности
- CAN-01 … CAN-05 в `.feature`; каждый зелёный или `@ignore @bug-NNN` с задачей.
- CAN-01: две генерации удержаны; отмена одной (`GenerationItemViewModel.CancelAsync`) → она «отменено», вторая после отпускания — «готово».
- CAN-02: отмена прогона (`RunItemViewModel.CancelRunAsync`) при одной готовой и двух удержанных → готовая без изменений, две — «отменено».
- CAN-03: ответ провайдера приходит до подтверждения отмены (порядок задаётся шагами) → карточка «готово», картинка сохранена.
- CAN-04: удержанная генерация, затем `Restart` мира → карточка «ошибка» с причиной «Interrupted» (по ресурсам). OpenRouter без handle задания — по S3.8 это Interrupted; queued-провайдеры с handle здесь не проверяются.
- CAN-05: время подтверждения отмены (из `FakeTimeProvider`) видно на карточке (`CancellationStatusText`) и после `Restart`.
- Ни один сценарий не ждёт реального времени: таймауты фейка и опросы двигаются `FakeTimeProvider.Advance`, если код опирается на `TimeProvider`; где код ждёт `Task.Delay` без `TimeProvider` (`IAsyncDelay`, `SystemAsyncDelay`) — записать в «Отчёт», при необходимости подменить `IAsyncDelay` через шов мира.
- Статусы CAN-* в каталоге обновлены.

## Анализ
- `ImageGen/ImageGen.Presentation/PageModels/GenerationItemViewModel.cs` (`CancelAsync`, `IsCancelled`, `CancellationStatusText`, `HasCancellationStatus`), `RunItemViewModel.cs` (`CancelRunAsync`).
- Ядро: `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `GenerationCancelResult.cs`, `IAsyncDelay.cs`; восстановление при старте — `ImageGen/ImageGen.Presentation/Services/AppInitializer.cs`.
- Спецификация: `docs/specs/chat-sessions.md` S3.5.1–S3.5.3, S3.8; `docs/specs/image-client.md` F5.4.

## План
По одному сценарию CAN-01 → CAN-05. Для порядка событий в CAN-03 — шаги «провайдер отвечает на генерацию N» и «пользователь отменяет генерацию N» в явной последовательности, без задержек по времени.

Файлы: `ImageGen/ImageGen.Acceptance.Tests/Features/CancelAndRecovery.feature`, `Steps/RunsSteps.cs`, `Steps/CommonSteps.cs`, `Support/AppWorld.cs` (только подмена `IAsyncDelay`, если понадобится), `docs/specs/acceptance-scenarios.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=cancel"
dotnet format ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj --verify-no-changes
bash scripts/check.sh
```
(Фильтр по тегу — если Reqnroll передаёт теги как категории xUnit; как именно — записано в `docs/knowledge/acceptance-tests.md` задачей 252.)

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы», названные тесты и разделы `docs/specs/acceptance-scenarios.md`; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): сначала сценарий в `.feature`, запуск — он падает, потому что шаг не привязан или ожидание не выполнено; затем минимальная привязка; сценарий зелёный. Один сценарий — один коммит (id сценария в сообщении).
- Ожидание сценария берётся из спецификации, не из кода. Если код ведёт себя иначе: не подгоняй сценарий и не чини код — тег `@ignore @bug-NNN`, новая задача `scripts/new-task.sh --publish backlog <имя>` с шагами воспроизведения, статус «расхождение (NNN)» в каталоге.
- Шаги действуют как пользователь: команды моделей страниц, выбор пунктов, навигация. Не вызывай репозитории, сервисы ядра и SQL напрямую, кроме шагов «Дано», которые готовят мир, и проверок «файл существует».
- Новый общий шаг — в `Steps/CommonSteps.cs`; сначала поищи, нет ли уже такого шага с другой формулировкой.
- Не меняй файлы вне строки «Файлы», не переименовывай существующие члены сверх плана, не чини соседние находки — они в отдельных задачах.
- Если код расходится с планом (нет такого метода, другая сигнатура), не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: какие сценарии зелёные, какие `@ignore` и с какими задачами, команды и результаты, отличия от плана. Статусы сценариев в `docs/specs/acceptance-scenarios.md` обнови.

## Отчёт

## Комментарии

### 2026-11-09, claude-bdd-plan
Создано по просьбе владельца распланировать тест-сценарии приложения для исполнения GPT Luna. Сценарии и ожидания — в `docs/specs/acceptance-scenarios.md`. План ждёт одобрения.
