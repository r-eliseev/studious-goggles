---
id: 139
status: approved
assignee:
claimed:
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

## Комментарии

### 2026-10-29, codex-review
Выделено при ревью PR #42 по поручению владельца. Это план работы, не отчёт об исполнении. Одобрение исходной задачи сохраняется для перенесённого объёма; код в этом PR не реализуется.
