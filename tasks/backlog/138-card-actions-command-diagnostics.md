---
id: 138
status: in-progress
assignee: codex-luna
claimed: 2026-10-30T09:12:56Z
created: 2026-10-29
---

# Карточка MAUI: общие тесты команды и диагностика меню

## Описание
Общая часть 088, не требующая Windows: закрепить контракт ShowActionsCommand и добавить диагностику ShellDialogService. Причину клика на Windows исследует 088.

Зависит от: —
Среда: Linux или Windows; для сборки MAUI нужен Android SDK.

## Критерии готовности
- Terminal-карточка из истории доступна для ShowActions; Running недоступна, переход к terminal уведомляет CanExecuteChanged.
- Отмена/ошибка ChooseAsync не оставляет команду в IsRunning.
- Debug фиксирует начало/конец ChooseAsync и число вариантов; текст title/options/answer и исключения не попадают в Warning.
- Нет изменений жестов, меню Windows или поведения команды без воспроизводимого дефекта.

## Анализ
Выделено из шагов 1–2 задачи 088: общий код/тесты можно проверить на Linux. ILogger<ShellDialogService> доступен через MAUI DI. При ошибке диагностика пишет Debug с exception и Warning только с именем операции; повторный выброс сохраняет исходное поведение. Пересечение тестов карточки с096 — добавлять отдельные тесты, не менять её ожидания.

## План
Для Haiku/Luna: не чинить предполагаемый дефект без падающего сценария. Характеризационные тесты существующего контракта могут проходить сразу — это результат, а не основание блокировать задачу.
1. Прочитать GenerationItemViewModel.ShowActionsAsync, GenerationItemViewModelTests (BaseGeneration/CardOf/CardWithActions), ShellDialogService.ChooseAsync и регистрацию IDialogService в MauiProgram.
2. Добавить ShowActions_FinishedStatus_CanExecute (Succeeded/Failed/Filtered/Cancelled), ShowActions_RunningThenSucceeded_RaisesCanExecuteChanged, ShowActions_AfterCancel_CanExecuteAgain, ShowActions_DialogThrows_CommandNotLeftRunning. Проверять состояние команды после await; в последнем тесте ожидать исходное исключение. Если тест выявил дефект — минимально исправить команду и проверить старые тесты.
3. ShellDialogService получает обязательный ILogger<ShellDialogService>. В ChooseAsync оставить ArgumentNullException и mapping cancel→null. До await — Debug с OptionCount, после — Debug с WasCancelled (bool), без текста выбранного пункта. Catch: Warning только «ChooseAsync failed», exception отдельным Debug, затем throw. Регистрация DI остаётся прежней.
4. Проверить Presentation.Tests, форматирование изменённых Presentation/Core-independent файлов и MAUI-проекта, сборку net10.0-android. В отчёте явно указать, что Windows воспроизведение остаётся088.
5. Заполнить «Отчёт» результатами проверок; снять занятость через `scripts/release-task.sh <номер>` и выполнить `bash scripts/check.sh`. Ветку задачи обновить от origin/main и открыть PR по docs/tasks.md. Ручные результаты не выдумывать.

Файлы: `ImageGen/ImageGen.Presentation.Tests/PageModels/GenerationItemViewModelTests.cs`, `ImageGen/ImageGen.Presentation/PageModels/GenerationItemViewModel.cs` (только при подтверждённом дефекте), `ImageGen/ImageGen.Maui/Services/ShellDialogService.cs`.

Проверка: `dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj`; `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android`; `dotnet format` каждого изменённого проекта с `--verify-no-changes`; `bash scripts/check.sh`.

## Отчёт

## Комментарии

### 2026-10-29, codex-review
Выделено при ревью PR #42 по поручению владельца. Это план работы, не отчёт об исполнении. Одобрение исходной задачи сохраняется для перенесённого объёма; код в этом PR не реализуется.
