---
id: 137
status: done
assignee:
claimed:
created: 2026-10-29
---

# OS fullscreen viewer: Avalonia и MAUI Android

## Описание
После modal viewer131 добавить настоящий fullscreen: Avalonia WindowState.FullScreen и Android immersive bars. MAUI Windows вынесен в139, поскольку требует Windows. MAUI iOS/Mac Catalyst сейчас не проверяются сборкой в CI: нативный код этих платформ в эту задачу не входит; поддержка остаётся открытым вопросом, клиентский viewer131 продолжает работать.

Зависит от: 131.
Среда: Linux или Windows; Android SDK для сборки, устройство/desktop нужны только для ручных сценариев.

## Критерии готовности
- Avalonia разворачивает именно ImageViewerWindow, не owner; прежний owner не меняет состояние/геометрию.
- Android меняет bars только у MAUI Window/Activity, в которой открыт viewer; захватывает исходное состояние, восстанавливает на Close/Back/load error/pause и уничтожении handler.
- Повторный выход безопасен, exception/cancellation не оставляет app fullscreen, нет второго активного viewer.
- Неподдерживаемые MAUI платформы используют no-op controller и сохраняют modal131; Windows полноценная реализация139, iOS/Mac Catalyst не выдаются за выполненные.
- Фокус возвращается после восстановления chrome; file read/decode остаётся по правилам131.

## Анализ
Native-типы не нужны в Presentation: интерфейс контроллера локален MAUI, Avalonia использует собственный тип. Общий IImageViewerService131 не меняется. Нельзя выбирать первое окно Application.Current.Windows: viewer должен передать своё окно после подключения handler. Android min API21 требует fallback для <30; framework AndroidX WindowInsetsControllerCompat позволяет единый публичный API. Проверить доступность у текущих зависимостей, не добавлять непроверенные raw flags. Общие AppResources/ChatPageModel здесь не меняются.

## План
Для Haiku/Luna: один controller за шаг, без reflection/private API. Сначала прочитать реализации131, актуальные ссылки131 важнее старых гипотез ревью. Рутинные ошибки сборки исправлять в границах плана; не блокировать задачу только из-за несовпавшей сигнатуры.
1. В Maui.Services добавить IMauiFullScreenController с Enter(Microsoft.Maui.Controls.Window window) → IDisposable: вход и Dispose только на main thread, lease хранит captured native state, Dispose идемпотентен. NoOpFullScreenController для всех MAUI платформ кроме Android (Windows заменяет139). Singleton controller не хранит одно глобальное прежнее окно; state принадлежит lease. Повторный Enter для того же открытого viewer не перезахватывает state.
2. MauiImageViewerService после создания/подключения viewer page передаёт её Window в Enter, хранит lease на одно открытие. Кнопка Close/Back и ошибка ведут через единый close/finally, где Dispose вызывается ДО возврата фокуса. Если Enter или показ бросил исключение, уже захваченный state всё равно освобождается; failed Enter сам откатывает частично применённые изменения.
3. AndroidFullScreenController в Platforms/Android: получить Activity/Window из переданного MAUI Window handler, а не static CurrentActivity. Использовать WindowCompat.GetInsetsController/WindowInsetsControllerCompat; сохранить видимость status/navigation отдельно и SystemBarsBehavior, затем Hide(systemBars) с временным показом свайпом. Если root insets ещё не доставлены или decor ещё не подключён, pending lease ждёт их без блокировки UI и отменяется при Dispose; исходные состояния фиксируются до Hide. Ожидание сделано отменяемой проверкой через DecorView.Post/PostDelayed, чтобы не подменять listener обработки insets MAUI. Если Activity/handler/insets недоступны, lease остаётся no-op, а исходные visibility=false не выдумываются. Для API21–29 используется compatibility слой AndroidX.
4. Lifecycle coordinator принадлежит MauiImageViewerService на одно открытие. После показа modal page подписаться на Window.Stopped/Resumed/Destroying и HandlerChanging/HandlerChanged; Stopped/HandlerChanging освобождают только active lease, Resumed/HandlerChanged захватывают состояние текущего handler, если viewer ещё открыт. Destroying/закрытие снимает подписки и lease. При recreation состояние старого Activity не применяется к новому. Не менять manifest/configChanges.
5. AvaloniaFullScreenController.Enter(ImageViewerWindow window) → IDisposable захватывает её WindowState/SystemDecorations и ставит FullScreen при открытии. Owner остаётся как был. Обёрнуть полный ShowDialog lifecycle в finally; Close/Escape/error завершают один lease. На закрытом окне очищать managed state без попытки восстановить уничтоженный native handle.
6. Headless Avalonia тесты: выбран viewer, owner unchanged; повторное Dispose; ошибка показа/загрузки; Escape/Close. Native Android тесты не имитировать строками исходника: сборка плюс manual140 для bars/pause/recreate/Back/rotation. В Windows CI controller остаётся no-op до139.
7. Проверить Presentation.Tests и Avalonia.Tests; Android build; dotnet format затронутых проектов; bash scripts/check.sh. Ручные пункты desktop→046, Android→140. В F6.2 указать modal131, настоящий fullscreen Avalonia/Android137, Windows139 и неподтверждённые iOS/MacCatalyst.
8. Заполнить отчёт, отметить реально непроверенные платформы, release-task.sh137, проверки и PR по docs/tasks.md. Недоступная ручная среда не оставляет готовый код в in-progress.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android
bash scripts/check.sh
```
Дополнительно dotnet format с --verify-no-changes для каждого изменённого проекта. Windows TFM собирает CI; WinUI код здесь не добавляется.

Файлы: `ImageGen/ImageGen.Maui/Services/IMauiFullScreenController.cs` (новый), `ImageGen/ImageGen.Maui/Services/NoOpFullScreenController.cs` (новый), `ImageGen/ImageGen.Maui/Services/MauiViewerFullscreenSession.cs` (новый), `ImageGen/ImageGen.Maui/Platforms/Android/AndroidFullScreenController.cs` (новый), `ImageGen/ImageGen.Maui/Services/MauiImageViewerService.cs`, `ImageGen/ImageGen.Maui/MauiProgram.cs`, `ImageGen/ImageGen.Avalonia/Services/AvaloniaFullScreenController.cs` (новый), `ImageGen/ImageGen.Avalonia/Services/AvaloniaImageViewerService.cs`, `ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj`, `ImageGen/ImageGen.Avalonia.Tests/Services/AvaloniaFullScreenControllerTests.cs` (новый), `ImageGen/ImageGen.Avalonia.Tests/Services/ImageViewerWindowTests.cs` (новый), `docs/knowledge/maui.md`, `docs/specs/image-client.md`, `tasks/backlog/046-desktop-manual-check.md`, `tasks/backlog/140-android-manual-check.md`.

## Отчёт

Android fullscreen controller получает Activity из `page.Window.Handler` (`IWindowHandler.PlatformView`), затем работает с её Window/DecorView через AndroidX compat. Lease запоминает видимость status/navigation bars раздельно и behavior контроллера; ждёт подключения DecorView/первых insets без блокирования UI, скрывает панели только после snapshot и отменяет отложенный вход при закрытии. Одно lifecycle session на viewer освобождает lease при Stopped/HandlerChanging и получает новый для текущего handler при Resumed/HandlerChanged; Destroying/закрытие снимает подписки. MAUI Windows/iOS/MacCatalyst сохраняют no-op.

Avalonia controller переводит только ImageViewerWindow в WindowState.FullScreen, хранит исходные state/decorations и восстанавливает их при выходе до закрытия окна; закрытое окно не трогается, owner не меняется. Ошибка показа/отмены проходит через finally и освобождает lease; ошибка начальной загрузки показывает локализованный alert и закрывает viewer.

Проверено: Avalonia.Tests — 146 passed, 2 skipped (DPAPI/Secret Service недоступны); MAUI Android build прошёл без warnings/errors. Добавлены headless проверки состояния viewer/owner, Escape и localized missing-file alert. Ручные сценарии Android bars/pause/rotation переданы в140, Avalonia fullscreen — в046; физического Android устройства и desktop display в этой среде нет. iOS/MacCatalyst/MAUI Windows не реализуются этой задачей.

## Комментарии

### 2026-10-29, codex-review
Windows часть одобренного объёма выделена в139. iOS/MacCatalyst native fullscreen не разрешается выдавать за выполненный без компиляции; вопрос о среде вынесен в docs/status.md.
