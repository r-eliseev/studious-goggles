---
id: 171
status: planned
assignee:
claimed:
created: 2026-10-31
---

# ErrorAlert — обязательная зависимость моделей страниц

## Описание
Замечание ревью 2026-10-31 к 157 (`docs/log/2026-10-31-task-review.md`): восемь моделей страниц и `ProviderKeyItemViewModel` принимают `ErrorAlert? errorAlert = null` и при отсутствии создают запасной `new ErrorAlert(dialogs, NullLogger<ErrorAlert>.Instance)`. Если в новой оболочке (например, Web 031) регистрацию забыть, ошибка не проявится, а журнал ошибок интерфейса молча пропадёт. План 157 предполагал обязательный параметр.

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- Во всех моделях страниц и `ProviderKeyItemViewModel` параметр `ErrorAlert` обязательный, запасного экземпляра нет.
- DI обеих оболочек разрешает все модели страниц (существующие тесты композиции Avalonia; для MAUI — сборка и тест разметки регистраций, если есть).
- Тесты Presentation создают `ErrorAlert` явно (общий помощник в `TestDoubles`); поведение диалогов не меняется.

## Анализ
Файлы с запасным экземпляром (на `040c198`): `ApiKeyPageModel`, `ChatPageModel`, `ModelPickerPageModel`, `SessionModelsPageModel`, `SessionParametersPageModel`, `SessionsPageModel`, `SettingsPageModel`, `ProviderKeyItemViewModel`. `ErrorAlert` зарегистрирован в `AppServices.cs` и `MauiProgram.cs`. Тесты Presentation вызывают конструкторы напрямую (`*PageModelTests`).

## План
1. Тест (сначала красный): в `Presentation.Tests` проверка, что у конструкторов перечисленных типов параметр `ErrorAlert` не имеет значения по умолчанию (рефлексия по `ParameterInfo.HasDefaultValue`).
2. Убрать `= null` и `??`-запасной экземпляр; поле присваивается из параметра (`ArgumentNullException.ThrowIfNull`).
3. `TestDoubles/ErrorAlerts.Create(IDialogService)` с `NullLogger`; обновить вызовы конструкторов в тестах.
4. Проверки: Presentation.Tests, Avalonia.Tests (композиция DI), сборка MAUI Android, `dotnet format` Presentation/Presentation.Tests, `scripts/check.sh`.

Файлы: восемь файлов из «Анализа» в `ImageGen/ImageGen.Presentation/PageModels/`, `ImageGen/ImageGen.Presentation.Tests/TestDoubles/ErrorAlerts.cs` (новый), `ImageGen/ImageGen.Presentation.Tests/PageModels/*Tests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ErrorAlertDependencyTests.cs` (новый).

## Отчёт

## Комментарии

### 2026-10-31, владелец (в чате, записал claude-audit)
Завести задачу: ErrorAlert обязательный (выбрано в вопросе).
