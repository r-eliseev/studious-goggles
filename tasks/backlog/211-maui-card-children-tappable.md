---
id: 211
status: done
assignee:
claimed:
created: 2026-11-07
---

# MAUI: кнопки и долгое нажатие карточки не работают на Android (TouchBehavior)

## Описание
Отчёт владельца (v0.1.11, Android, MAUI): нажатие на картинку не открывает просмотр, «…» не открывает действия — карточка в статусе Done (скриншот). В логах ни навигации, ни ошибки: нажатие не доходит до команд.

Причина (проверено по исходникам CommunityToolkit.Maui 15.0.1, 2026-11-07): у `TouchBehavior` свойство `ShouldMakeChildrenInputTransparent` по умолчанию `true` (`TouchBehaviorDefaults.ShouldMakeChildrenInputTransparent = true`). При подключении к `Border` (`IContentView { Content: Layout }`) поведение ставит `InputTransparent = true` всем дочерним элементам содержимого (`TouchBehavior.methods.shared.cs`, `SetChildrenInputTransparent`). В карточке `ChatPage.xaml` поведение висит на внешнем `Border` (долгое нажатие), а внутри — прозрачная кнопка открытия картинки и кнопка «…»: обе становятся «прозрачными» для касаний. На Windows поведение выключено (`IsEnabled="{OnPlatform WinUI=False, Default=True}"`, задача 088) — поэтому там кнопки работают.

Вторая причина (отчёт владельца 2026-11-07: «длинное нажатие не работает, реакции не видно»): поведения MAUI не наследуют `BindingContext` элемента, к которому подключены, а `BasePlatformBehavior` CommunityToolkit 15.0.1 его тоже не задаёт (проверено по исходнику `Behaviors/PlatformBehaviors/BasePlatformBehavior.shared.cs`: `BindingContext` не упоминается). Поэтому `LongPressCommand="{Binding ShowActionsCommand}"` привязывается к `null` и команда пустая. Документация CommunityToolkit для `TouchBehavior` задаёт контекст явно через `x:Reference` на элемент-хозяин.

Зависит от: —
Среда: Linux (тест разметки, сборка MAUI Android); проверка на телефоне — 140.

## Критерии готовности
- У `TouchBehavior` карточки в `ChatPage.xaml` задано `ShouldMakeChildrenInputTransparent="False"`.
- Контекст поведения задан явно: у внешнего `Border` карточки `x:Name="CardRoot"`, у поведения `BindingContext="{Binding Source={x:Reference CardRoot}, Path=BindingContext, x:DataType=Border}"` (или эквивалент с компилируемой привязкой — сверить с тем, как в проекте уже привязываются к `x:Reference Page`); `LongPressCommand="{Binding ShowActionsCommand}"` с `x:DataType="pm:GenerationItemViewModel"`. Если компилятор XAML (`MauiXamlInflator=SourceGen`) не принимает такую запись — использовать `LongPressCommand="{Binding Source={x:Reference CardRoot}, Path=BindingContext.ShowActionsCommand}"` и записать выбор в «Отчёт».
- Проверено, нет ли других `TouchBehavior` с вложенными кнопками (`rg -n "TouchBehavior" ImageGen/ImageGen.Maui`); если есть — то же свойство.
- Тест разметки в `ImageGen/ImageGen.Presentation.Tests/Design/MauiChatPageMarkupTests.cs` (по образцу соседних проверок XAML как XML): у каждого `TouchBehavior` в `ChatPage.xaml` атрибут `ShouldMakeChildrenInputTransparent="False"` и явный источник привязки (`BindingContext` с `x:Reference` или `Source=` в командах).
- Два факта записаны в `docs/knowledge/maui.md` рядом с записью о `TouchBehavior` (088): поведение по умолчанию делает детей прозрачными для ввода (внутри карточек с кнопками — выключать); поведение не наследует `BindingContext` (задавать через `x:Reference`).
- Сборка MAUI Android без предупреждений; пункт 140: нажатие на картинку открывает просмотр, «…» — действия, долгое нажатие по подписи карточки — действия.

## Анализ
Долгое нажатие по самой кнопке-картинке после правки достаётся кнопке, а не карточке: оно сработает по остальной площади карточки (подписи, статус). Это приемлемо: действия доступны и через «…». Если владелец захочет долгое нажатие и по картинке — отдельная задача.

## План
1. Падающий тест разметки.
2. Атрибут и контекст привязки в `ChatPage.xaml`.
3. `docs/knowledge/maui.md`, пункт 140.

Файлы: `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Presentation.Tests/Design/MauiChatPageMarkupTests.cs`, `docs/knowledge/maui.md`, `tasks/backlog/140-android-manual-check.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
bash scripts/check.sh
```

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы» и названные тесты; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): падающий тест → запуск, убедиться, что он падает именно по описанной причине → минимальный фикс → тест зелёный.
- Не меняй файлы вне строки «Файлы», не переименовывай существующие члены сверх плана, не чини соседние находки — они в отдельных задачах.
- Если код расходится с планом (нет такого метода, другая сигнатура, тест уже есть), не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: что изменено, команды и результаты, отличия от плана.

## Отчёт

В `ChatPage.xaml` у карточки задан `x:Name="CardRoot"`; `TouchBehavior` получает явный `BindingContext` через `x:Reference`, тип команды указан как `GenerationItemViewModel`, а `ShouldMakeChildrenInputTransparent` выключен. Это сохраняет касания вложенной кнопки открытия изображения и привязывает long press к `ShowActionsCommand`. Других `TouchBehavior` в MAUI-разметке не найдено. В `docs/knowledge/maui.md` записаны подтверждённые свойства поведения; в ручную проверку 140 добавлены пункты для телефона.

Проверено: новый тест сначала падал из-за отсутствующего `ShouldMakeChildrenInputTransparent`, затем прошёл; Presentation.Tests — 500/500; MAUI Android build — 0 предупреждений и ошибок; форматирование Presentation.Tests и MAUI прошло. Проверка на устройстве остаётся в ручной задаче 140.

## Комментарии

### 2026-11-07, claude-audit
Создано по отчёту владельца о сборке v0.1.11 на Android (MAUI, логи 2026-11-05 и 2026-11-06, скриншоты). План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
План одобрен ранее данным поручением «заапрувь все таски которые будут».

### 2026-11-07, claude-audit
Добавлена вторая причина по отчёту владельца «длинное нажатие не работает»: поведение без `BindingContext`. План одобрен тем же поручением.
