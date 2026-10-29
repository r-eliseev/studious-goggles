---
id: 080
status: in-progress
assignee: claude-536f
claimed: 2026-10-29T05:32:03Z
created: 2026-10-28
---

# Сохранение картинки: файл на десктопе, галерея на телефонах

## Описание
Решения владельца (2026-10-28): действие «Save image» у готовой карточки. Десктоп (Avalonia; MAUI Windows и Mac) — системный диалог «Сохранить как»; iOS — сразу в «Фото»; Android — сразу в галерею (MediaStore, папка `Pictures/ImageGen`). Задача поглощает 029 (сохранение в галерею Android, снята).

Справочник: `docs/design/implementation.md`; макеты: `docs/design/prototype.html` (https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решения владельца — задача 074 и «Комментарии» этой задачи.

Зависит от: 069 (меню действий), 076 (те же файлы чата).
Среда: Linux или Windows (Avalonia — headless-тесты на Linux; MAUI — сборка Android на Linux с Android SDK, Windows-цель собирает CI; ручные проверки — 016 и 046). Сохранение в галерею проверяется только на устройстве (эмулятора в облачной сессии нет, `docs/knowledge/claude-cloud-session.md`): ручная проверка — в 016 (Android и iOS при наличии устройства).

## Критерии готовности
- Пункт «Save image as…» (десктоп) / «Save to Photos» (iOS) / «Save to gallery» (Android) есть у готовых карточек; после сохранения — короткое подтверждение («Saved»), доступное диктору; отказ в разрешении — понятное сообщение.
- Имя файла по умолчанию: `imagegen-<первые 8 символов id генерации>.<расширение из типа файла>`.
- Android 10+ — без разрешений (MediaStore, `RelativePath`); Android 9 и ниже — разрешение `WRITE_EXTERNAL_STORAGE` (`maxSdkVersion="28"` в манифесте).
- iOS — `NSPhotoLibraryAddUsageDescription` в `Info.plist`.
- Тесты из «Плана» проходят; 029 помечена `cancelled` со ссылкой сюда.

## Анализ
- Файл картинки уже лежит в папке данных: `GenerationItemViewModel.ImageFilePath`, тип — `Generation.MediaType` (проверить имя поля; расширение по типу — есть ли готовая функция в `ImageMediaType` в Presentation).
- Новый интерфейс Presentation `IImageSaver` (`Task<ImageSaveResult> SaveAsync(string sourcePath, string suggestedFileName, string mediaType, CancellationToken)`; `ImageSaveResult` — `Saved`, `Cancelled`, `PermissionDenied`, `Failed`), подпись пункта — от оболочки (`IImageSaver.ActionLabel`).
- Avalonia: `TopLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName, FileTypeChoices })`, копирование потока.
- MAUI Windows и Mac: `CommunityToolkit.Maui.Storage.FileSaver.Default.SaveAsync(fileName, stream, cancellationToken)` (пакет `CommunityToolkit.Maui` 15.0.1 подключён).
- MAUI Android: `ContentValues` (`MediaStore.IMediaColumns.DisplayName`, `MimeType`, `RelativePath` = `Pictures/ImageGen`, `IsPending` 1 → 0), `ContentResolver.Insert(MediaStore.Images.Media.ExternalContentUri, values)`, запись потока; на API < 29 — `Permissions.RequestAsync<Permissions.StorageWrite>()` и файл в `Environment.GetExternalStoragePublicDirectory(DirectoryPictures)/ImageGen`, затем `MediaScannerConnection.ScanFile`.
- MAUI iOS: `PHPhotoLibrary.RequestAuthorizationAsync(PHAccessLevel.AddOnly)`, затем `PHPhotoLibrary.SharedPhotoLibrary.PerformChangesAsync(() => PHAssetChangeRequest.FromImage(NSUrl.FromFilename(path)))`.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, ключей и строк переноси дословно. Если шаг не получается так, как написано (API другое, компилятор не принимает разметку), не придумывай обход: опиши в «Комментариях», что пробовал, поставь `blocked` и остановись.

1. Presentation: `Services/IImageSaver.cs`, `Services/ImageSaveResult.cs`; `GenerationItemViewModel.SaveCommand` (доступна при `HasImage`; подтверждение через свойство `StatusAnnouncement`, которое view озвучивает, или через `IDialogService.AlertAsync` при ошибке); имя файла — функция `ImageFileNames.Suggested(Guid generationId, string mediaType)`. Строки: `Saved` = "Saved", `SavePermissionDenied` = "ImageGen has no permission to save images. Allow it in the system settings.", `SaveFailed` = "Could not save the image.". Тесты: `ImageFileNamesTests` (png, jpeg, webp, неизвестный тип → `.png`), `GenerationItemViewModelTests`: `Save_CallsSaverWithSuggestedName`, `Save_PermissionDenied_ShowsMessage`, `Save_NoImage_CannotExecute`.
2. Avalonia: `Services/AvaloniaImageSaver.cs` (`ActionLabel` = "Save image as…"), регистрация, пункт в меню карточки (значок `Save`). Тест `AvaloniaImageSaverTests.Save_CopiesFile` через подменный `IStorageProvider`, если его можно подменить; иначе тест на копирование потока (метод `CopyAsync(Stream, Stream)` выделить и проверить).
3. MAUI: `Services/MauiImageSaver.cs` с ветками `#if ANDROID`, `#if IOS`, `#if WINDOWS || MACCATALYST` (или `Platforms/<OS>/ImageSaver.cs` + partial — выбрать по образцу `Platforms/Windows/ChatEditorKeyHandling.cs`); `ActionLabel` по платформе; `AndroidManifest.xml` — `WRITE_EXTERNAL_STORAGE` с `maxSdkVersion="28"`; `Platforms/iOS/Info.plist` — `NSPhotoLibraryAddUsageDescription` = "ImageGen saves generated images to Photos.". Пункт в меню карточки.
4. `tasks/backlog/029-android-gallery-save.md`: статус `cancelled`, в «Комментарии» — «входит в 080 (решение владельца 2026-10-28)».
5. `tasks/v0.1/016-skeleton-manual-check.md` — пункты ручной проверки: сохранение на Windows (диалог), Android 10+ (галерея, без запроса разрешения), iOS (запрос доступа, «Фото»).
6. `docs/specs/chat-sessions.md` S4.8 «Save image».

### Проверка
```
dotnet test --project ImageGen/ImageGen.Core.Tests/ImageGen.Core.Tests.csproj
dotnet test --project ImageGen/ImageGen.Data.Tests/ImageGen.Data.Tests.csproj
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android -p:AndroidSdkDirectory=$ANDROID_HOME
dotnet format ImageGen/<каждый изменённый проект>/<проект>.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Presentation/{Services/IImageSaver.cs,Services/ImageSaveResult.cs,Services/ImageFileNames.cs,PageModels/GenerationItemViewModel.cs,PageModels/ChatPageModel.cs}`, строки, `ImageGen/ImageGen.Avalonia/{Services/AvaloniaImageSaver.cs,Composition/AppServices.cs,Views/ChatView.axaml}`, `ImageGen/ImageGen.Maui/{Services/MauiImageSaver.cs,Platforms/Android/AndroidManifest.xml,Platforms/iOS/Info.plist,MauiProgram.cs,Pages/ChatPage.xaml}`, тесты, `tasks/backlog/029-android-gallery-save.md`, `tasks/v0.1/016-skeleton-manual-check.md`, `docs/specs/chat-sessions.md`

## Отчёт

## Комментарии

### 2026-10-28, владелец (в чате, записал claude-cloud-t9wd)
Save image на телефонах — «сразу в галерею» (iOS «Фото», Android MediaStore); задача 029 входит сюда.
