---
id: 068
status: planned
assignee:
claimed:
created: 2026-10-28
---

# Чат: сообщения, прогон, карточки и поле ввода в новом стиле

## Описание
Перевести ленту чата на дизайн-систему по макету «Чат: прогон и статусы»: шапка с чипами (Models, Assistant, Parameters), сообщения (вариант стиля для сообщения пользователя), блок прогона (заголовок, промпт, строка о непереданных параметрах), карточки генераций во всех статусах (готово, идёт, в очереди, ошибка, отфильтровано, отменено), ошибка ассистента, индикатор «Assistant is thinking…», поле ввода со скрепкой и кнопкой отправки. Действия карточки (Repeat и др.) пока остаются как есть, но в одну строку кнопок-значков; меню и действия по наведению — задача 069.

Справочник: `docs/design/implementation.md` (ключи ресурсов, значения, шрифты, значки — дословно оттуда); токены: `docs/design/tokens.json`; макеты: `docs/design/prototype.html` (открыть: `python3 -m http.server` в `docs/design/`, затем http://localhost:8000/prototype.html; опубликованная версия — https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решение: ADR 0014.

Зависит от: 063, 064, 067; лучше после 055 и 057 (меняют те же файлы чата).
Среда: Linux или Windows (Avalonia — headless-тесты на Linux; MAUI — сборка Android на Linux с Android SDK, Windows-цель собирает CI; запуск под Windows — ручная проверка 016, Avalonia на рабочем столе — 046).

## Критерии готовности
- Стили различаются только ресурсами из `implementation.md`, раздел 2.4 (пузырь пользователя в Gallery, рамка прогона в Notebook и т. д.); в разметке нет проверок стиля.
- Карточка: картинка 3:2; для каждого статуса своя заглушка (идёт — индикатор; в очереди — значок `Queued`; ошибка — фон `IgErrSoftBrush`, значок `Error`, текст ошибки; отфильтровано — фон `IgWarnSoftBrush`, значок `Filtered`, текст «Filtered by the provider. This is the provider's decision, not an error.»); метка статуса — `Border.status` с классом статуса; модель, сид, цена и время — `IgFontMeta`.
- Статус не передаётся только цветом: у метки всегда есть текст.
- Gallery (`IgCardShowFullMeta` = False): под картинкой модель и цена; сид и время — в полосе поверх картинки (`IgCardShowOverlay` = True), на десктопе видна при наведении, на телефонах — всегда.
- Тесты из «Плана» проходят; тест доступности Avalonia проходит.

## Анализ
- Разметка: Avalonia `Views/ChatView.axaml` (шаблоны `MessageItemViewModel`, `RunItemViewModel`, `GenerationItemViewModel`); MAUI `Pages/ChatPage.xaml` и `PageModels/ChatItemTemplateSelector.cs`.
- В `GenerationItemViewModel` сейчас один `DetailsText` (модель · сид · цена · референсы · строки параметров). Для карточки нужны отдельные свойства: `ModelText`, `SeedText`, `CostText`, `TimeText` (время генерации, если есть в `Generation` — проверить `CompletedAt - CreatedAt`), `NoticeText` (строки параметров), `StatusKind` (enum `GenerationStatusKind`: `Done`, `Running`, `Queued`, `Failed`, `Filtered`, `Cancelled`) и булевы `IsQueued`, `IsFiltered`, `IsCancelled`. `DetailsText` и `SummaryText` оставить (доступное имя карточки).
- `RunItemViewModel.PromptText` — сейчас с префиксом «Prompt: » (`RunPromptFormat`); после 057 проверить, что промпт показывается один раз. Для заголовка прогона нужны `HeaderText` («2 models × 3 · 2 of 6 done · $0.028») — вычисляется из генераций.
- Сообщение об ошибке ассистента уже есть (`MessageItemViewModel.IsError`); кнопка «Try again» — задача 074 (новая функция), здесь не делается.
- Заглушка «идёт» с мерцанием — необязательна: достаточно индикатора (`ProgressBar IsIndeterminate`) внизу картинки.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, ключей и строк переноси дословно. Если шаг не получается так, как написано (компилятор не принимает разметку, API другое), не придумывай обход: опиши в «Комментариях», что пробовал и что получилось, поставь `blocked` и остановись.

1. Presentation: `PageModels/GenerationStatusKind.cs` (enum); в `GenerationItemViewModel` — свойства из «Анализа», заполняются в `Update`. `RunItemViewModel.HeaderText` (обновляется при изменении генераций: подписка на `PropertyChanged` генераций или пересчёт из `ChatPageModel`). Тесты в `GenerationItemViewModelTests`: `Update_Succeeded_FillsModelSeedCostTime`, `Update_Filtered_SetsFilteredKind`, `Update_SeedNotApplied_ShowsSeedNotSupported`, `Update_DroppedParameter_FillsNotice`; в `RunItemViewModelTests`: `HeaderText_CountsDoneAndCost`.
2. Avalonia `ChatView.axaml` по макету: шапка (`TextBlock.title`, три `Button` с `Border.chip`-видом и значками `Models`, `Parameters`), лента с максимальной шириной 780 по центру, шаблоны:
   - сообщение: `Border` с `Background={DynamicResource IgUserMessageBackgroundBrush}`, `HorizontalAlignment={DynamicResource IgUserMessageAlignment}`, `Padding`, `CornerRadius` — для пользователя; для ассистента — без фона; подпись роли `IsVisible={DynamicResource IgUserMessageRoleVisible}` только у пользователя, шрифт `IgRoleFont`;
   - прогон: `Border` с `BorderThickness={DynamicResource IgRunBorderThickness}`, `Padding={DynamicResource IgRunPadding}`; заголовок `TextBlock.meta`; промпт — `Border` с `IgPrompt*`; строка уведомлений `TextBlock.meta`; карточки — `WrapPanel` или `UniformGrid` 3 колонки (2 на ширине < 840);
   - карточка: `Border` с `IgCardBackgroundBrush`, `IgCardBorderThickness`, `IgRadiusMd`; картинка в `Border` с `IgCardImageCornerRadius` и `ClipToBounds`; заглушки по статусу; полоса поверх картинки (`IsVisible` = `IgCardShowOverlay` и `:pointerover`); блок данных (`IsVisible` = `IgCardShowFullMeta`) и короткий блок (обратное значение — через второй ресурс `IgCardShowShortMeta`, добавить в словари 061 и 062 и в `implementation.md`).
   - поле ввода: `Border` с радиусом `IgRadiusMd`+4, внутри вложения, `TextBox` без рамки, строка: скрепка (`Button.ghost.icon`), подсказка `TextBlock.meta`, кнопка отправки (`Button.primary.icon`, значок `Send`).
3. MAUI `ChatPage.xaml`: то же на `StyleClass` и `DynamicResource`; на телефонах сетка 2 колонки (`OnIdiom`), полоса Gallery видна всегда.
4. Тесты Avalonia `Views/ChatViewStyleTests.cs`: `GalleryStyle_UserMessageAlignedRight`, `NotebookStyle_RunHasBorder`, `FilteredCard_ShowsFilteredText`, `QueuedCard_ShowsQueuedStatus`; существующие `Flows/ChatFlowTests` проходят.
5. `docs/specs/chat-sessions.md` — не менять (поведение то же); `docs/design/implementation.md` — ключ `IgCardShowShortMeta`.

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

Файлы: `ImageGen/ImageGen.Presentation/PageModels/{GenerationItemViewModel,GenerationStatusKind,RunItemViewModel,ChatPageModel}.cs`, строки, `ImageGen/ImageGen.Avalonia/Views/ChatView.*`, `ImageGen/ImageGen.Avalonia/Themes/*.axaml`, `ImageGen/ImageGen.Maui/Pages/ChatPage.*`, `ImageGen/ImageGen.Maui/Resources/Themes/*`, тесты, `docs/design/implementation.md`

## Отчёт

## Комментарии
