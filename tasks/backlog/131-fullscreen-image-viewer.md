---
id: 131
status: blocked
assignee:
claimed:
created: 2026-10-29
---

# Добавить modal image viewer и перелистывание

## Описание
Решение владельца 2026-10-29: click/tap по готовой картинке открывает viewer. Эта задача создаёт modal viewer на всю клиентскую область, navigation/accessibility/loading. Настоящий OS fullscreen/immersive state добавляет зависимая task137. F6.2 перелистывание внутри run здесь; zoom/pan/compare остаются 023.

Зависит от: 097, 098, 133.
Среда: Linux или Windows; ручная проверка Android/desktop.

## Критерии готовности
- Click/tap successful image opens modal viewer filling shell client area on MAUI/Avalonia; OS immersive/fullscreen state is not changed. Placeholder/outer card keeps selection/actions.
- Initial image fit без crop; previous/next только successful images того же run, текущий порядок, no wrap.
- Close button, Escape desktop, Android system back (iOS — не проверено до доступной macOS-сборки/устройства); background click closes, image click does not.
- Desktop Left/Right; mobile visible ≥48×48 previous/next. Zoom/pan/swipe не обязательны.
- Missing/corrupt initial file → localized alert/no crash; invalid neighbor не заменяет текущую valid image.
- File existence/read и доступная проверка формата выполняются вне UI dispatcher; в Avalonia создание Bitmap выполняется в фоне. В MAUI байты читаются в фоне, ImageSource.FromStream получает новый MemoryStream при каждом вызове фабрики; внутреннее декодирование MAUI image handler управляется фреймворком. Синхронный ручной file read/decode в UI callback запрещён; только привязка готового source и modal operations идут через UI thread. Cancellation/stale navigation cannot replace current image.
- Focus Close on open, trap modal, localized automation names/position; focus returns trigger.
- Routes не добавляются; shell-specific modal service.

## Анализ
Shared `IImageViewerService` лучше INavigationService/IDialogService: MAUI PushModalAsync, Avalonia undecorated owned modal matching owner client bounds. Оба закрывают app chrome, но не меняют OS fullscreen/system bars. ChatPageModel знает run/cards; GenerationItemViewModel constructor не расширять. Task133 раньше для accessible dimensions.

023 оставляет F6.3 compare и может зависеть от этого viewer, а OS fullscreen остаётся отдельной 137; сам fullscreen не нужен для сравнения. 088 не является функциональной зависимостью: исправляет меню карточки Windows, viewer открывается отдельным image trigger. Общие ChatPageModel/XAML сначала меняют 097/098/133. После них обновить план по фактическому коду.

## План
Для Haiku/Luna: no zoom/pan/compare. TDD shared command first, shell UI second.

1. `IImageViewerService` и `ImageViewerItem(Id, absolutePath, accessibleLabel)`; `Task ShowAsync(IReadOnlyList<ImageViewerItem> items, int initialIndex, CancellationToken cancellationToken)`. Пустой список/индекс вне диапазона → ArgumentException до открытия. Snapshot списка фиксируется при открытии; обновления ленты не меняют порядок уже открытого viewer. В каждой оболочке допускается одно открытие; повторный тап при открытом viewer игнорируется. Cancellation закрывает viewer и завершается без alert; ожидаемые ошибки файла показываются локализованно внутри оболочки.
2. ChatPageModel inject service, `OpenImageCommand(GenerationItemViewModel)`: find containing `RunItemViewModel` в `Items` и его `Generations`, filter HasImage, index clicked, SelectedCard=clicked, show. Non-success/not found no-op.
3. Presentation tests same run/order/index; other run excluded; no-op status; SelectedCard.
4. MAUI `MauiImageViewerPage` строит black modal Grid с Image AspectFit и overlay controls. Сервис регистрируется в MauiProgram как singleton; открытие/закрытие идёт через текущую Shell Navigation.PushModalAsync/PopModalAsync. Все close/back/cancel пути завершают один TaskCompletionSource и не вызывают двойной PopModalAsync. Загрузка читает байты через File.ReadAllBytesAsync вне dispatcher и готовит ImageSource.FromStream с фабрикой нового MemoryStream над bytes; фабрика файл не читает. Epoch блокирует публикацию устаревших результатов. Отдельный синхронный декодер MAUI не добавляется.
5. MAUI card: accessible transparent Button/trigger only over visible Image, command via page BindingContext; outer SelectCard and long/context gestures remain.
6. Avalonia `ImageViewerWindow` — undecorated owned modal sized to owner client area, Image Uniform, ShowDialog owner; KeyDown Escape/Left/Right; background close. `AvaloniaImageViewerService` constructs Bitmap off UI thread, stale/cancellation guard, UI only assigns ready Bitmap. Не ставить WindowState=FullScreen.
7. Avalonia service получает `Func<Window?> owner` в конструкторе; registration создаётся внутри `AppServices.Create` с method group его private `MainWindowOwner`. Сам сервис не обращается к private-члену AppServices; при отсутствии owner не открывает orphan window, показывает/логирует безопасную ошибку. Закрытие освобождает Bitmap и отменяет pending load; при next старый Bitmap освобождается после успешной подмены. Avalonia card successful image wrapped in focusable Button with command; placeholder no trigger.
8. Resources: Open/Close/Previous/Next/position/missing-unreadable. Label includes model, seed, dimensions from133, N/count.
9. Presentation tests cover same-run order/index/exclusion/no-op/selection and accessible metadata. Avalonia composition registration and MAUI card markup bindings/semantics are tested; both projects build. Native modal navigation, file loading, keyboard/back, focus, and pointer behavior are handed to manual tasks 016/046/140.
10. 023 уже выделена в compare: не переписывать её повторно и не добавлять зависимость от 137. Обновить спецификацию F6.2: modal client-area viewer в этой задаче, OS fullscreen отдельно 137. Полный file list тестовых helper: найти все создания ChatPageModel (`rg "new ChatPageModel|ChatPageModel Create|ChatPageModel Build" ImageGen` плюс target-typed `new` в helper), передать fake нового IImageViewerService в Presentation/Avalonia tests. Затем выполнить полные suites обоих проектов.
11. Заполнить «Отчёт», передать непроверенные Android сценарии в 140, MAUI Windows в 016, Avalonia desktop в 046, выполнить завершение `docs/tasks.md` и `scripts/release-task.sh 131`.

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

Файлы: `ImageGen/ImageGen.Presentation/Services/IImageViewerService.cs` (новый), `ImageGen/ImageGen.Presentation/Services/ImageViewerItem.cs` (новый), `ImageGen/ImageGen.Presentation/PageModels/ChatPageModel.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `ImageGen/ImageGen.Presentation.Tests/PageModels/ChatPageModelTests.cs`, `ImageGen/ImageGen.Presentation.Tests/Design/MauiChatPageMarkupTests.cs`, `ImageGen/ImageGen.Maui/Services/MauiImageViewerService.cs` (новый), `ImageGen/ImageGen.Maui/Pages/ChatPage.xaml`, `ImageGen/ImageGen.Maui/MauiProgram.cs`, `ImageGen/ImageGen.Avalonia/Services/AvaloniaImageViewerService.cs` (новый), `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml`, `ImageGen/ImageGen.Avalonia/Composition/AppServices.cs`, `ImageGen/ImageGen.Avalonia.Tests/Composition/AppServicesTests.cs`, `docs/specs/image-client.md`, задачи ручных проверок 016/046/140

## Отчёт

Follow-up при проверке Windows CI для PR #51: Avalonia ChatView создавал `Bitmap` прямо из пути, из-за чего Windows удерживал PNG открытым до освобождения UI bitmap и не давал удалить тестовую папку. Конвертер теперь декодирует из байтов в памяти; добавлен регрессионный тест удаления файла при живом bitmap. Это не меняет поведение viewer.

Добавлен общий `IImageViewerService`; ChatPageModel передаёт в него успешные изображения выбранного run в исходном порядке и отмечает выбранную карточку. Растянутая на картинку кнопка открывает viewer; card actions и placeholder не открывают. MAUI показывает modal page с AspectFit, navigation без wrap, Close/Android Back/Windows Escape, фоновым чтением файлов и `ImageSource.FromStream`. Avalonia показывает owned modal без OS fullscreen, с AspectFit, Escape/Left/Right, фоновым созданием Bitmap, проверкой формата/декодирования и освобождением Bitmap. Добавлены строки, обновлена F6.2.

Platform view деревья сделаны программно внутри page/service вместо отдельных XAML files; это отклонение от плана, контракт и dependency boundaries сохранены. Проверено: Presentation.Tests — 387/387; Avalonia.Tests — 143 passed, 2 skipped (DPAPI и Secret Service недоступны в этой среде); Avalonia build и MAUI Android build прошли; форматирование всех изменённых проектов прошло. Ручной UI не запускался в этой среде; сценарии переданы в 016/046/140. iOS/MacCatalyst не собирались.

## Комментарии

### 2026-10-29, codex-review
Задача нужна отдельно от compare и OS fullscreen. Убрана функционально лишняя 088; уточнены RunItemViewModel, MAUI FromStream, lifetime/cancellation и границы тестов. Не требуется Mac-only реализация для завершения Android/desktop части.

### 2026-10-30, codex-audit — проверка завершённости
Статус done не подтверждён полностью; задача оставлена вне архива и переведена в blocked до фиксов 146, 148, 149. Не выполнены исходные viewer требования: rollback при failed previous/next использует index-1 вместо последнего показанного; MAUI Windows handler обрабатывает только Escape, Left/Right отсутствуют; MAUI background tap ошибочно использует null GetPosition вместо hit-test. Подробности и тесты в отдельных fix tasks, Windows часть изолирована148.
Исторический отчёт и прежние комментарии сохранены. Остаток реализовывать только в названных follow-up задачах, не занимать эту задачу параллельно им. Вернуть done после их merge и проверки исходных критериев; отсутствие устройства само по себе причиной reopen не было.
