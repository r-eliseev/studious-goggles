---
id: 177
status: planned
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
- При запуске приложения (в инициализации хранилища) удаляются осиротевшие `*.tmp`, созданные писателем (по шаблону имени `.<имя>.<32 hex>.tmp`) и старше 1 часа, в `images/`; чужие файлы и свежие временные (идёт запись) не трогаются.
- Тесты Data на оба пункта; существующие тесты 141 зелёные.

## Анализ
- `ImageGen/ImageGen.Data/AtomicImageFileWriter.cs`: писатель не имеет логгера; `FileImageStore` его создаёт и логгер имеет.
- Инициализация: `AppInitializer` вызывает `SqliteDatabase.InitializeAsync` и восстановление генераций; подходящее место для очистки — `FileImageStore` (новый метод), вызываемый там же.

## План
Сначала падающий тест (ub-mode).
1. Тесты писателя: удаление временного файла бросает `IOException` → наружу выходит исходная ошибка записи, в журнале одна Warning; удаление бросает неожиданное исключение (например, `InvalidOperationException`) → оно не глушится.
2. Писатель получает `ILogger` (из `FileImageStore`); оставить `catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)` с Warning.
3. `FileImageStore.CleanupOrphanedTemporaryFilesAsync(TimeSpan olderThan, ct)`: обход `images/` по шаблону имени, удаление старше порога, Warning при ошибке удаления, без падения. Вызов из `AppInitializer` после инициализации БД.
4. Тесты: свежий `.tmp` остаётся, старый удаляется, чужие файлы и картинки не трогаются, ошибка удаления не валит запуск.
5. Проверки: Data.Tests, Presentation.Tests (AppInitializer), `dotnet format`, `scripts/check.sh`.

Файлы: `ImageGen/ImageGen.Data/AtomicImageFileWriter.cs`, `ImageGen/ImageGen.Data/FileImageStore.cs`, `ImageGen/ImageGen.Presentation/Services/AppInitializer.cs`, тесты Data и Presentation.

## Отчёт

## Комментарии

### 2026-11-01, владелец (в чате, записал claude-audit)
Завести задачу на мелочи 141 (выбрано в вопросе).

### 2026-11-01, claude-audit — ревью PR #62
Та же атомарная запись есть в `DefaultAssistantModelFile.SaveAsync` (172, `ImageGen/ImageGen.Presentation/Settings/DefaultAssistantModelFile.cs:47`), но там наоборот ловится только `IOException`: `UnauthorizedAccessException` при удалении временного файла выйдет из `finally` и заменит исходную ошибку записи. Предлагаю привести к тому же правилу (ловить `IOException` и `UnauthorizedAccessException`, Warning) и учесть `defaults.json.*.tmp` при очистке осиротевших файлов. Это ещё один файл задачи.
