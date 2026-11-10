---
id: 225
status: done
assignee:
claimed:
created: 2026-11-07
---

# MAUI: карточки прогона на телефоне — одна колонка по центру

## Описание
Отчёт владельца 2026-11-07 (скриншоты видео и картинки): левый край карточки и кнопка «Copy» над ней обрезаны — содержимое прогона сдвинуто влево за границу ленты. Карточки прогона лежат в `FlexLayout Wrap="Wrap"` внутри `Border` прогона в `CollectionView` (`ChatPage.xaml`, шаблон `RunTemplate`); тот же `FlexLayout` с переносом уже ломал раскладку шапки (212). На узком экране помещается одна карточка (ширина 280), перенос не нужен.

Зависит от: 220 (тот же шаблон карточки; делать после её слияния).
Среда: Linux (тест разметки, сборка MAUI Android); проверка — 140.

## Критерии готовности
- В узкой раскладке (`IsWide = false`) карточки прогона — в `VerticalStackLayout` (`Spacing="8"`), каждая карточка `HorizontalOptions="Center"`, без отрицательных и асимметричных внешних отступов (`Margin="0,0,8,8"` заменить на симметричный или убрать — отступы задаёт `Spacing`). В широкой раскладке — прежний `FlexLayout` с переносом (на десктопе несколько карточек в ряд).
- Переключение — без дублирования шаблона карточки: шаблон карточки вынести в ресурс страницы (`DataTemplate x:Key="GenerationCardTemplate"`) и использовать в обоих контейнерах; видимость контейнеров — по `IsWide` (как в 212).
- Кнопка «Copy» и подпись прогона не обрезаются.
- Тест разметки: шаблон карточки определён один раз и используется в обоих контейнерах; у карточки `HorizontalOptions="Center"`.
- Сборка MAUI Android; пункт 140: карточки картинки, видео и аудио по центру, края и рамка видны целиком, «Copy» видна целиком.

## Анализ
`ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`: `RunTemplate` (≈89–…), `FlexLayout Wrap="Wrap" … BindableLayout.ItemsSource="{Binding Generations}"`, карточка `Border x:Name="CardRoot" WidthRequest="280" Margin="0,0,8,8"`. `x:Reference CardRoot` в `TouchBehavior` (211) должен продолжать работать в вынесенном шаблоне (имена внутри шаблона — своя область имён). `IsWide` — `ChatPage.xaml.cs`.

## План
1. Падающий тест разметки.
2. Шаблон карточки в ресурс; два контейнера по `IsWide`; центрирование и отступы.
3. Сборка Android, пункт 140.

Файлы: `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, при необходимости `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml.cs`, `ImageGen/ImageGen.Presentation.Tests/Design/MauiChatPageMarkupTests.cs`, `tasks/backlog/140-android-manual-check.md`.

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

- Extracted the generation card into the page resource `GenerationCardTemplate` and reused it from both layouts. Wide windows keep the wrapping `FlexLayout`; narrow windows use one centered `VerticalStackLayout` with 8 dp spacing. The card now has centered horizontal alignment and no asymmetric margin.
- Exposed the page's measured `IsWide` state as a typed `ChatPage` property so the two layout bindings remain compiled.
- Added markup coverage for the single shared template, both hosts, breakpoint binding and card centering. Added the phone layout scenario to task 140.
- Verification passed: Presentation tests (543), MAUI Android build (0 warnings/errors), formatting for Presentation.Tests and MAUI, and `bash scripts/check.sh`.
- Manual visual verification on an Android phone remains in task 140.

## Комментарии

### 2026-11-07, claude-audit
Создано по отчёту владельца («у видео та же проблема с центровкой и пропорциями»; скриншот — обрезан левый край карточки). Пропорции видео — в 220. План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
План одобрен ранее данным поручением «заапрувь все таски которые будут».
