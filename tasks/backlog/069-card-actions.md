---
id: 069
status: planned
assignee:
claimed:
created: 2026-10-28
---

# Действия над генерацией: наведение, меню «…», нижнее меню на телефоне

## Описание
По макету «Действия над генерацией»: на десктопе у готовой карточки при наведении появляется строка кнопок-значков (Repeat, New seed, Edit, Describe, More); «More» и правый клик открывают меню со всеми действиями (Repeat, New seed, Edit prompt…, Describe image, Write a prompt from image). На телефонах — кнопка «…» на карточке и долгое нажатие открывают нижнее меню действий с заголовком (модель, сид, цена, время). У ошибки и отфильтрованной — кнопка Repeat всегда видна.

Справочник: `docs/design/implementation.md` (ключи ресурсов, значения, шрифты, значки — дословно оттуда); токены: `docs/design/tokens.json`; макеты: `docs/design/prototype.html` (открыть: `python3 -m http.server` в `docs/design/`, затем http://localhost:8000/prototype.html; опубликованная версия — https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решение: ADR 0014.

Зависит от: 068.
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

## Комментарии
