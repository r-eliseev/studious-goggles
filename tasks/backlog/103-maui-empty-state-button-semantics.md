---
id: 103
status: in-progress
assignee: opencode-vps
claimed: 2026-10-30T02:40:33Z
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
- 095 также меняет `Pages/SessionsPage.xaml` (общий стиль keyboard inset). Участки независимы: сохранить стиль 095 и описание этой кнопки при rebase. Зависимость не требуется; одновременно задачу не брать, если файл уже занят другой веткой по `docs/tasks.md`.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, после каждого шага — сборка и проверки из «Проверки». Имена файлов, ключей строк и свойств переноси дословно. При расхождении с текущим API адаптируй имена и разметку в рамках контракта, исправь ошибку минимально и проверь сборкой. `blocked` нужен для неизвестного продуктового решения или недоступной обязательной среды, а не для рутинной ошибки компиляции.

Прочитай перед началом: `docs/conventions.md` (раздел про доступность), `ImageGen/ImageGen.Maui/Pages/SessionsPage.xaml`.

1. В `ImageGen/ImageGen.Maui/Pages/SessionsPage.xaml`, в `Border x:Name="EmptyState"`, у `Button` с `Command="{Binding NewSessionCommand}"` добавить атрибут `SemanticProperties.Description="{x:Static strings:AppResources.NewSession}"` (тот же ключ, что у `Text`). Других правок нет. Тест не пишется: проверка разметки — шаг 2.
2. Проверка открывающих тегов целиком: найти файлы через `rg --files ImageGen/ImageGen.Maui -g '*.xaml'`, разобрать XML (`XDocument` или Python `xml.etree.ElementTree`), выбрать элементы с локальным именем ровно `Button`/`ImageButton` и проверить атрибут `SemanticProperties.Description`. Не проверять отдельные строки grep: теги многострочные, а `Button.ImageSource` — свойство, не кнопка. Так же проверить `Button` Avalonia на `AutomationProperties.Name`, учитывая существующий style setter, если имя задано стилем. Проверка через одноразовый скрипт в `tmp/`, отдельный постоянный тест на один атрибут не нужен. Ожидается исправление только названной кнопки; если обнаружены другие пропуски, перечислить файлы/элементы и причины в «Отчёте», а не расширять задачу автоматически.
3. «Отчёт»: перечислить изменённые кнопки и результат шага 2.

Завершение: заполни «Отчёт» с изменениями, командами и результатами проверок, передай ручные сценарии в 016 (Windows), 046 (Avalonia), 140 (Android) по применимости. Выполни `scripts/release-task.sh 103`, затем `bash scripts/check.sh`. Недоступная ручная проверка не заменяет сборку и не оставляет готовую задачу в `in-progress`.

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

### 2026-10-29, владелец (в чате, записал claude-plan)
План одобрен; допущения из комментариев claude-plan приняты как решения. План переписан под небольшую модель.
