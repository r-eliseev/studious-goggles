---
id: 216
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-07T08:11:37Z
created: 2026-11-07
---

# MAUI: ресурсы оформления не того типа (предупреждения в журнале)

## Описание
В логах владельца при каждой отрисовке карточек: `Cannot convert Microsoft.Maui.Thickness to type 'System.Double'` (4 раза) и `Cannot convert Microsoft.Maui.Controls.SolidColorBrush to type 'Microsoft.Maui.Graphics.Color'` (2 раза). Значение ресурса не применяется (берётся значение по умолчанию), то есть рамки и цвет текста промпта отличаются от темы.

Найдено (на `07ff84a`):
- `Border StrokeThickness="{DynamicResource IgRunBorderThickness}"` и `StrokeThickness="{DynamicResource IgCardBorderThickness}"` в `ChatPage.xaml` — свойство `double`, а ресурсы объявлены как `<Thickness>` в темах (`Resources/Themes/*.xaml`).
- `Label TextColor="{DynamicResource IgPromptForegroundBrush}"` в `ChatPage.xaml` — свойство `Color`, ресурс — `SolidColorBrush`.

Зависит от: 213 (тот же `ChatPage.xaml`; делать после её слияния).
Среда: Linux (тест, сборка MAUI Android).

## Критерии готовности
- `IgRunBorderThickness` и `IgCardBorderThickness` во всех шести темах (`GalleryDark/Light`, `NotebookDark/Light`, `QuietDark/Light`) объявлены как `<x:Double>` с тем же числом; если где-то ещё эти ключи используются как `Thickness` (`rg -n "IgRunBorderThickness|IgCardBorderThickness" ImageGen/ImageGen.Maui`) — остановиться и описать в «Комментарии».
- Цвет текста промпта: в темах рядом с `IgPromptForegroundBrush` — `<Color x:Key="IgPromptForegroundColor">` с тем же цветом; `Label` использует его. Кисть оставить, если она ещё где-то используется.
- Поискать остальные несовпадения этого вида в MAUI-разметке (`StrokeThickness`, `TextColor`, `BackgroundColor`, `Color` с `DynamicResource` на ключи `*Thickness`/`*Brush`) и исправить так же; список — в «Отчёт».
- Тест в `ImageGen.Presentation.Tests/Design/` (XML): для каждого `StrokeThickness="{DynamicResource K}"` в `Pages/*.xaml` и `Controls/*.xaml` ресурс `K` во всех темах — `x:Double`; для каждого `TextColor="{DynamicResource K}"` — `Color`.
- Сборка MAUI Android; в журнале запуска на телефоне этих предупреждений нет (пункт 140).

## Анализ
Темы MAUI — `ImageGen/ImageGen.Maui/Resources/Themes/*.xaml` и `Common.xaml`; пример: `GalleryDark.xaml`, строки около 53–60 (`<Thickness x:Key="IgRunBorderThickness">0</Thickness>`, `<Thickness x:Key="IgCardBorderThickness">0</Thickness>`, `<SolidColorBrush x:Key="IgPromptForegroundBrush" Color="#EDEDEC" />`). Avalonia использует свои темы — не трогать.

## План
1. Падающий тест (XML-проверка типов ресурсов).
2. Правка тем и разметки.
3. Сборка Android, пункт 140.

Файлы: `ImageGen/ImageGen.Maui/Resources/Themes/*.xaml`, `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, при необходимости другие `Pages/*.xaml` и `Controls/*.xaml`, `ImageGen/ImageGen.Presentation.Tests/Design/MauiThemeResourceTypesTests.cs` (новый), `tasks/backlog/140-android-manual-check.md`.

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

## Комментарии

### 2026-11-07, claude-audit
Создано по отчёту владельца о сборке v0.1.11 на Android (MAUI, логи 2026-11-05 и 2026-11-06, скриншоты). План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
План одобрен ранее данным поручением «заапрувь все таски которые будут».
