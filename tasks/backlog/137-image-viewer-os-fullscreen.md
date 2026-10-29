---
id: 137
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Переводить image viewer в настоящий OS fullscreen

## Описание
Решение владельца 2026-10-29: click по картинке разворачивает её на весь экран. Task131 создаёт modal viewer/navigation в клиентской области; эта задача добавляет platform-specific fullscreen/immersive state и гарантированное восстановление предыдущего окна/system UI.

Зависит от: 131.
Среда: Windows, Android device и Linux desktop; iOS/macOS manual parts допускаются как непроверенные по правилам repo.

## Критерии готовности
- Avalonia desktop viewer uses real `WindowState.FullScreen` and restores prior state on every exit/error.
- MAUI Windows uses WinUI AppWindow fullscreen presenter; restores prior presenter/bounds.
- MAUI Android hides status/navigation bars in immersive mode while viewer open; restores prior bars/behavior on close, Back, activity pause/recreate and load failure.
- MAUI iOS/Mac Catalyst hides supported status/title UI through platform controller and restores it; unavailable manual device recorded, not silently replaced by client-only view.
- Repeated enter/exit idempotent; exception/cancellation cannot strand app fullscreen.
- Escape/system Back/Close all use one `try/finally` exit path; accessibility focus returns trigger after restoration.

## Анализ
OS fullscreen APIs differ and do not belong in Presentation. Viewer 131 remains functional without this task but does not satisfy «весь экран». Platform controllers live in shell/platform folders and are called by shell viewer services. No global app state mutation without captured previous state.

## План
Для Haiku/Luna: implement one platform/controller at a time, tests state machine before native API. If API differs on target SDK, capture compiler error and blocked; no reflection/hidden private APIs.

1. Add `ImageGen.Presentation.Services.IFullScreenController` EnterAsync/ExitAsync with idempotent captured-state session. Each shell registers one platform implementation; viewer service wraps show/close in try/finally.
2. Avalonia controller captures owner/viewer WindowState/SystemDecorations, applies FullScreen, restores on Close/Escape/background/load error. Headless tests enter/exit/repeated/error.
3. MAUI Windows controller in Platforms/Windows captures `AppWindow.Presenter.Kind` and bounds; use supported FullScreen presenter; restore on all paths. Windows build + manual.
4. Android controller in Platforms/Android uses supported WindowInsets controller APIs to hide system bars/immersive behavior, captures/restores visibility/behavior; lifecycle reconnect/recreate cleanup. No deprecated flags unless target API requires documented fallback.
5. iOS/Mac Catalyst controller via supported view-controller/window APIs; compile on macOS CI when available; no private API. Record untested device behavior.
6. Viewer pages/services call controller only on platform main thread; image read/decode remains background per131.
7. Manual matrix: open/close five times; Escape/Back; rotate/background Android; missing image failure; switch monitors/window state desktop; confirm exact prior UI restored.
8. Update F6.2/spec with true OS fullscreen and platform verification notes.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -c Release -f net10.0-android
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
bash scripts/check.sh
```
Windows MAUI build/manual check runs in CI/Windows environment; iOS/Mac Catalyst compile/manual availability recorded in report.

Файлы: `ImageGen/ImageGen.Presentation/Services/IFullScreenController.cs` (новый), `ImageGen/ImageGen.Avalonia/Services/AvaloniaFullScreenController.cs` (новый), `ImageGen/ImageGen.Avalonia/Services/AvaloniaImageViewerService.cs`, `ImageGen/ImageGen.Avalonia/Views/ImageViewerWindow.axaml.cs`, `ImageGen/ImageGen.Avalonia/Composition/AppServices.cs`, `ImageGen/ImageGen.Avalonia.Tests/Services/AvaloniaFullScreenControllerTests.cs` (новый), `ImageGen/ImageGen.Maui/Services/MauiImageViewerService.cs`, `ImageGen/ImageGen.Maui/Platforms/Windows/WindowsFullScreenController.cs` (новый), `ImageGen/ImageGen.Maui/Platforms/Android/AndroidFullScreenController.cs` (новый), `ImageGen/ImageGen.Maui/Platforms/iOS/IosFullScreenController.cs` (новый), `ImageGen/ImageGen.Maui/Platforms/MacCatalyst/MacCatalystFullScreenController.cs` (новый), `ImageGen/ImageGen.Maui/MauiProgram.cs`, `docs/specs/image-client.md`

## Отчёт

## Комментарии
