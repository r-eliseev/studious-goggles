---
id: 139
status: in-progress
assignee: claude-haiku
claimed: 2026-11-01T19:05:47Z
created: 2026-10-29
---

# MAUI Windows: настоящий fullscreen окна viewer

## Описание
Windows-часть исходной137: использовать WinUI AppWindow fullscreen и восстановить предыдущее окно. Общая интеграция viewer/controller и Android/Avalonia реализуются137.

Зависит от: 137.
Среда: только Windows.

## Критерии готовности
- Разворачивается именно MAUI Window, в которой открыт viewer131, а не первое глобальное окно.
- Close/Escape/load error возвращают прежний presenter и положение/размер/состояние окна; многократное открытие не накапливает state.
- Обычный просмотр131 на остальных платформах работает.

## Анализ
Нативный WinUI код нельзя включать в Linux-задачу с единственной проверкой Android. Отдельная139 сохраняет одобренный объём137. В137 не-Windows платформенный controller зарегистрирован отдельно;139 заменяет Windows no-op, не меняет контракт.

## План
Для Haiku/Luna: сначала прочитать реализацию137 и MAUI viewer131, затем Windows SDK API. Никаких reflection/private API.
1. Реализовать WindowsFullScreenController с контрактом IMauiFullScreenController.Enter(Microsoft.Maui.Controls.Window window) → IDisposable. Обращения к нативному окну только на main thread. Получить WinUI Window из переданного window.Handler.PlatformView, затем AppWindow через публичный MAUI/WinUI interop. Не выбирать Application.Current.Windows[0].
2. До SetPresenter(FullScreen) сохранить конкретный AppWindow, прежний AppWindowPresenter, Position/Size и состояние OverlappedPresenter (normal/maximized/minimized). Не менять сохранённый state при повторном Enter; вложенный viewer уже запрещён131/137.
3. Lease.Dispose идемпотентен: вернуть прежний presenter; для normal вернуть bounds, для maximized/minimized восстановить это состояние через публичный presenter API. Если native window уже закрыто — очистить state без обращения к уничтоженному handle. Путь finally137 вызывает Dispose также при ошибке показа/загрузки.
4. В MauiProgram заменить только регистрацию Windows no-op на WindowsFullScreenController через условную компиляцию. Android controller/остальные no-op оставить.
5. На Windows проверить normal и maximized окно, несколько мониторов, пять открытий/закрытий, Escape/кнопку Close и ошибку картинки; отдельно закрытие основного окна во время viewer. В отчёт фактические результаты. Если конкретный случай не проверен — указать, не выдумывать тест.
6. Собрать Windows и Android TFM, Presentation.Tests, dotnet format MAUI-проекта, bash scripts/check.sh. Дописать ручные пункты016 и уточнить F6.2, какие платформы теперь имеют настоящий fullscreen.
7. Заполнить «Отчёт» результатами проверок; снять занятость через `scripts/release-task.sh <номер>` и выполнить `bash scripts/check.sh`. Ветку задачи обновить от origin/main и открыть PR по docs/tasks.md. Ручные результаты не выдумывать.

Файлы: `ImageGen/ImageGen.Maui/Platforms/Windows/WindowsFullScreenController.cs` (новый), `ImageGen/ImageGen.Maui/MauiProgram.cs`, `docs/specs/image-client.md`, `docs/knowledge/maui.md`, `tasks/v0.1/016-skeleton-manual-check.md`.

## Отчёт

- Изменено: `ImageGen/ImageGen.Maui/Platforms/Windows/WindowsFullScreenController.cs` (новый), `ImageGen/ImageGen.Maui/MauiProgram.cs` (ветка `#elif WINDOWS`; Android и остальные платформы без изменений).
- Контроллер берёт `AppWindow` из `window.Handler.PlatformView` того окна, где открыт viewer. Перед `FullScreen` сохраняются presenter, положение, размер и состояние (normal/maximized/minimized). `Dispose` восстанавливает их; при уничтоженном окне `COMException` подавляется.
- Проверено: сборка `net10.0-windows10.0.19041.0` без ошибок; `dotnet format --verify-no-changes` по ImageGen.Maui чистый.
- Не проверено вручную: fullscreen в запущенном приложении, восстановление normal/maximized, несколько мониторов, пять открытий/закрытий, закрытие основного окна во время viewer.

## Комментарии

### 2026-10-29, codex-review
Выделено при ревью PR #42 по поручению владельца. Это план работы, не отчёт об исполнении. Одобрение исходной задачи сохраняется для перенесённого объёма; код в этом PR не реализуется.

### 2026-11-01, claude-audit — сверка Windows-задач с кодом
Зависимость 137 выполнена, задачу можно занимать на Windows. Контракт `IMauiFullScreenController.Enter(Window) → IDisposable` совпадает с кодом. Поправка к шагу 4: отдельной регистрации no-op для Windows нет, в `MauiProgram.cs` (строки 86–90) стоит `#if ANDROID … #else NoOpFullScreenController`, и в `#else` попадают ещё iOS и Mac Catalyst. Нужно добавить ветку `#elif WINDOWS` с `WindowsFullScreenController`, а `#else` с NoOp оставить. По шагу 5: если второго монитора нет, так и записать в отчёт.
