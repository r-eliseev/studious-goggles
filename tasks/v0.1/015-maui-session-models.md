---
id: 015
status: planned
assignee:
claimed:
created: 2026-10-27
---

# MAUI: модели сессии

## Описание
Экран настроек сессии: список моделей картинок OpenRouter из каталога (имя, id, «seed» или «no seed»), поиск по имени и id, галочки выбора, число запусков на модель от 1 до 10 (Stepper), кнопка Refresh (перечитать каталог из API) и Save (сохранить и вернуться в чат).

Зависит от: 014.
Среда: только Windows с установленными нагрузками MAUI (сборка под Windows и Android).

## Критерии готовности
- Страница и модели из плана есть и работают как описано.
- Сборка под Windows и Android без ошибок и предупреждений; `dotnet format` для MAUI-проекта проходит.
- Ручная проверка (задача 016): выбранные модели и число запусков сохраняются, подпись кнопки в чате меняется на «Models: N × M», следующий прогон создаёт N × M карточек.

## Анализ
- Спецификация: S5.1–S5.2 (`docs/specs/chat-sessions.md`).
- Выбор сохраняется при поиске и при Refresh (берётся из текущих галочек). Выбранные модели, которых нет в каталоге, при сохранении не теряются.
- Порядок моделей в сессии — порядок каталога (по имени); выбор порядка позже.
- Пробная реализация: `poc/v0.1/015-maui-session-models/` (подсказка, не копировать дословно, см. `poc/README.md`); там же итоговый `MauiProgram.cs` скелета.

## План
1. `PageModels/ModelOptionViewModel(ImageModel, isSelected)`: `Id`, `Name`, `DetailsText` = `<id> · <seed | no seed>`, наблюдаемое `IsSelected`.
2. `PageModels/SessionModelsPageModel` (`IQueryAttributable`, параметр `sessionId`):
   - Зависимости: `SessionService`, `ModelCatalogService`, `INavigationService`, `IDialogService`.
   - Состояние: полный список вариантов, отфильтрованная коллекция `Models`, `SearchText`, `RunsPerModel` (с текстом `RunsPerModelFormat`), `MinRunsPerModel`/`MaxRunsPerModel` из `SessionSettings`, `IsBusy`.
   - Поток:
     ```
     OnAppearingAsync → Load(forceRefresh: false); Refresh → Load(forceRefresh: true)
     Load: session = GetAsync(sessionId) (нет → ошибка)
           выбранные = текущие галочки, если список уже загружен, иначе Settings.ImageModelIds
           при первой загрузке RunsPerModel = Settings.RunsPerModel
           models = catalog.GetImageModelsAsync(forceRefresh)
           пересобрать варианты с галочками по «выбранным», применить фильтр
           ошибка → AlertAsync
     Фильтр (на каждое изменение SearchText): пустой поиск → все;
           иначе имя или id содержит строку без учёта регистра
     Save: ImageModelIds = отмеченные в порядке каталога + ранее выбранные id, которых нет в каталоге
           RunsPerModel из степпера, AssistantModelId без изменений
           SessionService.UpdateSettingsAsync → GoBackAsync; ошибка → AlertAsync
     ```
3. `Pages/SessionModelsPage` (заголовок `ModelsTitle`; первый показ → `OnAppearingAsync` через `FireAndForgetSafeAsync`):
   - `SearchBar` (placeholder `ModelsSearchPlaceholder`);
   - строка: текст числа запусков, `Stepper` от `MinRunsPerModel` до `MaxRunsPerModel` с шагом 1, кнопка Refresh;
   - индикатор и `CollectionView` без выбора: `CheckBox` и два текста (имя, детали);
   - кнопка Save внизу.
   - Компилируемые привязки, `SemanticProperties` у галочек, степпера и кнопок.
4. `MauiProgram.cs`: после регистрации `ChatPage` — `AddTransientWithShellRoute<SessionModelsPage, SessionModelsPageModel>(Routes.SessionModels)`.

Файлы: `ImageGen/ImageGen.Maui/MauiProgram.cs`, `ImageGen/ImageGen.Maui/PageModels/ModelOptionViewModel.cs`, `ImageGen/ImageGen.Maui/PageModels/SessionModelsPageModel.cs`, `ImageGen/ImageGen.Maui/Pages/SessionModelsPage.*`

### Проверка
```
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
scripts/check.sh
```

## Отчёт

## Комментарии
