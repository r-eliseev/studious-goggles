---
id: 199
status: done
assignee:
claimed:
created: 2026-11-06
---

# Вид результата генерации и потоковое сохранение видео/аудио

## Описание
ADR 0027, пп. 2, 3, 8. Первый шаг видео и музыки: домен и хранилище узнают вид результата, а файлы видео и аудио пишутся потоком. Поведение картинок не меняется.

Зависит от: —
Среда: Linux или Windows.

## Критерии готовности
- `MediaKind { Image = 0, Video = 1, Audio = 2 }` в `ImageGen.Core/Domain/Generations/`; `Generation.Kind` (по умолчанию `Image`) и `Generation.DurationSeconds` (`double?`).
- Миграция `NNNN_generation_media_kind.sql` (номер — следующий свободный): `kind INTEGER NOT NULL DEFAULT 0`, `duration_seconds REAL NULL` в `generations`; `RunRepository` пишет и читает оба поля (список колонок, `INSERT`, `UPDATE`, `ReadGeneration`, параметры).
- `IImageFileStore.SaveMediaStreamAsync(Guid sessionId, Guid generationId, Stream content, string mediaType, long maxBytes, CancellationToken)` → относительный путь; реализация в `FileImageStore`: разрешённые типы и расширения `video/mp4`→`.mp4`, `video/webm`→`.webm`, `audio/mpeg`→`.mp3`, `audio/wav`→`.wav`, `audio/ogg`→`.ogg`, `audio/flac`→`.flac`; иначе `ArgumentException`; запись во временный файл рядом и `File.Move` (как `AtomicImageFileWriter`, его шаблон имени `.<имя>.<guid>.tmp`, чтобы работала очистка 190); при превышении `maxBytes` — исключение `ProviderException(ErrorKind.ResponseTooLarge…)` (взять тот вид ошибки, что уже используется для слишком больших ответов; найти `rg -n "TooLarge" ImageGen/ImageGen.Core`) и временный файл удалён.
- `ProviderResponseLimits.Video = 500 МиБ`, `ProviderResponseLimits.Audio = 100 МиБ` (константы рядом с `Image`).
- `ExpenseKind.Video`, `ExpenseKind.Music` добавлены в конец перечисления; `RunRepository` при записи расхода генерации берёт вид: `Image`→`ExpenseKind.Image`, `Video`→`Video`, `Audio`→`Music`.
- `GalleryRepository` показывает только `kind = 0` (галерея — картинки, ADR 0027 «Последствия»).
- Тесты Core и Data на всё перечисленное; прежние тесты зелёные.

## Анализ
- `Generation` — `ImageGen/ImageGen.Core/Domain/Generations/Generation.cs`; копия для событий — `GenerationService.Snapshot` (добавить новые поля туда тоже).
- Колонки — константа `GenerationColumns` и методы `RunRepository`; расход — блок `ExpenseRepository.UpsertAsync` в `UpdateGenerationAsync` (`Kind = ExpenseKind.Image` сейчас).
- `FileImageStore.SaveAsync(sessionId, generationId, byte[] content, mediaType, ct)` — образец пути `images/<session>/<generation>.<ext>` и записи через `atomicWriter`; для потока писать в `FileStream` временного файла, считая байты, и прерываться при превышении лимита.
- Галерея: условия в `GalleryRepository` (`g.status = @succeeded`, `g.image_path IS NOT NULL`, …) — добавить `g.kind = 0`.

## План
1. Падающие тесты:
   - Data `RunRepositoryTests`: генерация с `Kind = Video`, `DurationSeconds = 8` сохраняется и читается; старая запись без колонок (после миграции) читается как `Image`/`null`.
   - Data `MigrationsTests` (если там перечислены миграции) — новая миграция применяется.
   - Data `FileImageStoreTests`: поток 1 КБ `video/mp4` → файл `images/<s>/<g>.mp4` с тем же содержимым, временных файлов нет; `audio/mpeg` → `.mp3`; `text/plain` → `ArgumentException`, файла нет; поток больше `maxBytes` → исключение, ни итогового, ни временного файла.
   - Data `ExpenseRepositoryTests`: успешная генерация `Video` с ценой → запись расхода `ExpenseKind.Video`.
   - Data `GalleryRepositoryTests`: успешная генерация `Video` не попадает в галерею.
2. Реализация по «Критериям».
3. Если `MigrationsTests`/`SqliteDatabaseTests` проверяют номер последней версии — обновить ожидание.

Файлы: `ImageGen/ImageGen.Core/Domain/Generations/MediaKind.cs` (новый), `ImageGen/ImageGen.Core/Domain/Generations/Generation.cs`, `ImageGen/ImageGen.Core/Domain/Expenses/ExpenseKind.cs`, `ImageGen/ImageGen.Core/Providers/ProviderResponseLimits.cs`, `ImageGen/ImageGen.Core/Storage/IImageFileStore.cs`, `ImageGen/ImageGen.Core/Services/GenerationService.cs` (`Snapshot`), `ImageGen/ImageGen.Data/Migrations/NNNN_generation_media_kind.sql` (новый), `ImageGen/ImageGen.Data/Repositories/RunRepository.cs`, `ImageGen/ImageGen.Data/Repositories/GalleryRepository.cs`, `ImageGen/ImageGen.Data/FileImageStore.cs`, тесты `ImageGen/ImageGen.Data.Tests/` (перечисленные выше) и тестовые реализации `IImageFileStore` (`rg -n ": IImageFileStore" ImageGen`).

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
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

Добавлены `MediaKind`, `Generation.Kind`/`DurationSeconds` и миграция 0014 с безопасными значениями по умолчанию для старых строк. RunRepository читает и сохраняет новые поля и относит расходы к Video/Music; галерея и список моделей отбирают только изображения. `IImageFileStore.SaveMediaStreamAsync` поддерживает шесть разрешённых MIME-типов, пишет через ограниченный поток во временный файл рядом с результатом и атомарно переименовывает его; превышение лимита удаляет временный файл. Добавлены лимиты видео 500 МиБ и аудио 100 МиБ. `GenerationService.Snapshot` переносит новые свойства.

Проверки: Core — 745 тестов, Data — 138, Presentation — 489; все прошли. Форматирование Core, Data и Data.Tests прошло; `bash scripts/check.sh` прошёл. Миграционный тест подтвердил, что прежняя генерация после обновления читается как Image с `DurationSeconds = null`.

Небольшое уточнение к плану: `ErrorKind.ResponseTooLarge` в текущем контракте отсутствует. Как и существующие клиенты для слишком больших ответов, запись потока выбрасывает `ProviderException(ErrorKind.Unknown)` с `ProviderResponseTooLargeException` во внутреннем исключении. Новые ErrorKind и UI-переводы не добавлялись.

## Комментарии

### 2026-11-06, claude-audit
Создано по поручению владельца («распланируй звук и видео»; выбрано: всё через OpenRouter, LibVLC в Avalonia сразу). ADR 0027, исследование — `docs/knowledge/media-generation.md`. План для Luna, ждёт одобрения.

### 2026-11-06, владелец (в чате, записал claude-audit)
План одобрен: «заапрувь все таски которые будут».
