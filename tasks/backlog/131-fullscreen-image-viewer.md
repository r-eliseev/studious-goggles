---
id: 131
status: approved
assignee:
claimed:
created: 2026-10-29
---

# Добавить modal image viewer и перелистывание

## Описание
Решение владельца 2026-10-29: click/tap по готовой картинке открывает viewer. Эта задача создаёт modal viewer на всю клиентскую область, navigation/accessibility/loading. Настоящий OS fullscreen/immersive state добавляет зависимая task137. F6.2 перелистывание внутри run здесь; zoom/pan/compare остаются 023.

Зависит от: 088, 097, 098, 133.
Среда: Linux или Windows; ручная проверка Android/desktop.

## Критерии готовности
- Click/tap successful image opens modal viewer filling shell client area on MAUI/Avalonia; OS immersive/fullscreen state is not changed. Placeholder/outer card keeps selection/actions.
- Initial image fit без crop; previous/next только successful images того же run, текущий порядок, no wrap.
- Close button, Escape desktop, Android/iOS system back; background click closes, image click does not.
- Desktop Left/Right; mobile visible ≥48×48 previous/next. Zoom/pan/swipe не обязательны.
- Missing/corrupt initial file → localized alert/no crash; invalid neighbor не заменяет текущую valid image.
- File existence/read/validation and platform decode run asynchronously outside UI dispatcher; only ready source assignment/modal operations use UI thread. Cancellation/stale navigation cannot replace current image.
- Focus Close on open, trap modal, localized automation names/position; focus returns trigger.
- Routes не добавляются; shell-specific modal service.

## Анализ
Shared `IImageViewerService` лучше INavigationService/IDialogService: MAUI PushModalAsync, Avalonia undecorated owned modal matching owner client bounds. Оба закрывают app chrome, но не меняют OS fullscreen/system bars. ChatPageModel знает run/cards; GenerationItemViewModel constructor не расширять. Task133 раньше для accessible dimensions.

Task023 после выделения оставляет F6.3 compare и зависит от OS fullscreen task137. Task131 intentionally does not claim full owner requirement until137.

## План
Для Haiku/Luna: no zoom/pan/compare. TDD shared command first, shell UI second.

1. `IImageViewerService` и `ImageViewerItem(Id, absolutePath, accessibleLabel)`; `ShowAsync(items, initialIndex, token)`.
2. ChatPageModel inject service, `OpenImageCommand(GenerationItemViewModel)`: find containing RunItem, filter HasImage, index clicked, SelectedCard=clicked, show. Non-success/not found no-op.
3. Presentation tests same run/order/index; other run excluded; no-op status; SelectedCard.
4. MAUI `ImageViewerPage.xaml(.cs)` black modal Grid, Image AspectFit, overlay controls, one idempotent close path. `MauiImageViewerService.LoadAsync` reads bytes and validates/decode-prepares off UI thread, checks cancellation/current index, then PushModal/assigns ready source on UI. FromFile с синхронным file read в dispatcher запрещён; если platform API не позволяет background decode, `blocked` с measurement вместо UI-blocking fallback.
5. MAUI card: accessible transparent Button/trigger only over visible Image, command via page BindingContext; outer SelectCard and long/context gestures remain.
6. Avalonia `ImageViewerWindow.axaml(.cs)` undecorated owned modal sized to owner client area, Image Uniform, ShowDialog owner; KeyDown Escape/Left/Right; root-only background close. `AvaloniaImageViewerService.LoadAsync` constructs Bitmap off UI thread, stale/cancellation guard, UI only assigns ready Bitmap. Не ставить WindowState=FullScreen.
7. Avalonia card successful image wrapped in focusable Button with command; placeholder no trigger.
8. Resources: Open/Close/Previous/Next/position/missing-unreadable. Label includes model, seed, dimensions from133, N/count.
9. Tests Avalonia viewer navigation/keys/background-vs-image/focus/missing and delayed load then next/close stale result ignored; composition registration. Presentation service fake proves file load/decode is not invoked inside main-thread dispatcher callback. MAUI structural markup AspectFit/bindings/semantics; manual system back/Escape/focus.
10. Edit task023 scope: remove F6.2/viewer, retain compare F6.3/metadata criterion, dependency points to 137 for completed fullscreen requirement.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
bash scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Presentation/Services/IImageViewerService.cs` (новый), `ImageGen/ImageGen.Presentation/Services/ImageViewerItem.cs` (новый), `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ChatPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/Design/MauiImageViewerMarkupTests.cs` (новый), `ImageGen/ImageGen.Maui/Pages/ImageViewerPage.xaml` (новый), `ImageGen/ImageGen.Maui/Pages/ImageViewerPage.xaml.cs` (новый), `ImageGen/ImageGen.Maui/Services/MauiImageViewerService.cs` (новый), `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Maui/MauiProgram.cs`, `ImageGen/ImageGen.Avalonia/Views/ImageViewerWindow.axaml` (новый), `ImageGen/ImageGen.Avalonia/Views/ImageViewerWindow.axaml.cs` (новый), `ImageGen/ImageGen.Avalonia/Services/AvaloniaImageViewerService.cs` (новый), `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml`, `ImageGen/ImageGen.Avalonia/Composition/AppServices.cs`, `ImageGen/ImageGen.Avalonia.Tests/Views/ImageViewerWindowTests.cs` (новый), `ImageGen/ImageGen.Avalonia.Tests/Views/CardActionsTests.cs`, `ImageGen/ImageGen.Avalonia.Tests/Composition/AppServicesTests.cs`, `tasks/backlog/023-compare.md`

## Отчёт

## Комментарии
