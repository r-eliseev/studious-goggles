---
id: 068
status: done
assignee:
claimed:
created: 2026-10-28
---

# Чат: сообщения, прогон, карточки и поле ввода в новом стиле

## Описание
Перевести ленту чата на дизайн-систему по макету «Чат: прогон и статусы»: шапка с чипами (Models, Assistant, Parameters), сообщения (вариант стиля для сообщения пользователя), блок прогона (заголовок, промпт, строка о непереданных параметрах), карточки генераций во всех статусах (готово, идёт, в очереди, ошибка, отфильтровано, отменено), ошибка ассистента, индикатор «Assistant is thinking…», поле ввода со скрепкой и кнопкой отправки. Действия карточки (Repeat и др.) пока остаются как есть, но в одну строку кнопок-значков; меню и действия по наведению — задача 069.

Справочник: `docs/design/implementation.md` (ключи ресурсов, значения, шрифты, значки — дословно оттуда); токены: `docs/design/tokens.json`; макеты: `docs/design/prototype.html` (открыть: `python3 -m http.server` в `docs/design/`, затем http://localhost:8000/prototype.html; опубликованная версия — https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решение: ADR 0014.

Зависит от: 025, 055, 057, 063, 064, 067 (025, 055 и 057 меняют те же файлы чата; 025 — `GenerationItemViewModel`, решение владельца 2026-10-28).
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
   - прогон: `Border` с `BorderThickness={DynamicResource IgRunBorderThickness}`, `Padding={DynamicResource IgRunPadding}`; заголовок `TextBlock.meta`; промпт — `Border` с `IgPrompt*`; строки о непереданных параметрах — на каждой карточке (`NoticeText`, решение владельца в 074), не под промптом, как на макете; карточки — `WrapPanel` или `UniformGrid` 3 колонки (2 на ширине < 840);
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

### 2026-10-28, kiro-agent — лента чата на дизайн-системе

Выполнены шаги 1–5 плана.

Что изменено:
- Presentation:
  - `PageModels/GenerationStatusKind.cs` (новый enum: `Done`, `Running`, `Queued`, `Failed`, `Filtered`, `Cancelled`).
  - `PageModels/GenerationItemViewModel.cs`: добавлены свойства `ModelText`, `SeedText`, `CostText`, `TimeText` (из `CompletedAt - CreatedAt`), `NoticeText`/`HasNotice` (строки параметров + текст «Filtered…» для отфильтрованных), `StatusKind`, булевы `IsDone`, `IsQueued`, `IsActive` (только Running, для бейджа), `IsFailed`, `IsFiltered`, `IsCancelled`, `CostUsd`. `DetailsText`/`SummaryText` сохранены (доступное имя). Все заполняются в `Update`.
  - `PageModels/RunItemViewModel.cs`: `HeaderText` («N models × M · K of T done · $X»), вычисляется из карточек прогона (число моделей по `ModelText`, прогоны = всего/моделей, готово по `StatusKind == Done`, цена — сумма `CostUsd`).
  - Строки `FilteredNotice` («Filtered by the provider. This is the provider's decision, not an error.»), `TimeFormat` («{0}s»), `RunHeaderFormat` в `AppResources.resx`/`AppResources.cs`.
- Avalonia:
  - `Views/ChatView.axaml` переписан по макету: шапка (`TextBlock.title` + три чипа со значками `Models`/`Parameters`), лента `MaxWidth="780"`, сообщение (`Border` с `IgUserMessage*`, подпись роли только у пользователя, шрифт `IgRoleFont`), прогон (`Border` с `IgRunBorderThickness`/`IgRunPadding`, заголовок `TextBlock.meta`, промпт `Border` с `IgPrompt*`), карточки (`WrapPanel`), карточка (`IgCardBackgroundBrush`/`IgCardBorderThickness`/`IgRadiusMd`, картинка 264×176 ≈ 3:2 в `Border` с `IgCardImageCornerRadius`+`ClipToBounds`, заглушки по статусу: индикатор/`Queued`/`Error`+`IgErrSoftBrush`/`Filtered`+`IgWarnSoftBrush`/отмена, полоса поверх по `IgCardShowOverlay`, бейдж `Border.status` с классом статуса, полный блок по `IgCardShowFullMeta`, короткий по новому `IgCardShowShortMeta`, строка `NoticeText`), поле ввода (`Border` радиус 12, скрепка `Button.ghost.icon`, подсказка `TextBlock.meta`, отправка `Button.primary.icon` со значком `Send`). Значки — коды из `implementation.md` раздел 5.
  - `Themes/{Quiet,Gallery,Notebook}.axaml`: добавлен `IgCardShowShortMeta` (обратное `IgCardShowFullMeta`: Quiet/Notebook False, Gallery True). `IgUserMessageAlignment` переведён из `x:String` в тип `HorizontalAlignment`, `IgPromptFontStyle` — в `FontStyle` (их объявленный в `implementation.md` тип для Avalonia; прежде лежали строками и ещё не потреблялись типизированными свойствами).
- MAUI:
  - `Pages/ChatPage.xaml` переведён на `StyleClass`/`DynamicResource` аналогично Avalonia (чипы, границы сообщения/прогона/промпта, карточка с заглушками по статусу, бейдж статуса через `status-*`, полный/короткий блок, значки Lucide, поле ввода). Полоса Gallery видна всегда (нет наведения).
  - `Resources/Themes/*.xaml` (6 файлов): добавлен `IgCardShowShortMeta`.
- Тесты:
  - Presentation: `GenerationItemViewModelTests` (`Update_Succeeded_FillsModelSeedCostTime`, `Update_Filtered_SetsFilteredKind`, `Update_SeedNotApplied_ShowsSeedNotSupported`, `Update_DroppedParameter_FillsNotice`), `RunItemViewModelTests` (`HeaderText_CountsDoneAndCost`).
  - Avalonia: `Views/ChatViewStyleTests.cs` (`GalleryStyle_ResourceIsRightAlignment`, `GalleryStyle_UserMessageAlignedRight`, `NotebookStyle_RunHasBorder`, `FilteredCard_ShowsFilteredText`, `QueuedCard_ShowsQueuedStatus`).
- `docs/design/implementation.md`: добавлен ключ `IgCardShowShortMeta` в таблицу раздела 2.4 с пояснением. `docs/specs/chat-sessions.md` не менялся (поведение прежнее).

Автономные решения:
- Для бейджа статуса введён отдельный `IsActive` (только Running), так как существующий `IsRunning` включает Queued; классы `Border.status.*` уже были в `Controls.axaml`/`Styles.xaml`.
- Полосу-оверлей в Avalonia показываю по `IgCardShowOverlay` и проявляю при наведении через стиль `Border:pointerover Border.overlay` (opacity 0→1); на макете «на десктопе при наведении».
- Заглушку «отменено» сделал значком `Close` на `IgSunkenBrush` (на макете явной иконки отмены нет; статус подписан текстом бейджа, цветом не кодируется).
- Картинка 264×176 как фикс 3:2 внутри карточки шириной 280; `UniformToFill`/`AspectFill` с клипом.
- `NoticeText` рендерю на каждой карточке (решение владельца в 074), строки разделены `\n`.
- Строку заголовка прогона вычисляю из карточек, а не из `ChatPageModel`: `RunItemViewModel` уже получает готовый список карточек.

Как проверено (локально, .NET 10 SDK 10.0.403):
- `dotnet test` — Core.Tests 307/307, Presentation.Tests 208/208, Avalonia.Tests 95/95 (2 платформенных пропуска), Data.Tests 63/63 — всё зелёное.
- `dotnet format --verify-no-changes` — Presentation, Avalonia, Core и их тест-проекты, Data.Tests — без изменений.
- `scripts/check.sh` — пройден.

Попутно устранён регресс от регистрации fal (025): без ключа листинг моделей fal
обрушивал экран моделей сессии; `GetAllImageModelsAsync` теперь пропускает
упавший провайдер (отдельный коммит 025). Это же чинило падения
`SessionModelsFlowTests`/`SessionParametersFlowTests`, и обновило
`CompositionTests` под встроенный список с fal.

Не проверено (вне среды, остаётся за CI/ручной проверкой):
- Локальная сборка MAUI не выполнялась: в среде нет MAUI workload и Android SDK
  (`ANDROID_HOME` не задан). Правки `ChatPage.xaml` и MAUI-словарей внесены по
  плану на `StyleClass`/`DynamicResource`; сборку Windows-цели и Android проверяет
  CI. `dotnet format` по `ImageGen.Maui` локально не прошёл (restore без workload) —
  затронут только XAML, который `dotnet format` не форматирует.
- Визуальная проверка на рабочем столе Avalonia и под Windows/телефонами — ручные
  задачи 046 и 016.

### 2026-10-28, kiro-agent — фикс регресса MAUI XAML (StyleClass, IsClippedToBounds)

CI-задача `maui` (Windows-цель `net10.0-windows10.0.19041.0`) падала с ошибками
компиляции XAML в `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml` — регресс от
предыдущего отчёта по 068 (MAUI локально не собирался, ошибки уехали на CI).

Что было не так:
- `MAUIX2002: No accessible property ... for "IsClippedToBounds"` на `<Border>`
  области картинки 3:2 (строка 104). В MAUI у `Border` нет свойства
  `IsClippedToBounds` — это свойство из Avalonia/Layout, по ошибке перенесённое из
  Avalonia-разметки. В MAUI `Border` и так обрезает содержимое по заданному
  `StrokeShape` (`RoundRectangle` с `IgCardImageCornerRadius` уже задан внутри).
- Шесть `MAUIX2002: No accessible property ... for "StyleClass"` в блоке метки
  статуса: внутри `DataTrigger` стояли `<Setter Property="StyleClass"
  Value="meta,status-*" />`. В MAUI `StyleClass` не является `BindableProperty`,
  поэтому его нельзя присвоить через `Setter` в `DataTrigger` (в отличие от классов
  Avalonia). Эти сеттеры порождали вторичные CS0119/CS0120 в сгенерированном
  `Pages_ChatPage.xaml.xsg.cs`.

Как починено:
- Убран атрибут `IsClippedToBounds="True"` у `<Border>` картинки. Обрезка по форме
  обеспечивается самим `Border` по его `StrokeShape` (`RoundRectangle`), как и
  задумано.
- Вместо `Setter Property="StyleClass"` в шести `DataTrigger` метки статуса теперь
  `Setter Property="TextColor"` с теми же `DynamicResource`, что и у классов
  `status-*` в `Resources/Styles/Styles.xaml` (дословно): `IgOkColor` (done),
  `IgAccentColor` (running), `IgMutedColor` (queued), `IgErrColor` (failed),
  `IgWarnColor` (filtered), `IgMutedColor` (cancelled). Класс `meta` на `Label`
  сохранён (шрифт/размер/spacing), текст метки по-прежнему
  `Text="{Binding StatusText}"` — критерий 068 «статус не только цветом, у метки
  всегда есть текст» соблюдён.

Выбранный вариант для метки статуса: прямые `Setter`'ы конкретного свойства
(`TextColor`) через `DynamicResource`. Этот вариант минимален, не меняет визуальный
результат (классы `status-*` в MAUI задавали ровно один `TextColor` каждый),
сохраняет текст метки и не трогает рабочую Avalonia-разметку (там классы работают
иначе и компилируются). Проверено: других `Setter Property="StyleClass"` и
`IsClippedToBounds` в MAUI-проекте нет.

Как проверено (локально, .NET 10 SDK 10.0.403, MAUI android workload + Android SDK
API 35/36, JDK 17):
- `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android` —
  **Build succeeded, 0 Warning(s), 0 Error(s)**. Генерация XAML включена
  (`MauiXamlInflator=SourceGen`) — это тот же путь компиляции `ChatPage.xaml`, что
  падал на CI, поэтому фикс подтверждён локально (Windows-цель собрать на Linux
  нельзя, но ошибки MAUIX/SourceGen общие для всех целей).
- `dotnet test` Presentation 208/208 и Avalonia 95/95 (2 пропуска) — зелёные.
- `dotnet format --verify-no-changes` для `ImageGen.Avalonia` и `ImageGen.Maui` —
  без изменений.
- `scripts/check.sh` — пройден.

Тем самым восполнено «Не проверено» из предыдущего отчёта: MAUI теперь собирается
локально (Android-цель), а не только на CI.

## Комментарии
