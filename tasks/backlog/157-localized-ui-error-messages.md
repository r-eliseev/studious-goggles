---
id: 157
status: approved
assignee:
claimed:
created: 2026-10-31
---

# Не показывать текст исключений в диалогах ошибок

## Описание
Найдено при аудите 2026-10-31 (`docs/log/2026-10-31-completed-tasks-audit.md`), в том числе при проверке задачи 145.

- Документация: `docs/architecture.md`, «Правила слоёв» — «Ядро не формирует тексты интерфейса: ошибки и состояния отдаёт кодами (`ErrorKind`, `GenerationStatus`), тексты в `.resx` слоя представления». `docs/conventions.md`, «Секреты и данные» — не печатать ключи в сообщения об ошибках.
- Код: 24 обработчика в `ImageGen/ImageGen.Presentation/PageModels` показывают пользователю `ex.Message` как есть: `dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message)` — `ChatPageModel.cs` (8 мест), `SessionsPageModel.cs` (8), `SessionParametersPageModel.cs` (2), `ModelPickerPageModel.cs` (2), `SessionModelsPageModel.cs` (2), `ApiKeyPageModel.cs:140`, `ProviderKeyItemViewModel.cs:86`.
- Пример: при несовместимых референсах (задача 145) `GenerationService.RunOnModelAsync` бросает `ProviderException(ErrorKind.ModelWithoutReferences, "The selected model does not support reference images.")`, а `ModelPickerPageModel.PickAsync` показывает английский текст ядра вместо `ErrorKindText`. Для прочих исключений (SQLite, IO, HTTP) пользователь видит технический текст, который может содержать пути и ответ провайдера.

Зависит от: —
Среда: Linux или Windows (логика в `Presentation`, тесты на Linux).

## Критерии готовности
- Для `ProviderException` диалог показывает локализованный текст по `ErrorKind` (`ErrorKindText`), а не `Message`.
- Для прочих исключений — общий локализованный текст из `.resx`; детали исключения уходят в журнал на Debug (ADR 0022), в Warning/Error — только безопасные метаданные.
- Существующее поведение экранов не меняется: остаются на месте или возвращаются назад, как сейчас.
- Тесты `Presentation`: `ModelPickerPageModel` при `ModelWithoutReferences` показывает локализованный текст и остаётся на экране; хотя бы один обработчик с произвольным исключением не показывает `ex.Message`.

## Анализ
Что есть в коде (на `c6f8948`):
- 24 обработчика `catch (Exception ex)` в `ImageGen.Presentation/PageModels` вызывают `dialogs.AlertAsync(AppResources.ErrorTitle, ex.Message)`: `ChatPageModel` 255, 303, 361, 413, 451, 483, 535, 564; `SessionsPageModel` 165, 193, 302, 321, 338, 408, 434, 466; `SessionParametersPageModel` 108, 134; `ModelPickerPageModel` 115, 138; `SessionModelsPageModel` 159, 239; `ApiKeyPageModel` 140; `ProviderKeyItemViewModel` 86. В `ChatPageModel` 344–361 перед диалогом восстанавливаются ввод и вложения — эта логика остаётся.
- Готовое отображение кодов есть: `Presentation/Services/ErrorKindText.Of(ErrorKind)` с текстами в `AppResources.resx`, неизвестный код → `ErrorKindUnknown` («Something went wrong.»).
- Исключения ядра с кодом: `ProviderException.Kind` (`ErrorKind`), `ImageDescriptionException.Error` (`DescriptionError`: `ModelDoesNotAcceptImages`, `SourceMissing`, `EmptyResponse`) — для него текстов в `.resx` нет. Остальные (`StorageWriteException`, SQLite, IO, `InvalidOperationException` вроде «Session … not found», `ProviderConfigException`) — без кода, их `Message` технический и может содержать пути и ответ провайдера.
- Ни у одной PageModel нет `ILogger`: детали этих ошибок сейчас нигде не пишутся, кроме того, что уже залогировали сервисы. Единственный статический журнал — `TaskExtensions.Logger`.
- Конструкторы PageModels вызываются напрямую в тестах: `ChatPageModelTests`, `SessionsPageModelTests`, `SessionModelsPageModelTests`, `SessionParametersPageModelTests`, `ModelPickerPageModelTests`, `ApiKeyPageModelTests`; `ProviderKeyItemViewModel` создаёт `SettingsPageModel`.

Пересечения: 156 правит `SessionModelsPageModel` (загрузка моделей ассистента) — делать после 156, при rebase сохранить обе правки. Блокированная 131 упоминает `ChatPageModel.cs` и `.resx`, но её код уже в main и она ждёт только Windows-only 148: конфликта по файлам нет.

## План
1. **`Presentation/Services/ErrorAlert`** (sealed, singleton в DI обеих оболочек): зависит от `IDialogService` и `ILogger<ErrorAlert>`; метод `ShowAsync(Exception exception)`:
   - текст: `ProviderException` → `ErrorKindText.Of(Kind)`; `ImageDescriptionException` → текст по `DescriptionError` (новый статический `DescriptionErrorText.Of`, три строки в `.resx`); прочее → `ErrorKindUnknown`;
   - журнал: Warning только с именем типа исключения и, для `ProviderException`, кодом (без сообщения и объекта исключения); Debug — исключение целиком (ADR 0022);
   - заголовок — `AppResources.ErrorTitle`, как сейчас.
2. **PageModels.** Во всех 24 местах `dialogs.AlertAsync(ErrorTitle, ex.Message)` → `errorAlert.ShowAsync(ex)`. Остальная логика catch-блоков (возврат ввода, `IsBusy`, навигация) не меняется. `ErrorAlert` передаётся в конструкторы перечисленных PageModels; `SettingsPageModel` передаёт его в `ProviderKeyItemViewModel`.
3. **DI.** Регистрация `ErrorAlert` в `ImageGen.Avalonia/Composition/AppServices.cs` и `ImageGen.Maui/MauiProgram.cs`.
4. **Не входит:** `AppearanceSettingsFile` и `LogLevelSettings` — сообщения о неверном JSON уходят в журнал, не в UI; тексты в карточках генераций уже идут через `ErrorKindText`.
5. **Документы.** `docs/conventions.md`, «MAUI и MVVM» — правило: ошибки в диалогах только через `ErrorAlert`, без `ex.Message`. Архитектурный тест не добавлять (текстовая проверка исходников хрупкая); при повторе находки — проверка по `docs/audit.md`, «Что автоматизировать».

Тест-кейсы (сначала падающий тест):
- `ErrorAlert`: `ProviderException(ModelWithoutReferences, "english core text")` → диалог с `ErrorKindModelWithoutReferences`, текст ядра не показан; `ImageDescriptionException(SourceMissing, …)` → локализованный текст; `InvalidOperationException("C:\secret\path")` → `ErrorKindUnknown`, путь не в диалоге и не в Warning; Debug содержит исключение.
- `ModelPickerPageModel.PickAsync` при несовместимых референсах: локализованный текст и остаётся на экране (обновить `Pick_TargetDoesNotSupportSourceReferences_ShowsErrorAndStaysOnPage`).
- `ChatPageModel.SendAsync` при ошибке: ввод и вложения возвращаются, диалог без `ex.Message`.
- По одному тесту на `SessionsPageModel` и `ApiKeyPageModel`: произвольное исключение → общий текст.
- Существующие тесты, которые сравнивают текст диалога с `ex.Message`, переводятся на ожидаемый локализованный текст.

Проверка: тесты Presentation, Avalonia (композиция DI), Core не меняется; `dotnet format` для Presentation, Presentation.Tests, Avalonia, Maui; сборка MAUI `-f net10.0-android`; `scripts/check.sh`.

Файлы: `ImageGen/ImageGen.Presentation/Services/ErrorAlert.cs` (новый), `ImageGen/ImageGen.Presentation/Services/DescriptionErrorText.cs` (новый), `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/SessionsPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/SessionParametersPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/ModelPickerPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/SessionModelsPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/ApiKeyPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/SettingsPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/ProviderKeyItemViewModel.cs`, `ImageGen/ImageGen.Avalonia/Composition/AppServices.cs`, `ImageGen/ImageGen.Maui/MauiProgram.cs`, тесты в `ImageGen.Presentation.Tests` (Services, PageModels), `docs/conventions.md`.

## Отчёт

## Комментарии

### 2026-10-31, claude-audit
План готов, статус planned. Допущения: текст для исключений без кода — общий «Something went wrong.» (`ErrorKindUnknown`), без технических деталей в диалоге; детали только в журнале на Debug. Делать после 156 (общий `SessionModelsPageModel`).

### 2026-10-31, владелец (в чате, записал claude-audit)
План одобрен.
