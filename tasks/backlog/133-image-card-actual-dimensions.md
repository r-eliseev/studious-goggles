---
id: 133
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Показывать фактические pixel dimensions и aspect ratio в карточке

## Описание
Решение владельца 2026-10-29: successful image card показывает фактические размеры сохранённого файла и сокращённое соотношение сторон, например `1200×800 px · 3:2`, в обеих оболочках. Requested aspect ratio/outcome остаётся отдельной информацией.

Зависит от: 096.
Среда: Linux или Windows.

## Критерии готовности
- Width/height читаются из сохранённых bytes/file header, не из requested parameters, extension или card viewport.
- Формат invariant: `1920×1080 px · 16:9`, portrait `9:16`, square `1:1`, coprime unchanged; GCD positive.
- Работает для новых и старых rows без DB migration/backfill.
- Metadata file I/O выполняется асинхронно вне UI thread; late result применяется только если generation id, captured ImagePath и текущий Succeeded/HasImage всё ещё совпадают.
- PNG/JPEG/WebP/GIF; missing/corrupt/unsupported → metadata hidden/no crash/status unchanged.
- Строка всегда видна во всех styles/оболочках и входит в SummaryText accessibility.
- Existing replaced/dropped parameter lines не меняются.

## Анализ
Производные данные не храним в DB: file source of truth, это покрывает старые rows и не создаёт migration. Синхронное file I/O из `GenerationItemViewModel.Update` запрещено: async read orchestrates ChatPageModel before marshaling result to UI; ViewModel только применяет готовые dimensions. Для robust header parsing в Data выбран `MetadataExtractor` 2.9.3, Apache-2.0: [NuGet versions](https://api.nuget.org/v3-flatcontainer/metadataextractor/index.json), [2.9.3 package metadata and dependencies](https://api.nuget.org/v3-flatcontainer/metadataextractor/2.9.3/metadataextractor.nuspec), [project/license](https://github.com/drewnoakes/metadata-extractor-dotnet). Runtime dependency XmpCore 6.1.10.1 имеет отдельный BSD notice. ImageSharp не выбран из-за Split License.

Пересечения: 096 меняет GenerationItemViewModel, поэтому dependency; 131 зависит от этой задачи. XAML также пересекается 088/097/098, при rebase сохранять независимые blocks. 122 использует IImageFileStore.DeleteFileAsync и дополняет FileImageStoreTests; методы метаданных независимы, новая зависимость не нужна. При параллельной работе 133 добавляет новый метод/тесты, а 122 сохраняет контракт удаления; при rebase оставить оба набора тестов. Прежде чем claim проверить чужие ветки по docs/tasks.md; при активной работе 122 с FileImageStoreTests подождать её завершения, затем обновиться и продолжить 133.

## План
Для Haiku/Luna: no Generation fields/migration/provider changes; no full image decode.

1. Создать следующий свободный ADR `NNNN-image-metadata-reader.md`: MetadataExtractor 2.9.3, Apache-2.0, Data-only dependency, alternatives manual parser/ImageSharp/UI decoders. Approval этой task принимает choice.
2. В обе shell Assets/Licenses добавить `MetadataExtractor-LICENSE.txt` и `XmpCore-LICENSE.txt`; central package version2.9.3 (либо минимальный исправленный стабильный release по шагу5 с теми же лицензиями) и Data csproj reference. В MAUI csproj добавить `MauiAsset Include="Resources/Licenses/**"` с logical name, иначе текущий glob пакует только Resources/Raw.
3. Core `ImageDimensions` readonly record struct с int Width/Height; конструктор отклоняет неположительные значения ArgumentOutOfRangeException. Контракт `Task<ImageDimensions?> IImageFileStore.TryReadDimensionsAsync(string relativePath, CancellationToken cancellationToken)`: missing/corrupt/unknown/content IO → null; OperationCanceledException при отмене распространяется, ошибки программиста не подавляются. Метод не меняет файл. Existing doubles на NSubstitute проверять на default Task/null; явно задать результат в helper metadata tests, интерфейс не снабжать фиктивной default реализацией.
4. FileImageStore async method выполняет MetadataExtractor sync header read через Task.Run с cancellation token; общий readonly SemaphoreSlim на FileImageStore ограничивает одновременно работающие parser jobs до 2 (WaitAsync перед Task.Run, Release в finally). Внутри проверять token до/после синхронного чтения; активный parser не обещает моментально прерываться. Caller UI context не выполняет file IO; чтение `ImageMetadataReader.ReadMetadata(fullPath)`; поддерживать четыре directory-типа JpegDirectory/PngDirectory/WebPDirectory/GifHeaderDirectory и их ImageWidth/ImageHeight tags, точные имена сверить с установленным 2.9.3. Положительные пиксельные размеры растра без EXIF orientation (width/height encoded header); типы пакета наружу Data не выдавать. Expected IO/metadata exceptions safe Debug only; return null.
5. Data tests: маленькие валидные встроенные bytes fixtures четырёх форматов (не требуют UI/сетевого скачивания в test), задокументировать происхождение/лицензию; missing/random/truncated/unknown `.bin`; no file modification. Run `dotnet list ... --vulnerable --include-transitive`. Advisory проверить по package id/affected range: посторонняя уже существовавшая зависимость не означает дефект выбранного reader. Если затронут MetadataExtractor/XmpCore, выбрать минимальную стабильную исправленную совместимую версию с той же лицензией, обновить central pin/transitive notice и ADR, затем повторить Data/Presentation/Avalonia tests и Android build. Подмена библиотеки или лицензии не допускается; patch/API-адаптация в этой библиотеке уже разрешена и не требует вопроса владельцу. Не ставить blocked автоматически по самому наличию advisory и не скрывать реальную уязвимость новой зависимости: если совместимой исправленной версии нет, описать конкретный технический дефект и продолжать подбор/проверку поддержанного стабильного release той же библиотеки, не заявляя успешную проверку заранее.
6. GenerationItemViewModel: observable `ImageDimensionsText` (string, пусто без metadata), `HasImageDimensions` (bool); `ApplyImageDimensions(ImageDimensions?)` обновляет оба и пересобирает SummaryText вместе с существующими status/details/error. Новый resource `ImageDimensionsFormat` со значением `{0}×{1} px · {2}:{3}` и wrapper AppResources.cs; числа/ratio форматируются InvariantCulture. `Update` clears metadata when status/path changes, but never reads file. Euclidean GCD, resource format invariant, include SummaryText.
7. ChatPageModel при создании карточек в ReloadAsync сначала публикует ленту без metadata, затем запускает загрузки успешных карточек; не ждать чтения всей ленты перед её показом. Использовать отдельный epoch ленты/session и CancellationTokenSource, смена сессии/Reload/OnDisappearing отменяет старые reads; задачи наблюдаются через FireAndForgetSafeAsync. Live update: сначала card.Update в UI, потом асинхронный metadata read, затем отдельный UI callback для результата; статус генерации не ждать вместе с disk read. Before apply compare captured epoch/session id + reference конкретного card в `_cards` + generation id + captured `generation.ImagePath` converted via GetFullPath equals current card.ImageFilePath + current card.IsDone && card.HasImage; поля ImagePath/Succeeded у card нет, относительный путь захватывается из Generation при create/update; stale result ignored. В пределах epoch кешировать завершённые (id,path) для предотвращения дублей; changed path rereads. Ошибку null можно считать кешированной до следующего Reload; иначе каждый update повторно читает corrupt file. При Update с тем же path/status metadata не стирать. Отмену не логировать как ошибку и не превращать в null.
8. Tests: ViewModel formatting/clear/culture/SummaryText; ChatPageModel delayed read then path/status change ignores stale, reload создаёт новый card с тем же id/path и старый результат его не меняет, same path reuse, cancellation, file reader not called inside main-thread callback.
9. Add standalone meta TextBlock/Label in both card templates. New Avalonia `GenerationCardMetadataTests`; new MAUI `MauiGenerationCardMarkupTests` checks binding once.
10. Update docs architecture storage contract and card metadata spec; no migration. Включить существующие status/details/error в SummaryText без потери текста при ApplyImageDimensions.
11. Заполнить «Отчёт», перечислить непроверенные ручные платформы и передать Android в 140, MAUI Windows в 016, Avalonia в 046, завершить по docs/tasks.md и `scripts/release-task.sh 133`.

### Проверка
```
dotnet list ImageGen/ImageGen.Data/ImageGen.Data.csproj package --vulnerable --include-transitive
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Core/ImageGen.Core.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data/ImageGen.Data.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `docs/decisions/NNNN-image-metadata-reader.md` (новый), `ImageGen/Directory.Packages.props`, `ImageGen/ImageGen.Data/ImageGen.Data.csproj`, `ImageGen/ImageGen.Maui/ImageGen.Maui.csproj`, `ImageGen/ImageGen.Core/Storage/ImageDimensions.cs` (новый), `ImageGen/ImageGen.Core/Storage/IImageFileStore.cs`, `ImageGen/ImageGen.Data/FileImageStore.cs`, `ImageGen/ImageGen.Data.Tests/FileImageStoreTests.cs`, `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`, `ImageGen/ImageGen.Presentation/PageModels/GenerationItemViewModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ChatPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/GenerationItemViewModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/Design/MauiGenerationCardMarkupTests.cs` (новый), `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml`, `ImageGen/ImageGen.Avalonia.Tests/Views/GenerationCardMetadataTests.cs` (новый), `ImageGen/ImageGen.Avalonia/Assets/Licenses/MetadataExtractor-LICENSE.txt` (новый), `ImageGen/ImageGen.Avalonia/Assets/Licenses/XmpCore-LICENSE.txt` (новый), `ImageGen/ImageGen.Maui/Resources/Licenses/MetadataExtractor-LICENSE.txt` (новый), `ImageGen/ImageGen.Maui/Resources/Licenses/XmpCore-LICENSE.txt` (новый), `docs/architecture.md`, `docs/specs/chat-sessions.md`

## Отчёт

## Комментарии

### 2026-10-29, codex-review
Задача нужна: фактические размеры — производное от файла свойство без миграции. Уточнены cancellation/error contract, предел фонового чтения, конкретные metadata properties, защита нового card от результата предыдущего Reload и независимость обновления статуса от чтения диска.
