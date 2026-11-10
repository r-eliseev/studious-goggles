---
id: 232
status: done
assignee:
claimed:
created: 2026-11-07
---

# Clear logs: культура сообщения и неудавшиеся удаления

## Описание
Мелочи ревью задачи 187 (2026-11-06): `SettingsPageModel.ClearLogsAsync` форматирует сообщение `string.Format(AppResources.SettingsLogsCleared, deleted)` без культуры (остальной код страницы передаёт `CultureInfo.CurrentCulture`); `LogFiles.DeleteAllExceptNewest` молча пропускает файлы, которые не удалось удалить, — ни пользователь, ни журнал об этом не узнают.

Зависит от: 229 (тот же `SettingsPageModel.cs`; делать после её слияния).
Среда: Linux или Windows.

## Критерии готовности
- `LogFiles.DeleteAllExceptNewest` возвращает `LogCleanupResult(int Deleted, int Failed)`; неудавшиеся удаления не прерывают обход (как сейчас).
- `ClearLogsAsync`: при `Failed > 0` — Warning в журнал (число и тип последнего исключения, без путей) и текст «Deleted N log files; M could not be deleted. The current file is kept.» (новый ресурс); при `Failed = 0` — прежний текст. Оба — `string.Format(CultureInfo.CurrentCulture, …)`.
- Тесты: `LogFilesTests` — файл, удаление которого бросает (подмена удаления делегатом или файл, открытый без `FileShare.Delete` на Windows — выбрать переносимый способ: необязательный параметр `Action<string>? deleteFile`), даёт `Failed = 1`, остальные удалены; `SettingsPageModelTests` — текст для `Failed > 0`.

## Анализ
`ImageGen/ImageGen.Presentation/Logging/LogFiles.cs` (`DeleteAllExceptNewest`), `ImageGen/ImageGen.Presentation/PageModels/SettingsPageModel.cs` (`ClearLogsAsync`, ≈строка 196), тесты — `ImageGen/ImageGen.Presentation.Tests/Logging/LogFilesTests.cs`, `PageModels/SettingsPageModelTests.cs`.

## План
1. Падающие тесты.
2. Результат очистки, журнал, тексты.

Файлы: `ImageGen/ImageGen.Presentation/Logging/LogFiles.cs`, `ImageGen/ImageGen.Presentation/Logging/LogCleanupResult.cs` (новый), `ImageGen/ImageGen.Presentation/PageModels/SettingsPageModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `ImageGen/ImageGen.Presentation.Tests/Logging/LogFilesTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/SettingsPageModelTests.cs`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
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

- `LogFiles.DeleteAllExceptNewest` now returns `LogCleanupResult` with deleted/failed counts and the last exception type; an injectable delete delegate makes failure handling portable to test.
- `SettingsPageModel.ClearLogsAsync` formats both messages with `CurrentCulture`, reports failed removals to the user, and writes a warning with only the count and exception type (no file paths).
- Added coverage for continued cleanup after an access-denied error and the failed-deletion alert/log message.
- Verification passed: Presentation tests (545), formatting for Presentation and Presentation.Tests, and `bash scripts/check.sh`.

## Комментарии

### 2026-11-07, claude-audit
Создано по мелочам ревью 187 (2026-11-06). План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
Поручение «на те проблемы, для которых нет задач, создай задачи»; план одобрен ранее данным поручением «заапрувь все таски которые будут».
