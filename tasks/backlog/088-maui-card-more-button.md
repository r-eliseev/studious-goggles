---
id: 088
status: planned
assignee:
claimed:
created: 2026-10-29
---

# MAUI: кнопка «…» под картинкой не открывает действия

## Описание
Замечание владельца после ручной проверки MAUI под Windows (2026-10-29): на готовой карточке генерации (статус Done, картинка показана) кнопка «…» под метаданными не реагирует на клик. Ожидается то же меню действий, что по правому клику и долгому нажатию (задачи 069, 076, 080).

Что известно: в MAUI кнопка вызывает `GenerationItemViewModel.ShowActionsCommand` (`ImageGen.Maui/Pages/ChatPage.xaml`, блок «Card actions»), у команды `CanExecute = IsFinished`; в Avalonia «…» открывает `ContextMenu` через `OnMoreClick` и работает. Причина неизвестна. Возможные варианты: команда недоступна (`IsFinished` не выставлен или `CanExecuteChanged` не доходит до кнопки), не показывается лист действий `IDialogService` на Windows, жест карточки (`TapGestureRecognizer`, `TouchBehavior`) перехватывает нажатие. Выяснить в «Анализе».

Зависит от: —
Среда: только Windows (воспроизводится в MAUI под Windows; Android проверить, если причина общая).

## Критерии готовности
- Клик по «…» на готовой карточке открывает список действий; выбор пункта выполняет действие.
- На карточке в очереди или в работе кнопка недоступна или не показывается, как решено в 069.
- Причина записана в «Отчёте»; если это неочевидный факт о MAUI, то и в `docs/knowledge/maui.md`.
- Тест на уровне `Presentation` (доступность команды для готовой карточки, в том числе восстановленной из истории), если причина там.

## Анализ
Разбор по коду (2026-10-29). Воспроизвести можно только на Windows, поэтому причина по коду не установлена; ниже — что исключено и что осталось.

Цепочка вызова. Кнопка «…» (`ChatPage.xaml`, «Card actions») → `GenerationItemViewModel.ShowActionsCommand` (`AsyncRelayCommand`, `CanExecute = IsFinished`) → `IDialogService.ChooseAsync` → в MAUI `ShellDialogService.ChooseAsync` → `Shell.Current.DisplayActionSheetAsync`. `ChooseAsync` вызывается только отсюда, поэтому лист действий в MAUI под Windows раньше нигде не проверялся (в отчёте 069: «фактическое открытие DisplayActionSheet … не проверено»). Правый клик открывает не лист, а `MenuFlyout` карточки (`FlyoutBase.ContextFlyout`), это другой путь.

Что по коду в порядке:
- `IsFinished` выставляется в `Update` (из конструктора и из `GenerationUpdated`); у `Generation.IsFinished` готовы Succeeded, Failed, Filtered, Cancelled. Карточка из истории создаётся сразу готовой, поэтому `CanExecute` при привязке уже `true`.
- `[NotifyCanExecuteChangedFor(nameof(ShowActionsCommand))]` у `IsFinished` есть; `Update` при живой генерации идёт в UI-потоке (`ChatPageModel.OnGenerationUpdated` → `IMainThread.BeginInvokeOnMainThread`).
- Тест есть только на «в работе → недоступна» (`ShowActions_NotFinished_CannotExecute`); на «готовая из истории → доступна» и «в работе → готова → `CanExecuteChanged`» тестов нет. В этой среде нет .NET SDK, гипотезы тестами не проверялись.
- Стили `ghost` и `icon` у кнопки те же, что у работающей скрепки (`ghost,icon`).

Что остаётся (проверяется только на Windows):
1. **Лист действий не показывается или не возвращается на Windows.** В MAUI (исходник `AlertManager.Windows.cs`, ветка main на 2026-10-28; версия 10.0.110 не сверялась) лист — WinUI `Flyout` с `Placement = Full`, показанный у родителя платформенного вида страницы (`sender.ToPlatform().Parent`); если родителя нет, результат сразу `null` (то есть «Cancel» и ничего не происходит). Отправитель у нас — `Shell.Current`. Если задача листа не завершается, `AsyncRelayCommand` остаётся «выполняется», и кнопка становится недоступной до перезапуска.
2. **Нажатие перехватывает жест карточки.** `TouchBehavior` CommunityToolkit.Maui на Windows (исходник `TouchBehavior.windows.cs`, main на 2026-10-28; версия 15.0.1 не сверялась) подписывается на `PointerPressed`/`PointerReleased` платформенного вида карточки, захватывает указатель (`CapturePointer`) и ставит `e.Handled = true`. WinUI `Button` обычно сам помечает свои события указателя обработанными, так что родитель их не получает; поэтому вариант менее вероятен, но не исключён. `TapGestureRecognizer` карточки подписан на обычный `Tapped` и кнопке мешать не должен.
3. **Команда недоступна.** По коду маловероятно (см. выше); закрывается тестами шага 1.

Для различения нужна диагностика на Windows (шаг 2 плана): строка журнала в `ShellDialogService.ChooseAsync` при вызове и при ответе. Нет строки «вызов» → команда не выполнилась (варианты 2–3); есть «вызов», нет листа → вариант 1.

Решение по варианту 1 — сделать «…» на Windows так же, как в Avalonia: открыть тот же `MenuFlyout`, что по правому клику (в Avalonia `OnMoreClick` открывает `ContextMenu` карточки). Так меню одно на правый клик и «…», без системного листа. На Android и Mac Catalyst остаётся лист действий (`ChooseAsync`). Требует решения владельца — вопрос в «Комментариях», допущение: да.

Avalonia: «…» работает; если причина окажется в `Presentation`, правка общая, и Avalonia проверяется её тестами. Разметку Avalonia задача не меняет (решение владельца «обе оболочки» соблюдено: поведение уже совпадает).

Пересечения с незавершёнными задачами:
- `ImageGen.Maui/Pages/ChatPage.xaml`: 090, 091 (панель моделей), 095 (ввод), 097 (лента), 098 (шапка). Эта задача меняет только блок «Card actions» и, возможно, `TouchBehavior` карточки — другие места файла; порядок любой, при `rebase` сохранять обе стороны. Предлагаю 088 раньше 097 (маленькая и только Windows).
- `GenerationItemViewModel.cs` и `GenerationItemViewModelTests.cs`: 096 (сводка прогона читает статус и цену карточек; может добавить уведомления карточки). Здесь только тесты и, если причина в `Presentation`, правка `ShowActionsAsync`; порядок любой, лучше 088 первой: её тесты фиксируют доступность команды, на которую 096 не должна повлиять.
- `ChatPage.xaml.cs`: 097 (прокрутка ленты), 098 — добавляют свои обработчики; конфликты только текстовые.

## План
TDD: сначала падающий (или фиксирующий) тест, потом минимальная правка. Части 1 и 4 — Linux; 2, 3 и проверка — Windows.

1. **Тесты `Presentation` (Linux), `GenerationItemViewModelTests`:**
   - карточка, созданная из генерации Succeeded (как из истории) → `ShowActionsCommand.CanExecute(null)` = true; то же для Failed, Filtered, Cancelled;
   - карточка Running, затем `Update` той же генерации в Succeeded → событие `ShowActionsCommand.CanExecuteChanged` поднято хотя бы раз и `CanExecute` = true;
   - `ChooseAsync` возвращает `null` (отмена) → после завершения `ShowActionsCommand.IsRunning` = false и `CanExecute` = true (повторное открытие возможно);
   - `ChooseAsync` бросает исключение → исключение не оставляет команду в состоянии «выполняется» (ожидаемое поведение `AsyncRelayCommand` фиксируется тестом).
   Если какой-то тест падает — причина в `Presentation`: правка в `GenerationItemViewModel` минимальная, под этот тест.
2. **Диагностика (Windows).** `ShellDialogService` получает `ILogger<ShellDialogService>` (регистрация уже через DI в `MauiProgram`, конструктор). В `ChooseAsync`: Debug-строка «choose requested: N options» до вызова листа и «choose answered: chosen/cancel» после; исключение — Warning с текстом и повторный выброс. Запуск на Windows с `IMAGEGEN_LOG_LEVEL=Debug`, клик по «…» готовой карточки, затем журнал `<папка данных>/logs/imagegen-<дата>.log`. Одновременно руками: работает ли правый клик (меню), Tab до «…» и Enter/Space, «Copy» прогона, «Repeat» у карточки с ошибкой. Итог записать в «Отчёт».
3. **Правка по результату (Windows):**
   - вариант 1 (лист не показан или не вернулся): на Windows «…» открывает `MenuFlyout` карточки у кнопки. Кнопка получает обработчик `Clicked` в `ChatPage.xaml.cs` вместо `Command`, только под `#if WINDOWS`; на остальных платформах `Command="{Binding ShowActionsCommand}"` остаётся (через `OnPlatform` у свойства или два варианта кнопки с `IsVisible` по `OnPlatform` — выбрать то, что компилируется под Android и Windows). Обработчик: найти предка-`Border` карточки, взять у его платформенного вида `ContextFlyout` (MAUI переносит туда `FlyoutBase.ContextFlyout`) и показать у платформенного вида кнопки. Доступность кнопки: `IsEnabled` по `IsFinished` (правило 069 — у незавершённой карточки «…» недоступна). Если окажется, что платформенного `ContextFlyout` нет, запасной путь — свой список действий в `ShellDialogService.ChooseAsync` на Windows (модальная страница со списком кнопок, как `AvaloniaDialogService.ChooseAsync`); решение записать в «Отчёт».
   - вариант 2 (перехват жестом): у `TouchBehavior` карточки `IsEnabled` только на Android и iOS (`OnPlatform`); на десктопе меню даёт правый клик, долгое нажатие мышью не нужно. Проверить, что «…», выбор карточки тапом и правый клик работают.
   - вариант 3: правка из шага 1.
4. **Сборка и форматирование (Linux):** Android-цель MAUI собирается (`#if WINDOWS` не ломает Android), тесты и `dotnet format` по изменённым проектам.
5. **Документация:** причина — в «Отчёт»; если подтвердится вариант 1 или 2, факт о MAUI/CommunityToolkit на Windows — в `docs/knowledge/maui.md` (раздел «Меню и ускорители» или новый пункт «Лист действий на Windows»). В `tasks/v0.1/016-*.md` дописать пункт: «…» на готовой карточке открывает меню, выбор выполняет действие; на карточке в работе «…» недоступна; правый клик и долгое нажатие (Android) по-прежнему работают.

Крайние случаи: карточка, ставшая готовой во время показа ленты (живое обновление); карточка Failed (в меню добавляются «Other model…» и «Details»); повторное открытие меню после отмены.

Команды проверки:
- `dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj`
- `dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj` (если менялся `Presentation`)
- `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android`; на Windows `-f net10.0-windows10.0.19041.0` и запуск
- `dotnet format ImageGen/<Проект>/<Проект>.csproj --verify-no-changes` для Presentation (и Presentation.Tests), Maui
- `scripts/check.sh`

Файлы: `ImageGen/ImageGen.Presentation.Tests/PageModels/GenerationItemViewModelTests.cs`; `ImageGen/ImageGen.Presentation/PageModels/GenerationItemViewModel.cs` (только если падает тест шага 1); `ImageGen/ImageGen.Maui/Services/ShellDialogService.cs`; `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml.cs` (вариант 1 или 2); `docs/knowledge/maui.md`; `tasks/v0.1/016-skeleton-manual-check.md`.

## Отчёт

## Комментарии
### 2026-10-29, claude-plan
Вопрос владельцу: если причина в системном листе действий на Windows, можно ли, чтобы «…» на Windows открывала то же контекстное меню, что правый клик (как в Avalonia), а лист действий остался на телефонах? Допущение в плане: да.
