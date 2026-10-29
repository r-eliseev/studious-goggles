---
id: 137
status: approved
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
3. AndroidFullScreenController в Platforms/Android: получить Activity/Window из переданного MAUI Window handler, а не static CurrentActivity без проверки. Использовать WindowCompat.GetInsetsController/WindowInsetsControllerCompat; захватить видимость statusBars/navigationBars и SystemBarsBehavior, затем Hide(systemBars), временный показ свайпом. Захватить исходную видимость каждого типа через текущие RootWindowInsets до Hide. Если initial insets ещё не доставлены после attachment — отложить Enter до первого получения insets с отменой при закрытии viewer; если захват невозможен, no-op без изменения bars и явно записанный непроверенный исход, а не выдуманные visibility=false. Учесть видимость каждого типа отдельно при восстановлении. Для API21–29 compat сохраняет/восстанавливает flags через публичный compatibility слой; проверять поддержку методов на установленной версии.
4. Lifecycle coordinator принадлежит MauiImageViewerService на одно открытие. Подписаться на Window.Stopped/Resumed/Destroying и HandlerChanging/HandlerChanged перед Enter; держать active lease отдельно от этих подписок. Stopped/HandlerChanging — Dispose и обнулить только active lease; resume-наблюдатели остаются до закрытия viewer. Resumed/HandlerChanged — если viewer всё ещё открыт и handler/insets готовы, Enter для текущего window; повторный event при active lease ничего не делает. Destroying/окончательное закрытие — Dispose и удалить все подписки. При recreation не применять state старого Activity к новому. OnDisappearing из-за вложенной операции не должно закрывать modal непреднамеренно. Не менять manifest/configChanges вслепую.
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

Файлы: `ImageGen/ImageGen.Maui/Services/IMauiFullScreenController.cs` (новый), `ImageGen/ImageGen.Maui/Services/NoOpFullScreenController.cs` (новый), `ImageGen/ImageGen.Maui/Platforms/Android/AndroidFullScreenController.cs` (новый), `ImageGen/ImageGen.Maui/Services/MauiImageViewerService.cs`, `ImageGen/ImageGen.Maui/Pages/ImageViewerPage.xaml.cs`, `ImageGen/ImageGen.Maui/MauiProgram.cs`, `ImageGen/ImageGen.Avalonia/Services/AvaloniaFullScreenController.cs` (новый), `ImageGen/ImageGen.Avalonia/Services/AvaloniaImageViewerService.cs`, `ImageGen/ImageGen.Avalonia/Views/ImageViewerWindow.axaml.cs`, `ImageGen/ImageGen.Avalonia.Tests/Services/AvaloniaFullScreenControllerTests.cs` (новый), `docs/specs/image-client.md`, `tasks/backlog/046-desktop-manual-check.md`, `tasks/backlog/140-android-manual-check.md`.

## Отчёт

## Комментарии

### 2026-10-29, codex-review
Windows часть одобренного объёма выделена в139. iOS/MacCatalyst native fullscreen не разрешается выдавать за выполненный без компиляции; вопрос о среде вынесен в docs/status.md.
