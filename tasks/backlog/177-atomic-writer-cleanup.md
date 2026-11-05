---
id: 177
status: completed
assignee:
claimed:
created: 2026-11-01
---

# Атомарная запись: явная обработка сбоя очистки и осиротевшие .tmp

## Описание
Найдено при ревью 2026-11-01 (задача 141, `09d5533`), мелочи.
- `AtomicImageFileWriter.WriteAsync`, блок `finally`: три `catch` подряд — `IOException`, `UnauthorizedAccessException` и `Exception`; последний делает первые два лишними и молча глушит любую ошибку очистки временного файла (журнала нет).
- Временные файлы `.<имя>.<guid>.tmp` создаются рядом с картинкой; если процесс упадёт между созданием и `File.Move`, файл остаётся в `images/<session>/` навсегда (скрытый, место не освобождается).

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- Сбой удаления временного файла пишется одной записью Warning (тип исключения и путь относительно папки данных, без содержимого) и не заменяет исходную ошибку записи; ловятся только ожидаемые исключения файловой системы.
- Тесты Data; существующие тесты 141 зелёные. (Очистка осиротевших `.tmp` при запуске — задача 190.)

## Анализ
- `ImageGen/ImageGen.Data/AtomicImageFileWriter.cs`: писатель не имеет логгера; `FileImageStore` его создаёт и логгер имеет.
- Инициализация: `AppInitializer` вызывает `SqliteDatabase.InitializeAsync` и восстановление генераций; подходящее место для очистки — `FileImageStore` (новый метод), вызываемый там же.

## План
Удаление осиротевших `.tmp` при запуске вынесено в задачу 190; `defaults.json` — в 175. Здесь только `AtomicImageFileWriter`.

1. Падающие тесты в `ImageGen/ImageGen.Data.Tests/AtomicImageFileWriterTests.cs` (писатель уже принимает подмены `writeAsync` и `deleteFile`):
   - `writeAsync` бросает `IOException("write")`, `deleteFile` бросает `IOException("delete")` → наружу `IOException` с сообщением `write`; логгер получил ровно одну Warning;
   - то же с `UnauthorizedAccessException` при удалении → тот же результат;
   - `deleteFile` бросает `InvalidOperationException` → оно выходит наружу (не глушится).
   Логгер — тестовый двойник из `ImageGen/ImageGen.Data.Tests/TestDoubles/` (если подходящего нет — простой класс-накопитель `ILogger` в тестах).
2. Конструктор `AtomicImageFileWriter` получает необязательный `ILogger? logger = null` первым новым параметром после существующих. В `finally` оставить один `catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)` с `logger?.LogWarning("Could not remove temporary image file {FileName} ({ExceptionType}).", Path.GetFileName(temporaryPath), exception.GetType().Name)`. Два прочих `catch` удалить.
3. `FileImageStore`: поле `atomicWriter` создавать в конструкторах (`new AtomicImageFileWriter(logger: logger)`), а не инициализатором поля.

Файлы: `ImageGen/ImageGen.Data/AtomicImageFileWriter.cs`, `ImageGen/ImageGen.Data/FileImageStore.cs`, `ImageGen/ImageGen.Data.Tests/AtomicImageFileWriterTests.cs`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj --verify-no-changes
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

- `AtomicImageFileWriter` получил необязательный логгер; ожидаемые ошибки удаления (`IOException`, `UnauthorizedAccessException`) записываются одной Warning с именем временного файла и типом исключения. Неожиданные ошибки больше не поглощаются.
- `FileImageStore` создаёт писатель с собственным логгером.
- TDD: новые тесты сначала упали на отсутствующем параметре логгера; после реализации все тесты писателя прошли.
- Проверки: `dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj` — 126/126; форматирование Data и Data.Tests — прошло; `bash scripts/check.sh` — прошло.
- Предупреждение содержит только имя временного файла относительно каталога, где лежат изображения, и тип исключения; содержимое файла и абсолютный путь не журналируются.

## Комментарии

### 2026-11-01, владелец (в чате, записал claude-audit)
Завести задачу на мелочи 141 (выбрано в вопросе).

### 2026-11-01, claude-audit — ревью PR #62
Та же атомарная запись есть в `DefaultAssistantModelFile.SaveAsync` (172, `ImageGen/ImageGen.Presentation/Settings/DefaultAssistantModelFile.cs:47`), но там наоборот ловится только `IOException`: `UnauthorizedAccessException` при удалении временного файла выйдет из `finally` и заменит исходную ошибку записи. Предлагаю привести к тому же правилу (ловить `IOException` и `UnauthorizedAccessException`, Warning) и учесть `defaults.json.*.tmp` при очистке осиротевших файлов. Это ещё один файл задачи.

### 2026-11-05, claude-audit
План переписан под Luna и сужен до обработки ошибок писателя. Очистка осиротевших `.tmp` — задача 190, `defaults.json` — 175. Критерий про очистку при запуске выполняется в 190.

### 2026-11-05, владелец (в чате, записал claude-audit)
План одобрен: «убедись, что все таски распланированы для Луны» — «и заапрувь их».
