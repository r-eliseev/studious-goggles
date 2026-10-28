---
id: 071
status: done
assignee:
claimed:
created: 2026-10-28
---

# Широкое окно: модели и параметры панелью справа от чата

## Описание
На широком окне (≥ 840, макеты «Модели сессии» и «Параметры») кнопки Models и Parameters в шапке чата открывают не отдельный экран, а панель 360 px справа; чат остаётся виден и работает. Save сохраняет и закрывает панель, Cancel и крестик закрывают без сохранения. На узком — как сейчас (экран; в MAUI на телефонах параметры — нижняя панель, если `CommunityToolkit.Maui` `Popup` подходит, иначе экран).

Справочник: `docs/design/implementation.md` (ключи ресурсов, значения, шрифты, значки — дословно оттуда); токены: `docs/design/tokens.json`; макеты: `docs/design/prototype.html` (открыть: `python3 -m http.server` в `docs/design/`, затем http://localhost:8000/prototype.html; опубликованная версия — https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решение: ADR 0014.

Зависит от: 069, 070 (069 и 071 меняют `ChatView` и `ChatPage`, поэтому по очереди).
Среда: Linux или Windows (Avalonia — headless-тесты на Linux; MAUI — сборка Android на Linux с Android SDK, Windows-цель собирает CI; запуск под Windows — ручная проверка 016, Avalonia на рабочем столе — 046).

## Критерии готовности
- На ширине ≥ 840 панель открывается в `ChatView`/`ChatPage`, на < 840 — переход на экран, как сейчас.
- Одновременно открыта одна панель; повторное нажатие той же кнопки закрывает её.
- Сохранённые в панели модели и параметры действуют на следующий прогон (S5.2) — тест.
- Тесты из «Плана» проходят.

## Анализ
- `ChatPageModel.OpenModelsCommand` и `OpenParametersCommand` сейчас вызывают `INavigationService.GoToAsync(Routes.SessionModelsFor(id))`. Решение о панели зависит от ширины окна — это знание оболочки. План: `ChatPageModel` получает свойство `IsWide` (оболочка выставляет при изменении размера) и свойства `ModelsPane`/`ParametersPane` (модели страниц, создаются через фабрику `Func<SessionModelsPageModel>` из DI); команды при `IsWide` открывают панель, иначе — навигация.
- `SessionModelsPageModel.SaveCommand` сейчас после сохранения идёт назад (`GoBackAsync`); в панели нужно закрыть панель. Добавить событие `Closed` (или делегат `onClose`), которым пользуется чат; по умолчанию — назад, как сейчас.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, ключей и строк переноси дословно. Если шаг не получается так, как написано (компилятор не принимает разметку, API другое), не придумывай обход: опиши в «Комментариях», что пробовал и что получилось, поставь `blocked` и остановись.

1. Presentation: `ChatPageModel.IsWide`, `ModelsPane`, `ParametersPane`, `ClosePaneCommand`; у `SessionModelsPageModel` и `SessionParametersPageModel` — режим панели (`HostedInPane`, событие `CloseRequested`; Save и Cancel в режиме панели вызывают событие вместо `GoBackAsync`). Тесты: `OpenModels_Wide_OpensPane`, `OpenModels_Narrow_Navigates`, `OpenModelsTwice_ClosesPane`, `SaveInPane_ClosesPaneAndAppliesToNextRun`, `OpenParameters_WhenModelsOpen_ReplacesPane`.
2. Avalonia `ChatView`: третья колонка 360 с `ContentControl`, `Content` = `ModelsPane` или `ParametersPane` через шаблоны данных (`SessionModelsView`, `SessionParametersView`); заголовок панели и крестик; `IsWide` выставляется из обработчика ширины (067).
3. MAUI `ChatPage`: то же через `ContentView` и `AdaptiveTrigger`; `IsWide` — из `SizeChanged` страницы.
4. Тесты Avalonia `Views/ChatPaneTests.cs`: `Wide_ModelsButton_ShowsPane`, `Narrow_ModelsButton_Navigates`, `Pane_Save_Closes`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android -p:AndroidSdkDirectory=$ANDROID_HOME
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Presentation/PageModels/{ChatPageModel,SessionModelsPageModel,SessionParametersPageModel}.cs`, `ImageGen/ImageGen.Avalonia/Views/ChatView.*`, `Composition/AppServices.cs`, `ImageGen/ImageGen.Maui/Pages/ChatPage.*`, `MauiProgram.cs`, тесты

## Отчёт

Сделано по плану, TDD, все проверки зелёные.

Файлы:
- `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`: конструктор получил фабрики `Func<SessionModelsPageModel>`/`Func<SessionParametersPageModel>`; свойства `IsWide`, `ModelsPane`, `ParametersPane` плюс вычисляемые `ActivePane`, `IsPaneOpen`, `PaneTitle` (с `NotifyPropertyChangedFor`); команды `OpenModels`/`OpenParameters` при `IsWide` открывают/переключают панель (повторное нажатие той же кнопки закрывает, другая кнопка заменяет), иначе навигация как прежде; команда `ClosePane` и обработчик `CloseRequested` панели.
- `ImageGen/ImageGen.Presentation/PageModels/SessionModelsPageModel.cs` и `SessionParametersPageModel.cs`: флаг `HostedInPane`, событие `CloseRequested`; Save и Cancel в режиме панели вызывают событие вместо `GoBackAsync` (по умолчанию поведение прежнее).
- `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml(.cs)`: третья колонка 360 — `Border x:Name="SidePane"` с заголовком `PaneTitle`, крестиком (`ClosePaneCommand`, значок U+E1B2, имя `DescriptionClose`) и `ContentControl Content="{Binding ActivePane}"` с шаблонами данных на `SessionModelsView`/`SessionParametersView`; `IsWide` выставляется из обработчика ширины (067), при узком окне панель закрывается.
- `ImageGen/ImageGen.Avalonia/Composition/AppServices.cs` и `ImageGen/ImageGen.Maui/MauiProgram.cs`: регистрация фабрик панелей (`Func<...>` → `GetRequiredService`).
- `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml(.cs)`: третья колонка — `Border x:Name="SidePane"` (360, `IsVisible` по `IsPaneOpen`), заголовок, крестик, и два тела (модели/параметры), повторяющие `SessionModelsPage`/`SessionParametersPage`, с `BindingContext={Binding ModelsPane|ParametersPane}` и `IsVisible` через `toolkit:IsNotNullConverter` (источник — страница по `x:Reference`); `IsWide` из `SizeChanged`; обработчик `OnAssistantCheckedChanged` (у `RadioButton` в MAUI нет `Command`).
- Тесты: `ImageGen.Presentation.Tests/PageModels/ChatPageModelTests.cs` (`OpenModels_Wide_OpensPane`, `OpenModels_Narrow_Navigates`, `OpenParameters_Narrow_Navigates`, `OpenModelsTwice_ClosesPane`, `SaveInPane_ClosesPaneAndAppliesToNextRun`, `OpenParameters_WhenModelsOpen_ReplacesPane`); `ImageGen.Avalonia.Tests/Views/ChatPaneTests.cs` (`Wide_ModelsButton_ShowsPane`, `Narrow_ModelsButton_Navigates`, `Pane_Save_Closes`). Обновлён `ViewsAccessibilityTests` — теперь явно задаёт узкое окно (600), иначе кнопка Models по новому поведению открыла бы панель, а не экран.

Автономные решения:
- Для заголовка панели и крестика взял существующие строки `ModelsTitle`/`SessionParametersTitle` и `DescriptionClose` (отдельного ресурса `Close` в проекте нет, есть только значок).
- Добавил вычисляемые `ActivePane`/`IsPaneOpen`/`PaneTitle` в `ChatPageModel`, чтобы разметка обеих оболочек оставалась простой (в проекте нет null→bool конвертера для Avalonia; `ContentControl.Content` берёт `ActivePane`).
- MAUI: тело панели собрано инлайн по образцу страниц `SessionModelsPage`/`SessionParametersPage` (переиспользуемых ContentView для них в MAUI нет, только ContentPage; извлечение — вне объёма). Видимость тел — через `toolkit:IsNotNullConverter` из `CommunityToolkit.Maui`.
- `SaveInPane_*` проверяет через `_sessionRepository.Received().UpdateAsync(...)` с `RunsPerModel == 5` (мок `GetAsync` возвращает исходный объект, так что перечитать значение нельзя).

Проверки (локально, .NET 10 SDK 10.0.403, MAUI android workload + Android SDK, JDK 21):
- `dotnet build ImageGen.Maui -f net10.0-android` — успешно, 0 warnings / 0 errors.
- Presentation.Tests 215/215, Avalonia.Tests 102/102 (2 пропуска — DPAPI/Secret Service, как обычно).
- `dotnet format` (Presentation, Avalonia, Maui) — без изменений.
- `scripts/check.sh` — пройден.

Не проверено: запуск под Windows/рабочим столом вживую (ручная проверка 016/046) — панель смотрелась только через headless-тесты Avalonia и сборку MAUI; Windows-цель MAUI собирает CI.

## Комментарии
