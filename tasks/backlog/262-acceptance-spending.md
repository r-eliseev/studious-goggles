---
id: 262
status: planned
assignee:
claimed:
created: 2026-11-09
---

# Приёмочные сценарии: расходы

## Описание
Автоматизировать раздел «Расходы» каталога: SPD-01 … SPD-05. Файл `Features/Spending.feature`, тег `@spending`.

Зависит от: 253, 257 (шаги прогона).
Среда: Linux или Windows (headless).

## Критерии готовности
- SPD-01 … SPD-05 в `.feature`; каждый зелёный или `@ignore @bug-NNN` с задачей.
- Данные расходов создаются настоящими прогонами с ценой из фейка (`usage.cost`), а не записью в БД.
- SPD-01: две генерации с ценами 0.014 и 0.03 → итог за день 0.044 (формат — по `SpendingPageModel.TotalText`, культура инвариантная), группы по провайдеру и модели (`Groups`).
- SPD-02: генерация без `usage.cost` → не в сумме, учтена в `UnknownOperationsText`.
- SPD-03: часовой пояс и «сейчас» задаются `FakeTimeProvider` (`SetLocalTimeZone` при необходимости); прогон в воскресенье, затем «сейчас» — понедельник → в `Week` не входит, в `Month` входит (если тот же месяц).
- SPD-04: порог выключен по умолчанию; после установки порога ниже оценки следующего прогона чат показывает предупреждение до отправки (как именно — по коду `ChatPageModel` и F8.4).
- SPD-05: месячный лимит (`MonthlyLimitText`, `SaveSettingsAsync`) сохраняется после `Restart`.
- Статусы SPD-* в каталоге обновлены.

## Анализ
- `ImageGen/ImageGen.Presentation/PageModels/SpendingPageModel.cs` (`SpendingPeriod` Day/Week/Month, `Groups`, `TotalText`, `UnknownOperationsText`, `MonthlyLimitText`, `ExpensiveRunThresholdText`, `SaveSettingsAsync`; принимает `TimeProvider`), `ImageGen/ImageGen.Presentation/Spending/SpendingService.cs`.
- Вход: `SettingsPageModel.OpenSpendingAsync`.
- Модульные тесты: `ImageGen/ImageGen.Presentation.Tests/Spending/` — сверить ожидания.
- Спецификация: `docs/specs/image-client.md` F8.1–F8.4.

## План
По одному сценарию SPD-01 → SPD-05. Шаг «Дано сейчас <дата время> <пояс>» — в `CommonSteps` (если ещё нет после 255).

Файлы: `ImageGen/ImageGen.Acceptance.Tests/Features/Spending.feature`, `Steps/SpendingSteps.cs` (новый), `Steps/CommonSteps.cs`, `docs/specs/acceptance-scenarios.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj
dotnet test --project ImageGen/ImageGen.Acceptance.Tests/ImageGen.Acceptance.Tests.csproj -- --filter-trait "Category=spending"
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
