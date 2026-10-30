---
id: 143
status: done
assignee:
claimed:
created: 2026-10-29
---

# Заменить устаревший TextBox.Watermark в Avalonia-оболочке

## Описание
Сборка `ImageGen.Avalonia` выдаёт предупреждение Avalonia `AVLN5001`: `TextBox.Watermark` устарел, нужно `PlaceholderText` (`ImageGen/ImageGen.Avalonia/Views/SessionsListView.axaml:30`). Предупреждение появляется при каждой сборке и заглушает настоящие предупреждения. Найдено при проверке среды на VPS 2026-10-29.

Зависит от: —.
Среда: Linux или Windows.

## Критерии готовности
- Сборка `ImageGen.Avalonia` и `ImageGen.Avalonia.Tests` проходит без `AVLN5001` и без новых предупреждений.
- Поведение поля не меняется: подсказка по-прежнему показывается, когда текст пуст.
- Другие файлы и свойства не затронуты.

## Анализ
Причина — устаревшее свойство в разметке, а не в коде. `PlaceholderText` — современное имя того же самого свойства в Avalonia; переименование не меняет поведение и не требует изменения логики или тестов.

Правило TDD здесь неприменимо в обычном виде: поведение не меняется, а проверяемое условие — отсутствие предупреждения компилятора. Поэтому «падающий тест» — это сборка с предупреждением до правки и без него после; отдельный новый тест не пишется.

## План
1. Зафиксировать исходное состояние: собрать `ImageGen.Avalonia` и убедиться, что в выводе есть `AVLN5001` на строке 30 `SessionsListView.axaml`.
2. В `ImageGen/ImageGen.Avalonia/Views/SessionsListView.axaml` заменить `Watermark` на `PlaceholderText` у `TextBox`. Больше ничего в разметке не менять.
3. Выполнить «Проверку»: предупреждение исчезло, сборка и тесты проходят, `dotnet format --verify-no-changes` для `ImageGen.Avalonia` не требует изменений.
4. «Отчёт»: исходный вывод сборки с `AVLN5001` и вывод после правки. Завершить через `scripts/release-task.sh 143`, затем `bash scripts/check.sh`.

### Проверка
```
dotnet build ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj 2>&1 | grep -c AVLN5001   # ожидается 0
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Avalonia/Views/SessionsListView.axaml`

## Отчёт
Причина — устаревшее свойство в разметке, а не в коде. `PlaceholderText` — современное имя того же свойства в Avalonia; поведение не меняется, логика и тесты не затронуты.

Что изменено: в `ImageGen/ImageGen.Avalonia/Views/SessionsListView.axaml` у `TextBox` `SearchBox` атрибут `Watermark` заменён на `PlaceholderText` (значение и всё остальное в разметке не тронуто).

До и после (сборка `dotnet build ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj`):
```
до:    AVLN5001 × 2 — 'TextBox.Watermark' is obsolete: Use PlaceholderText instead.
после: AVLN5001 × 0, всего предупреждений 0
```
Проверено: сборка успешна, `ImageGen.Avalonia.Tests` — 120 тестов, 0 упавших (2 пропущены, как и до правки: Secret Service и DPAPI), `dotnet format --verify-no-changes` для `ImageGen.Avalonia` — без изменений, `bash scripts/check.sh` проходит.

TDD в обычном виде неприменим: поведение не меняется, проверяемое условие — отсутствие предупреждения компилятора. «Падающая проверка» — сборка с предупреждением до правки и без него после.

## Комментарии
