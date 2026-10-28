---
id: 069
status: done
assignee:
claimed: 2026-10-28T00:00:00Z
created: 2026-10-28
---

# Действия над генерацией: наведение, меню «…», нижнее меню на телефоне

## Описание
По макету «Действия над генерацией»: на десктопе у готовой карточки при наведении появляется строка кнопок-значков (Repeat, New seed, Edit, Describe, More); «More» и правый клик открывают меню со всеми действиями (Repeat, New seed, Edit prompt…, Describe image, Write a prompt from image). На телефонах — кнопка «…» на карточке и долгое нажатие открывают нижнее меню действий с заголовком (модель, сид, цена, время). У ошибки и отфильтрованной — кнопка Repeat всегда видна.

Справочник: `docs/design/implementation.md` (ключи ресурсов, значения, шрифты, значки — дословно оттуда); токены: `docs/design/tokens.json`; макеты: `docs/design/prototype.html` (открыть: `python3 -m http.server` в `docs/design/`, затем http://localhost:8000/prototype.html; опубликованная версия — https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решение: ADR 0014.

Зависит от: 068, 072 (оба меняют `AvaloniaDialogService`).
Среда: Linux или Windows (Avalonia — headless-тесты на Linux; MAUI — сборка Android на Linux с Android SDK, Windows-цель собирает CI; запуск под Windows — ручная проверка 016, Avalonia на рабочем столе — 046).

## Критерии готовности
- Действия те же, что сейчас (команды `GenerationItemViewModel`); новых действий нет (Copy prompt, Save image — задача 074).
- Десктоп Avalonia: строка значков видна только при наведении и при фокусе клавиатуры внутри карточки; контекстное меню по правому клику и по «More»; у каждой кнопки-значка доступное имя и подсказка (`ToolTip.Tip`).
- MAUI: на телефонах «…» и долгое нажатие → `DisplayActionSheet` (через `IDialogService`, новый метод); на Windows и Mac — `MenuFlyout` (`FlyoutBase.ContextFlyout`) и видимая кнопка «…».
- Тесты из «Плана» проходят.

## Анализ
- Сейчас в `ChatView.axaml` пять кнопок с текстом всегда видны.
- Доступность: показ по наведению недоступен с клавиатуры, поэтому добавить показ по `:focus-within` (Avalonia).
- Нижнее меню: в `IDialogService` нет выбора из списка — добавить `Task<string?> ChooseAsync(string title, string cancel, IReadOnlyList<string> options)`; реализации: MAUI — `Shell.Current.DisplayActionSheet`; Avalonia — окно со списком кнопок (как остальные диалоги в `AvaloniaDialogService`).
- `GenerationItemViewModel.ShowActionsCommand` вызывает `ChooseAsync` с подписями действий и выполняет выбранное — логика в Presentation, тестируется подменой `IDialogService`.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, ключей и строк переноси дословно. Если шаг не получается так, как написано (компилятор не принимает разметку, API другое), не придумывай обход: опиши в «Комментариях», что пробовал и что получилось, поставь `blocked` и остановись.

1. Presentation: `IDialogService.ChooseAsync`; `GenerationItemViewModel.ShowActionsCommand` (доступна, когда `IsFinished`); заголовок меню — `ModelText` + `SeedText` + `CostText` + `TimeText` через « · ». Тесты `ShowActions_ChoosesRepeat_RunsRepeat`, `ShowActions_Cancel_DoesNothing`, `ShowActions_NotFinished_CannotExecute`. Обновить подмены `IDialogService` в тестах обеих оболочек.
2. Avalonia: реализация `ChooseAsync` в `AvaloniaDialogService`; в шаблоне карточки — `StackPanel.card-actions` с пятью `Button.ghost.icon.small` (значки `Repeat`, `NewSeed`, `Edit`, `Describe`, `More`), стиль `Border.card:pointerover StackPanel.card-actions, Border.card:focus-within StackPanel.card-actions { IsVisible: True }`; `ContextMenu` на карточке с пятью пунктами; «More» открывает тот же `ContextMenu`.
3. MAUI: на карточке кнопка «…» (`Button.ghost.icon`, значок `More`), `TapGestureRecognizer`… для долгого нажатия в MAUI нет готового жеста — использовать `CommunityToolkit.Maui` `TouchBehavior` `LongPressCommand` (пакет уже подключён; проверить версию), команда `ShowActionsCommand`; на Windows и Mac — `FlyoutBase.ContextFlyout` с `MenuFlyoutItem`.
4. Тесты Avalonia `Views/CardActionsTests.cs`: `Actions_HiddenWithoutPointer`, `Actions_VisibleOnFocusWithin`, `ContextMenu_HasAllActions`, `IconButtons_HaveAutomationNames`.

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

Файлы: `ImageGen/ImageGen.Presentation/{Services/IDialogService.cs,PageModels/GenerationItemViewModel.cs}`, строки, `ImageGen/ImageGen.Avalonia/{Services/AvaloniaDialogService.cs,Views/ChatView.axaml}`, `ImageGen/ImageGen.Maui/{Services/ShellDialogService.cs,Pages/ChatPage.xaml}`, тестовые подмены `IDialogService`, тесты

## Отчёт

Сделано по шагам плана (TDD).

Изменённые файлы:
- `ImageGen/ImageGen.Presentation/Services/IDialogService.cs` — добавлен `Task<string?> ChooseAsync(string title, string cancel, IReadOnlyList<string> options)`.
- `ImageGen/ImageGen.Presentation/PageModels/GenerationItemViewModel.cs` — новый конструкторный параметр `IDialogService`; команда `ShowActionsCommand` (доступна при `IsFinished`): заголовок меню — `ModelText · SeedText · CostText · TimeText` (пустые пропускаются), опции — пять действий, выбранное выполняется. Добавлено свойство `IsFailedOrFiltered` (для всегда видимого Repeat у ошибки/фильтра).
- `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs` — `CreateCard` передаёт `dialogs` в карточку.
- `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx` и `.cs` — новые строки `More` = «More», `ActionEditPrompt` = «Edit prompt…», `ActionDescribeImage` = «Describe image», `ActionPromptFromImage` = «Write a prompt from image».
- `ImageGen/ImageGen.Avalonia/Services/AvaloniaDialogService.cs` — реализация `ChooseAsync` окном со списком кнопок (как остальные диалоги), Escape = отмена.
- `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml` — карточка получила `Classes="card"`; строка `StackPanel.card-actions` из пяти `Button.ghost.icon.small` (Repeat, New seed, Edit, Describe, More) с `ToolTip.Tip` и доступными именами; стили `Border.card:pointerover`/`Border.card:focus-within` → `IsVisible=True`; `ContextMenu` на карточке с пятью пунктами; кнопка «More» открывает тот же `ContextMenu` (код-behind `OnMoreClick`). Добавлена всегда видимая кнопка Repeat при `IsFailedOrFiltered`.
- `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml.cs` — обработчик `OnMoreClick` (ищет Border.card среди предков и открывает `ContextMenu.Open`).
- `ImageGen/ImageGen.Maui/Services/ShellDialogService.cs` — `ChooseAsync` через `Shell.Current.DisplayActionSheetAsync` (возврат `null` при отмене).
- `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml` — кнопка «…» (`ghost,icon`, значок `More`) с `ShowActionsCommand`; `CommunityToolkit.Maui` `TouchBehavior` `LongPressCommand="{Binding ShowActionsCommand}"` на карточке (долгое нажатие); `FlyoutBase.ContextFlyout` → `MenuFlyout` с пятью `MenuFlyoutItem` (Windows/Mac, правый клик); всегда видимая кнопка Repeat при `IsFailedOrFiltered`.
- Тесты: `GenerationItemViewModelTests` (`ShowActions_ChoosesRepeat_RunsRepeat`, `ShowActions_Cancel_DoesNothing`, `ShowActions_NotFinished_CannotExecute`), обновлены подмены `IDialogService` в `GenerationItemViewModelTests`, `RunItemViewModelTests`, `ChatViewStyleTests`; новый `ImageGen.Avalonia.Tests/Views/CardActionsTests.cs` (`Actions_HiddenWithoutPointer`, `Actions_VisibleOnFocusWithin`, `ContextMenu_HasAllActions`, `IconButtons_HaveAutomationNames`).

Сборка и проверки (локально, .NET 10 SDK 10.0.403, MAUI android workload + Android SDK в `.tools`, JDK 21 из mise):
- `dotnet build ImageGen.Maui -f net10.0-android` — успешно, 0 warnings / 0 errors (SourceGen XAML — тот же путь компиляции, что и у Windows-цели CI).
- `ImageGen.Presentation.Tests` 211/211; `ImageGen.Avalonia.Tests` 99/99 (2 пропуска — DPAPI/Secret Service по платформе); Core 307/307; Data 63/63.
- `dotnet format --verify-no-changes` для Presentation, Avalonia, Maui — без изменений.
- `scripts/check.sh` — пройден.

Автономные решения:
- Подписи пунктов меню действий в `docs/design/implementation.md` не заданы дословно; взяты из макета `prototype.html` (строки 574/580): «Edit prompt…», «Describe image», «Write a prompt from image». Repeat и New seed переиспользуют существующие ресурсы `Repeat`/`NewSeed`. Это действия задачи (Copy prompt / Save image — задача 074 — не добавлены).
- Иконка «More» = Lucide ellipsis (`IconGlyphs.More`, U+E0B6) по разделу 5 справочника.
- «Repeat всегда виден у ошибки/отфильтрованной» (из «Описания» и «Критериев», вне нумерованных шагов плана) реализован отдельной кнопкой `Repeat` с видимостью по новому свойству `IsFailedOrFiltered` в обеих оболочках.
- `CommunityToolkit.Maui` уже подключён (версия 15.0.1, инициализация `UseMauiCommunityToolkit` в `MauiProgram`); `TouchBehavior.LongPressCommand` присутствует в этой версии — новый пакет не добавлялся.
- `DisplayActionSheet` помечен устаревшим в MAUI 10 — использован `DisplayActionSheetAsync` (как соседние `DisplayAlertAsync`/`DisplayPromptAsync`).

Не проверено автоматически: фактическое открытие `DisplayActionSheet`/`MenuFlyout`/долгого нажатия на устройстве (ручная проверка 016/046); Windows-цель MAUI собирает CI.

## Комментарии
