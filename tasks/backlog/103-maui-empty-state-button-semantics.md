---
id: 103
status: planned
assignee:
claimed:
created: 2026-10-29
---

# MAUI: у кнопки «New session» на пустом экране нет описания для экранного диктора

## Описание
Найдено аудитом 2026-10-29 (`docs/log/2026-10-29-audit.md`): `ImageGen/ImageGen.Maui/Pages/SessionsPage.xaml` (блок `EmptyState`, кнопка с `Command="{Binding NewSessionCommand}"`) не задаёт `SemanticProperties.Description`, хотя `docs/conventions.md` требует его у всех кнопок. Остальные кнопки MAUI-разметки выборочно проверены — описание есть. Сверить Avalonia (`AutomationProperties.Name` у той же кнопки в `SessionsView.axaml`).

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- У кнопки задано описание из `AppResources` (та же строка, что и текст кнопки).
- Grep по `ImageGen.Maui/**/*.xaml` не находит других `Button`/`ImageButton` без `SemanticProperties.Description` (или исключения перечислены в «Отчёте» с причиной).
- То же в Avalonia для `AutomationProperties.Name`.

## Анализ
- Проверено скриптом (2026-10-29): во всей MAUI-разметке (`ImageGen.Maui/**/*.xaml`) единственный элемент `<Button`/`<ImageButton` без `SemanticProperties.Description` — кнопка «New session» в `Pages/SessionsPage.xaml` (около строки 39, блок `EmptyState`). Совпадения `<Button.Triggers>` и `<Button.ImageSource>` — это вложенные свойства, не кнопки.
- В Avalonia та же кнопка (`Views/SessionsView.axaml`, около строки 18) уже имеет `AutomationProperties.Name="{markup:Res NewSession}"`: правка не нужна.
- Пересечений с 088–101 по этому файлу нет.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, после каждого шага — сборка и проверки из «Проверки». Имена файлов, ключей строк и свойств переноси дословно. Если шаг не получается так, как написано, не придумывай обход: опиши в «Комментариях», что пробовал, поставь `blocked` и остановись.

Прочитай перед началом: `docs/conventions.md` (раздел про доступность), `ImageGen/ImageGen.Maui/Pages/SessionsPage.xaml`.

1. В `ImageGen/ImageGen.Maui/Pages/SessionsPage.xaml`, в `Border x:Name="EmptyState"`, у `Button` с `Command="{Binding NewSessionCommand}"` добавить атрибут `SemanticProperties.Description="{x:Static strings:AppResources.NewSession}"` (тот же ключ, что у `Text`). Других правок нет. Тест не пишется: проверка разметки — шаг 2.
2. Проверка, что других кнопок без описания нет: выполнить из корня репозитория
   `grep -rn "<Button\b\|<ImageButton\b" ImageGen/ImageGen.Maui --include=*.xaml | grep -v "<Button\.\|<ImageButton\." `
   и для каждой найденной строки убедиться, что внутри открывающего тега есть `SemanticProperties.Description`. Если найдётся ещё кнопка без него — добавить описание тем же способом (ключ строки — тот же, что у `Text`; если `Text` нет — ключ из соседнего `ToolTipProperties.Text` или `AutomationProperties`; если подходящего ключа нет — поставить `blocked` и описать в «Комментариях»).
3. «Отчёт»: перечислить изменённые кнопки и результат шага 2.

### Проверка
```
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
bash scripts/check.sh
```
(`dotnet format` для XAML не нужен: C#-файлы не меняются.)

### Ручная проверка
Владельцу (дописать в задачу 016): на пустом списке сессий в MAUI под Windows экранный диктор читает у кнопки «New session».

Файлы: `ImageGen/ImageGen.Maui/Pages/SessionsPage.xaml`, `tasks/v0.1/016-skeleton-manual-check.md` (пункт ручной проверки)

## Отчёт

## Комментарии
