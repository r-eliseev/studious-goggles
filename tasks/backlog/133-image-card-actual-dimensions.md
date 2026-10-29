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

Пересечения: 096 меняет GenerationItemViewModel, поэтому dependency; 131 зависит от этой задачи. XAML также пересекается 088/097/098, при rebase сохранять независимые blocks.

## План
Для Haiku/Luna: no Generation fields/migration/provider changes; no full image decode.

1. Создать следующий свободный ADR `NNNN-image-metadata-reader.md`: MetadataExtractor 2.9.3, Apache-2.0, Data-only dependency, alternatives manual parser/ImageSharp/UI decoders. Approval этой task принимает choice.
2. В обе shell Assets/Licenses добавить `MetadataExtractor-LICENSE.txt` и `XmpCore-LICENSE.txt`; central package version 2.9.3 и Data csproj reference. В MAUI csproj добавить `MauiAsset Include="Resources/Licenses/**"` с logical name, иначе текущий glob пакует только Resources/Raw.
3. Core `ImageDimensions` readonly record struct positive Width/Height; `IImageFileStore.TryReadDimensionsAsync(relativePath, cancellationToken)` nullable, non-throwing for content/file errors.
4. FileImageStore async method выполняет MetadataExtractor sync header read через bounded background work, не на caller UI context; maps JPEG/PNG/WebP/GIF without exposing package types. Expected IO/metadata exceptions safe Debug only; return null.
5. Data tests generated/minimal valid landscape fixtures for four formats; missing/random/truncated/unknown `.bin`; no file modification. Run `dotnet list ... --vulnerable --include-transitive`; advisory → blocked/version review.
6. GenerationItemViewModel observable metadata + method `ApplyImageDimensions(ImageDimensions?)`; `Update` clears metadata when status/path changes, but never reads file. Euclidean GCD, resource format invariant, include SummaryText.
7. ChatPageModel ReloadAsync starts dimension reads for successful cards outside UI dispatcher (bounded small parallelism); live update reads before BeginInvoke. Before apply compare generation id + captured path + current Succeeded/HasImage; stale result ignored. Same successful path may reuse current metadata, changed path rereads.
8. Tests: ViewModel formatting/clear/culture/SummaryText; ChatPageModel delayed read then path/status change ignores stale, same path reuse, file reader not called inside main-thread callback.
9. Add standalone meta TextBlock/Label in both card templates. New Avalonia `GenerationCardMetadataTests`; new MAUI `MauiGenerationCardMarkupTests` checks binding once.
10. Update docs architecture storage contract and card metadata spec; no migration.

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
