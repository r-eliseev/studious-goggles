---
id: 157
status: todo
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

## План

## Отчёт

## Комментарии
