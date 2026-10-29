---
id: 113
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Не скрывать сбой сохранения состояния генерации

## Описание
Найдено ревью 2026-10-29. `GenerationService.SaveAndNotifyAsync` ловит любое исключение `UpdateGenerationAsync`, пишет log и возвращается как после успеха. Если файл уже сохранён, а terminal update БД не прошёл, задача считается завершённой, UI не получает событие, в БД остаётся `Running`, а на диске — недостижимый файл.

Зависит от: 111.
Среда: Linux или Windows.

## Критерии готовности
- Ошибка записи состояния не считается успешным завершением фоновой задачи.
- Временный сбой update повторяется ограниченно: максимум три попытки с короткой задержкой через `TimeProvider`.
- Событие публикуется ровно один раз и только после успешной записи.
- После исчерпания попыток ошибка логируется один раз на Error; task supervisor наблюдает исключение и не создаёт `UnobservedTaskException`.
- Если terminal `Succeeded` не удалось сохранить после записи файла, файл удаляется best-effort, чтобы не оставлять orphan; ошибка удаления логируется отдельно.
- Тесты проверяют успех со второй попытки, полный отказ и отказ cleanup.

## Анализ
- `SaveAndNotifyAsync` сейчас подавляет persistence exception на любом переходе, включая Running и terminal.
- `Launch` удаляет task из `_running` в `finally`, но не имеет supervisor catch.
- Полной атомарности SQLite+filesystem нет. В этой задаче применяется ограниченный retry и компенсация файла; новую БД-схему/outbox не добавлять.
- После 106/108/111 `Launch` и cancellation могли измениться; исполнитель обязан сначала перечитать актуальный код.

## План
Для исполнителя (Haiku/Luna): не вводи outbox, background queue или новую таблицу. Сделай минимальный retry+compensation. Тесты времени используют `TimeProvider`, не реальный многосекундный delay.

Прочитай: `GenerationService.cs`, `IRunRepository.cs`, `IImageFileStore.cs`, `GenerationServiceTests.cs`, `FileImageStoreTests.cs`.

1. Падающий тест: `UpdateGenerationAsync` один раз бросает `IOException`, затем успешен → метод делает две попытки, публикует одно событие, generation заканчивается успешно.
2. Падающий тест: три отказа terminal update после сохранённого изображения → `DeleteFileAsync` вызывается с сохранённым path, событие Succeeded не публикуется, task supervisor пишет Error и `WhenIdleAsync` завершается без fault.
3. Падающий тест: cleanup файла тоже падает → исходная persistence ошибка не теряется; обе ошибки видны в логах без prompt/content.
4. Выделить private persistence retry с константой 3. Между попытками использовать короткие интервалы и `TimeProvider`; отмена останавливает retry.
5. `SaveAndNotifyAsync` после последней неудачи должен бросить исходное исключение. Не публиковать событие до успешного update.
6. В `Launch` добавить последний supervisor catch для наблюдения ошибки после того, как специализированные пути уже попытались сохранить terminal status. Cancellation активной операции не логировать как Error.
7. Для failed terminal `Succeeded` вызвать best-effort удаление только файла этой generation; очистить in-memory `ImagePath/MediaType` после cleanup. Не удалять каталог сессии.
8. Убедиться, что ошибка сохранения промежуточного `Running` не запускает provider request: работа должна закончиться до платного вызова.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Core/Services/GenerationService.cs`, `ImageGen/ImageGen.Core.Tests/Services/GenerationServiceTests.cs`

## Отчёт

## Комментарии
