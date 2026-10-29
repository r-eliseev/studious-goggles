---
id: 088
status: approved
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

Решение по варианту 1 — сделать «…» на Windows так же, как в Avalonia: открыть тот же `MenuFlyout`, что по правому клику (в Avalonia `OnMoreClick` открывает `ContextMenu` карточки). Так меню одно на правый клик и «…», без системного листа. На Android и Mac Catalyst остаётся лист действий (`ChooseAsync`). Решено владельцем (2026-10-29).

Avalonia: «…» работает; если причина окажется в `Presentation`, правка общая, и Avalonia проверяется её тестами. Разметку Avalonia задача не меняет (решение владельца «обе оболочки» соблюдено: поведение уже совпадает).

Пересечения с незавершёнными задачами:
- `ImageGen.Maui/Pages/ChatPage.xaml`: 090, 091 (панель моделей), 095 (ввод), 097 (лента), 098 (шапка). Эта задача меняет только блок «Card actions» и, возможно, `TouchBehavior` карточки — другие места файла; порядок любой, при `rebase` сохранять обе стороны. Предлагаю 088 раньше 097 (маленькая и только Windows).
- `GenerationItemViewModel.cs` и `GenerationItemViewModelTests.cs`: 096 (сводка прогона читает статус и цену карточек; может добавить уведомления карточки). Здесь только тесты и, если причина в `Presentation`, правка `ShowActionsAsync`; порядок любой, лучше 088 первой: её тесты фиксируют доступность команды, на которую 096 не должна повлиять.
- `ChatPage.xaml.cs`: 097 (прокрутка ленты), 098 — добавляют свои обработчики; конфликты только текстовые.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, членов, ключей строк и тестов переноси дословно. Если шаг не получается так, как написано (API другое, компилятор не принимает разметку), не придумывай обход: опиши в «Комментариях», что пробовал, поставь `blocked` и остановись.

Выполнять после: — (с 090, 091, 095, 097, 098 общий `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml` и `ChatPage.xaml.cs`, но разные блоки; с 096 общий `GenerationItemViewModelTests.cs`, только новые тесты. Порядок любой, при `rebase` сохранять обе стороны.)

Решения (владелец, 2026-10-29): если причина в системном листе действий на Windows, «…» на Windows открывает тот же `MenuFlyout`, что правый клик по карточке (как «More» в Avalonia); на Android, iOS и Mac Catalyst «…» по-прежнему вызывает `ShowActionsCommand` (лист действий). Строки журнала диагностики остаются в коде (уровень Debug).

Шаги 1, 2 и 6 делаются на Linux или Windows; шаги 3–5 — только на Windows.

Прочитай перед началом:
- этот «Анализ» (цепочка вызова и варианты причины);
- `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`: шаблон карточки `DataTemplate x:DataType="pm:GenerationItemViewModel"` (`Border.Behaviors` с `toolkit:TouchBehavior`, `FlyoutBase.ContextFlyout` с `MenuFlyout`, блок по комментарию `<!-- Card actions: ... -->`);
- `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml.cs`, `ImageGen/ImageGen.Maui/Services/ShellDialogService.cs`;
- `ImageGen/ImageGen.Presentation/PageModels/GenerationItemViewModel.cs` (`IsFinished`, `ShowActionsAsync`), `ImageGen/ImageGen.Presentation.Tests/PageModels/GenerationItemViewModelTests.cs` (помощники `BaseGeneration`, `CardOf`, `CardWithActions`);
- `docs/knowledge/maui.md`, раздел «Меню и ускорители (задача 073)»; `docs/knowledge/dev-environment.md`, раздел «Логи».

1. **Тесты доступности команды** (`ImageGen/ImageGen.Presentation.Tests/PageModels/GenerationItemViewModelTests.cs`). Ожидается, что тесты проходят сразу (они фиксируют поведение). Если какой-то тест падает — это неожиданность: опиши в «Комментариях», какой и с каким сообщением, поставь `blocked` и остановись.
   - `ShowActions_FinishedStatus_CanExecute` — `[Theory]` по `GenerationStatus.Succeeded`, `Failed`, `Filtered`, `Cancelled`: карточка через `CardOf(generation)` из генерации с этим статусом (как из истории) → `ShowActionsCommand.CanExecute(null)` равно `true`.
   - `ShowActions_RunningThenSucceeded_RaisesCanExecuteChanged`: карточка из генерации `Running`; подписка на `ShowActionsCommand.CanExecuteChanged` со счётчиком; та же генерация со статусом `Succeeded` передаётся в `card.Update(generation)` → счётчик ≥ 1 и `CanExecute(null)` равно `true`.
   - `ShowActions_AfterCancel_CanExecuteAgain`: `IDialogService.ChooseAsync` (подмена) возвращает `null`; `await card.ShowActionsCommand.ExecuteAsync(null)` → `ShowActionsCommand.IsRunning` равно `false`, `CanExecute(null)` равно `true`.
   - `ShowActions_DialogThrows_CommandNotLeftRunning`: `ChooseAsync` бросает `InvalidOperationException`; `Assert.ThrowsAsync<InvalidOperationException>` вокруг `ExecuteAsync(null)` → после этого `IsRunning` равно `false`, `CanExecute(null)` равно `true`.
2. **Журнал в `ShellDialogService`** (`ImageGen/ImageGen.Maui/Services/ShellDialogService.cs`). Класс получает первичный конструктор `ShellDialogService(ILogger<ShellDialogService> logger)` (регистрация `AddSingleton<IDialogService, ShellDialogService>()` в `MauiProgram.cs` не меняется: DI подставит логгер). В `ChooseAsync(string title, string cancel, IReadOnlyList<string> options)`:
   - до вызова `DisplayActionSheetAsync` — `logger.LogDebug("ChooseAsync requested: {OptionCount} options", options.Count)`;
   - после ответа — `logger.LogDebug("ChooseAsync answered: {Answer}", ...)`, где `Answer` — выбранный текст или `"cancel"`, если вернулся `cancel` или `null`;
   - исключение из `DisplayActionSheetAsync` — `logger.LogWarning(exception, "ChooseAsync failed")` и повторный выброс (`throw;`).
   Тестов нет (MAUI-проект без тестов); проверка — сборка Android из «Проверки».
3. **Диагностика на Windows** (точная процедура; результат — в «Отчёт» дословно: какой исход и строки журнала).
   1. Собрать Windows-цель (команда из «Проверки»). В PowerShell: `$env:IMAGEGEN_LOG_LEVEL = "Debug"`, затем запустить `ImageGen/ImageGen.Maui/bin/Debug/net10.0-windows10.0.19041.0/win-x64/ImageGen.Maui.exe` из того же окна.
   2. Открыть сессию с хотя бы одной карточкой Done (или отправить сообщение и дождаться Done).
   3. Один раз щёлкнуть мышью «…» на карточке Done. Подождать 3 секунды. Записать: появилось ли что-нибудь на экране; выглядит ли «…» серой (недоступной).
   4. Нажать Escape. Нажимать Tab, пока фокус не окажется на «…» этой карточки, нажать Enter. Подождать 3 секунды, записать, что появилось.
   5. Закрыть приложение. Открыть самый свежий файл `imagegen-<дата>.log` в папке логов MAUI (путь — `docs/knowledge/dev-environment.md`, «Логи») и найти строки с `ChooseAsync`.
   6. Определить исход и перейти:
      - **Исход A**: после щелчка мышью (п. 3) в журнале нет `ChooseAsync requested`, а после Enter (п. 4) есть → нажатие мышью перехватывает жест карточки → **шаг 4**.
      - **Исход B**: после щелчка есть `ChooseAsync requested`, лист на экране не появился (сразу `ChooseAsync answered: cancel`, или `answered` нет вовсе, или есть `ChooseAsync failed`) → лист действий на Windows не работает → **шаг 5**.
      - **Исход C**: нет `ChooseAsync requested` ни после щелчка, ни после Enter → команда не выполняется, хотя тесты шага 1 прошли. Записать в «Комментарии», выглядела ли «…» серой, поставить `blocked` и остановиться.
      - **Исход D**: лист действий появился и выбор пункта работает → не воспроизводится. Записать в «Комментарии» версию Windows и что делал, поставить `blocked` и остановиться (нужны шаги воспроизведения от владельца).
4. **Исход A: отключить `TouchBehavior` на десктопе** (`ChatPage.xaml`, `Border.Behaviors` карточки). У `toolkit:TouchBehavior` добавить `IsEnabled="{OnPlatform Default=False, Android=True, iOS=True}"`; `LongPressCommand` не трогать. Пересобрать, повторить пп. 3–5 шага 3: щелчок мышью по «…» должен дать `ChooseAsync requested` и лист действий. Проверить, что щелчок по карточке по-прежнему выбирает её (пункты меню «Card» в строке меню действуют на неё) и правый клик открывает меню. Если после правки исход стал B → **шаг 5**; если «…» всё ещё не реагирует или компилятор не принимает `IsEnabled` у `TouchBehavior` → `blocked`. Иначе → **шаг 6**.
5. **Исход B: «…» на Windows открывает меню карточки.**
   - `ChatPage.xaml`, кнопка «…» в блоке `<!-- Card actions: ... -->` (`Button StyleClass="ghost,icon"` с `IconGlyphs.More`): убрать `Command="{Binding ShowActionsCommand}"`, добавить `Clicked="OnMoreClicked"` и `IsEnabled="{Binding IsFinished}"` (правило 069: у незавершённой карточки «…» недоступна). `SemanticProperties.Description` оставить.
   - `ChatPage.xaml.cs`: новый метод `private void OnMoreClicked(object? sender, EventArgs e)`. Если `sender` не `Button { BindingContext: GenerationItemViewModel card } button` — выйти. Под `#if WINDOWS`: подняться по `Element.Parent` от кнопки до первого `Border`, у которого `FlyoutBase.GetContextFlyout(border)` не `null`; взять `border.Handler?.PlatformView as Microsoft.UI.Xaml.FrameworkElement` и его `ContextFlyout`; взять `button.Handler?.PlatformView as Microsoft.UI.Xaml.FrameworkElement`; если оба не `null` — `ContextFlyout.ShowAt(<платформенная кнопка>)` и выйти. Под `#else` (и если на Windows что-то из этого `null`): если `card.ShowActionsCommand.CanExecute(null)` — `card.ShowActionsCommand.Execute(null)`.
   - Пересобрать Windows-цель, повторить п. 3 шага 3: должно открыться меню из девяти пунктов (как по правому клику) у кнопки; выбрать «Copy prompt» и вставить в поле ввода — текст промпта карточки. Если меню не открылось: записать в «Комментарии», какое из значений (`Border`, его `ContextFlyout`, платформенная кнопка) оказалось `null` (проверить отладчиком или временным `System.Diagnostics.Debug.WriteLine`, удалить после), поставить `blocked` и остановиться.
   - Собрать Android-цель (`#if WINDOWS` не должен ломать Android).
6. **Документация.**
   - `docs/knowledge/maui.md`, раздел «Меню и ускорители (задача 073)»: новый пункт с найденным фактом (исход A: «`TouchBehavior` CommunityToolkit.Maui 15.0.1 на Windows перехватывает щелчок по кнопке внутри карточки; на десктопе выключен через `OnPlatform`»; исход B: «`DisplayActionSheetAsync` на Windows из `Shell.Current` не показывает лист: <что видно в журнале>; «…» открывает платформенный `ContextFlyout` карточки через `ShowAt`»). Пометить «проверено на Windows, <дата>».
   - «Отчёт»: исход шага 3, строки журнала, что изменено.
   - Ручные пункты — см. «Ручная проверка».

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
bash scripts/check.sh
```
(Windows-цель — только на Windows; на Linux её собирает CI.)

### Ручная проверка
Дописать в `tasks/v0.1/016-skeleton-manual-check.md` (MAUI под Windows) новым пунктом:
1. На карточке Done щелчок по «…» открывает меню действий; выбор «Copy prompt» копирует промпт.
2. На карточке в очереди или в работе «…» недоступна.
3. Правый клик по карточке открывает то же меню; щелчок по карточке выбирает её (меню «Card» в строке меню действует на неё).
В «Отчёт»: «Android: долгое нажатие и «…» на карточке открывают лист действий — не проверено, проверить владельцу».

Файлы: `ImageGen/ImageGen.Presentation.Tests/PageModels/GenerationItemViewModelTests.cs`, `ImageGen/ImageGen.Maui/Services/ShellDialogService.cs`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml` (исход A или B), `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml.cs` (исход B), `docs/knowledge/maui.md`, `tasks/v0.1/016-skeleton-manual-check.md`.

## Отчёт

## Комментарии
### 2026-10-29, claude-plan
Вопрос владельцу: если причина в системном листе действий на Windows, можно ли, чтобы «…» на Windows открывала то же контекстное меню, что правый клик (как в Avalonia), а лист действий остался на телефонах? Допущение в плане: да.
### 2026-10-29, владелец (в чате, записал claude-plan)
План одобрен; допущения из комментариев claude-plan приняты как решения. План переписан под небольшую модель.
