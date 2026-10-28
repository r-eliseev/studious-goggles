---
id: 071
status: approved
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

## Комментарии
