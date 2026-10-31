---
id: 141
status: in-progress
assignee:
claimed:
created: 2026-10-29
---

# Атомарно сохранять картинки и вложения

## Описание
FileImageStore.SaveAsync и SaveAttachmentAsync пишут сразу в конечный путь через File.WriteAllBytesAsync. При исключении/отмене после начала записи может остаться усечённый файл; вызывающий сервис не получил relative path и не может выполнить компенсацию122. Сохранять во временный соседний файл и публиковать конечный путь атомарной заменой после полной успешной записи.

Зависит от: —
Среда: Linux или Windows.

Новая находка ревью PR42, статус planned: план требует отдельного одобрения; это не перенесённый одобренный объём122.

## Критерии готовности
- До commit final path не появляется; при ошибке записи/отмене старый существующий final file сохраняется, temporary file удаляется best-effort.
- SaveAsync/SaveAttachmentAsync сохраняют прежний relative path/media mapping и возвращают его только после commit; наружу не выдаётся имя temporary file.
- Отмена, наблюдённая последней проверкой token перед commit, не публикует final file. Отмена после этой проверки/во время успешного move допускает commit и возврат успеха; после commit поздней отменой результат не подменяется.
- Ошибка cleanup не подменяет исходную write/move exception; не удаляются session folder, чужие изображения или ранее подтверждённый final file.
- Тесты обеих операций проверяют success, partial write failure, cancellation, замещение существующего файла и cleanup failure без гонок/реального disk-full.

## Анализ
Подтверждено чтением FileImageStore: обе операции напрямую пишут final path;122 получает путь только после успешного возврата SaveAsync и не может охватить этот сбой. Задачи независимы:122 правит Core GenerationService,141 — Data storage implementation.133 также правит FileImageStore/его tests (чтение metadata); при занятом файле ждать завершения активной ветки по docs/tasks.md, функциональной зависимости нет.

Нужен temporary file в том же каталоге/файловой системе, иначе rename не обеспечивает требуемый commit. Это атомарная публикация при managed failure, не обещание сохранности после отключения питания/FS corruption; outbox/reconciliation и fsync DB здесь не вводить. Внешние сигнатуры IImageFileStore не менять.

## План
Для Haiku/Luna: сначала падающие тесты Data, затем один общий writer для двух методов, никаких новых NuGet/миграций.
1. Прочитать FileImageStore, FileImageStoreTests, IImageFileStore, AttachmentService и границы122. Сохранить алгоритм имени final path и GetExtension.
2. Ввести internal AtomicImageFileWriter с WriteAsync(finalPath, content, token). Временное имя — finalPath + случайный Guid + .tmp в том же каталоге. Helper сам открывает FileStream с CreateNew и только после успешного создания ставит ownedTemp=true; write seam не открывает/закрывает путь. Проверить token до записи; полностью записать/закрыть stream; проверить token непосредственно перед commit; File.Move(temp, final, overwrite:true) — единственная точка публикации. Последний token-check — граница принятия отмены: cancellation между этой проверкой и успешным move не откатывает commit. После commit cancellation не проверять, возвращать успех. Ошибка создания/записи/close/move пробрасывается без подмены.
3. Для детерминированного отказа internal writer допускает передаваемую в конструктор функцию записи уже открытого Stream; production вызывает stream.WriteAsync(content, token), тестовая функция пишет несколько байт и бросает либо отменяет token. Не использовать static mutable hook и не менять публичный FileImageStore constructor ради теста: общий writer можно тестировать отдельно, а public store success интеграционно на настоящей временной папке. Аргументы функции — открытый Stream/content/token; helper гарантирует await DisposeAsync stream до move или cleanup на любом исходе. Никакого повторного открытия по path.
4. Finally удаляет только temp path при ownedTemp=true, если он ещё существует. CreateNew failure не даёт права удалять файл по этому имени; добавить тест отказа создания с сохранением чужого файла. Для воспроизведения коллизии internal helper принимает фабрику temp-имени, production использует случайный Guid, test задаёт существующий путь. Ожидаемые cleanup IO failures пишутся через тот же безопасный logger helper из шага5, исходная ошибка сохраняется; если запись уже committed, не удалять final. Не маскировать ошибку успешным возвратом до commit.
5. FileImageStore оба Save-метода используют один writer; лог Image saved после commit не меняет семантику результата. Тест с logger, бросающим после commit, не должен заставлять caller считать файл незаписанным: ограничить диагностическую запись безопасным helper/catch именно логгера, не окружающим файловый commit.
6. Tests AtomicImageFileWriterTests: new success (final bytes correct, temp отсутствует); partial failure без final; existing final + partial failure сохраняет старые bytes; pre-cancel; token отменён writer перед move; successful overwrite; failure cleanup сохраняет исходную exception: internal writer constructor получает также delete-функцию с production default File.Delete, тестовая delete-функция бросает IOException. Этот seam относится только к helper и не расширяет IImageFileStore. Не имитировать disk-full заполнением диска. FileImageStoreTests проверяют оба public Save пути/имена/bytes и regression всех существующих delete/read сценариев.
7. Проверки: Data.Tests и Core.Tests; dotnet format Data/Data.Tests с --verify-no-changes; bash scripts/check.sh. В отчёте назвать гарантию только атомарной публикацией, отделить от compensation122. После одобрения и исполнения: отчёт/release-task141/PR по docs/tasks.md.

Файлы: `ImageGen/ImageGen.Data/AtomicImageFileWriter.cs` (новый), `ImageGen/ImageGen.Data/FileImageStore.cs`, `ImageGen/ImageGen.Data.Tests/AtomicImageFileWriterTests.cs` (новый), `ImageGen/ImageGen.Data.Tests/FileImageStoreTests.cs`.

## Отчёт

`FileImageStore.SaveAsync` и `SaveAttachmentAsync` используют общий `AtomicImageFileWriter`: временный файл создаётся через `CreateNew` рядом с итоговым, полностью записывается и закрывается, затем последняя проверка отмены предшествует единственному commit через `File.Move(..., overwrite: true)`. До commit итоговый файл не меняется; cleanup владеет только успешно созданным temporary path и не подменяет исходную ошибку. Отмена после последней проверки не откатывает успешный move. Debug-лог после commit не меняет результат сохранения.

Добавлены проверки writer: успех и cleanup, частичная запись при существующем final, пред-отмена, отмена после записи перед commit, collision `CreateNew`, ошибка cleanup при сохранении исходной ошибки и overwrite; public store проверяет замену изображений и вложений без временных файлов. Команды: `dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj` (105/105), Core.Tests (696/696), `dotnet format` для Data и Data.Tests (`--verify-no-changes --no-restore`), `scripts/check.sh` — все прошли. Гарантия ограничена атомарной публикацией и обработанными ошибками процесса/записи; это не гарантия от потери питания или повреждения файловой системы и не замена компенсации 122.

## Комментарии

### 2026-10-29, codex-review
Новая задача создана при проверке границ122. В этом PR только план; без одобрения код не писать. Поведение сбоя подтверждено структурой текущей записи, реальное заполнение диска/аварийное отключение не испытывались.

### 2026-10-31, владелец (в чате, записал claude-audit)
План одобрен.
