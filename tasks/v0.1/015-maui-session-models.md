---
id: 015
status: done
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

Статус `in-progress`: критерий «сборка под Windows» и ручная проверка (задача 016) не выполнены (облачная сессия, XAML-компилятор только под Windows). Статусы 012, 013 и 014 не менялись.

- Сделано (план пп. 1–4):
  - `PageModels/ModelOptionViewModel.cs`: `Id`, `Name`, `DetailsText` (`<id> · seed` / `<id> · no seed`), наблюдаемый `IsSelected`;
  - `PageModels/SessionModelsPageModel.cs`: загрузка (`OnAppearingAsync`, `LoadAsync`), `RefreshCommand` (`forceRefresh: true`), `SaveCommand`, фильтр по `SearchText` (без учёта регистра, по имени и id, после `Trim`), `RunsPerModel` и `RunsPerModelText`, `IsBusy`; поток и правила Save по плану: отмеченные в порядке каталога и ранее сохранённые id, которых нет в каталоге, сохраняются;
  - `Pages/SessionModelsPage.xaml(.cs)`: `SearchBar`, `Stepper` с `Minimum`/`Maximum`, Refresh, `ActivityIndicator`, `CollectionView` (`SelectionMode="None"`) с `CheckBox`, Save; `x:DataType` у страницы и шаблона, `SemanticProperties.Description` у поиска, степпера, галочек и кнопок; первый показ через `FireAndForgetSafeAsync`;
  - `MauiProgram.cs`: `AddTransientWithShellRoute<SessionModelsPage, SessionModelsPageModel>(Routes.SessionModels)` после `ChatPage`;
  - строки: новых нет, все ключи уже были в `AppResources.resx` и `AppResources.cs`.
- Тесты (TDD: сначала файлы тестов с ссылками на ещё не существующие классы, падение сборки; затем минимальная реализация; все тесты в `ImageGen.Core.Tests/MauiLinked/`, рантайм MAUI не нужен):
  - `ModelOptionViewModelTests` (5): id и имя, детали с seed и без, стартовое значение и уведомление `IsSelected`;
  - `SessionModelsPageModelTests` (18): загрузка с галочками и числом запусков из настроек, без принудительного обновления; поиск по имени, по id, пустой после `Trim`, без совпадений; выбор, сделанный при фильтре, сохраняется; Save в порядке каталога с сохранением неизвестных id, новое число запусков, настройки ассистента не меняются, возврат назад; Refresh с `forceRefresh` и текущими галочками; неизвестная сессия и ошибка каталога дают alert; ошибка сохранения даёт alert и не уходит со страницы; Save и Refresh недоступны во время загрузки; неверный id в query даёт `ArgumentException`. Сервисы реальные (`SessionService`, `ModelCatalogService`), подменены репозитории и `IModelProvider`.
- Результаты:
  - `dotnet test` ImageGen.Core.Tests: 193 passed, 0 failed (было 170; +23). Прогон с ошибкой одного теста: см. «Замечание» ниже;
  - `dotnet test` ImageGen.Data.Tests: 38 passed, 0 failed;
  - `dotnet format --verify-no-changes` (код выхода 0): ImageGen.Maui, ImageGen.Core, ImageGen.Core.Tests, ImageGen.Data, ImageGen.Data.Tests;
  - `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android`: 0 Error(s), 0 Warning(s);
  - контрольная проверка XAML: привязка к несуществующему свойству даёт ошибку MAUIG2045 в Android-сборке (значит, привязки этой страницы проверяются генератором); правка откачена, сборка повторена, 0 ошибок и 0 предупреждений;
  - `scripts/check.sh`: все проверки пройдены.
- Отклонения от плана:
  - `SessionModelsPageModel` не реализует `IQueryAttributable` (тип MAUI не попадает в тесты на Linux). Как в 014, `SessionModelsPage` реализует его и передаёт query в `ApplyQueryAttributes(IDictionary<string, object>)`;
  - `RunsPerModel` имеет тип `double`, потому что `Stepper.Value`, `Minimum` и `Maximum` в MAUI типа `double`; значение всегда целое от 1 до 10, при сохранении округляется до `int`. Минимум и максимум выведены как `double`-свойства `MinRunsPerModel`/`MaxRunsPerModel`;
  - добавлено: `SaveCommand` и `RefreshCommand` недоступны при `IsBusy` (во время загрузки); Save без загруженной сессии даёт alert, а не молчит;
  - `docs/` не менялись: расхождений с реализацией не найдено (`conventions.md` и `knowledge/maui.md` говорят об `IQueryAttributable` без указания класса).
- Замечание (существующий тест из 014, не менялся): `ChatPageModelTests.GeneratingCard_UpdatesInPlaceWithoutReload` падает примерно в 2 прогонах из 9 (`Expected: "Generating..." Actual: "Queued"`). Причина: тест ждёт, пока первая генерация дойдёт до провайдера (`entered`), и сразу проверяет, что обе карточки в статусе «идёт», а вторая может быть ещё в «в очереди». Задача 015 не трогает `ChatPageModel` и `GenerationService`. Исправление в тесте 014: дождаться (с таймаутом) статуса «идёт» у всех карточек перед проверкой. Критерий «все тесты проходят» выполняется не во всех прогонах; решение за владельцем или следующей правкой 014.
- Не проверено (нужен Windows или рантайм MAUI, задача 016):
  - сборка `-f net10.0-windows10.0.19041.0` (XAML-компилятор только под Windows);
  - запуск на устройстве: `CheckBox` и `Stepper` в `CollectionView`, двусторонние привязки `SearchBar.Text`, `Stepper.Value`, `CheckBox.IsChecked` (явно `Mode=TwoWay`);
  - переход с кнопки «Models» в чате и возврат через `GoBackAsync`, перечитывание чата и подпись «Models: N × M»;
  - расположение `ActivityIndicator` поверх `CollectionView` в одной строке сетки (верстка не проверена);
  - `SemanticProperties` на рантайме (голосовой вывод);
  - Android: только сборка, без запуска (эмулятор в облачной сессии недоступен, см. `docs/knowledge/claude-cloud-session.md`).

**Проверено на Windows (2026-10-28, ревью после мерджа PR #14; Windows 11, SDK 10.0.402):**
- `dotnet format --verify-no-changes` для ImageGen.Maui, Core, Core.Tests, Data, Data.Tests: без изменений.
- `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0` и `-f net10.0-android`: 0 ошибок, 0 предупреждений.
- `dotnet test`: ImageGen.Core.Tests 193 из 193 пять прогонов подряд (после c9d5ea9 гонки нет), ImageGen.Data.Tests 38 из 38.
- Запуск `ImageGen.Maui.exe` (Debug): приложение стартует и работает без падения (контейнер DI и Shell собираются). Интерфейс и сценарий с ключом — задача 016.
- Закрыто: строка «сборка под Windows» из критериев выполнена; ручные пункты критериев отнесены к 016.

## Комментарии
