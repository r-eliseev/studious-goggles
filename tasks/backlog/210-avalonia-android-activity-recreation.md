---
id: 210
status: done
assignee:
claimed:
created: 2026-11-06
---

# Avalonia Android: пересоздание активности и сохранение в галерею

## Описание
Находки ревью PR #78 (задачи 195, 193–194), 2026-11-06.

1. **Пустой экран после пересоздания активности.** `MainActivity` объявляет `ConfigurationChanges = Orientation | ScreenSize | UiMode`, поэтому смена шрифта, языка, плотности экрана, разделение экрана (`ScreenLayout`, `SmallestScreenSize`) или пересоздание активности системой создают новый `MainView`. Его конструктор вызывает `AvaloniaNavigationService.AttachHost(PageHost)`, но `AttachHost` только запоминает хост и не переносит туда текущую страницу; маршрут `Sessions` повторно не открывается (`MobileApp.Started = true`). Экран пуст, а страница стека остаётся дочерним элементом старого `PageHost` — следующий `GoBackAsync` попробует показать её в новом хосте, и Avalonia, вероятно, выбросит «already has a visual parent». Открытые overlay (диалог, просмотр, сравнение) остаются в `OverlayHost` старого `MainView`.
2. **Сохранение в галерею.** `AndroidImageSaver.SaveAsync` удаляет незавершённую запись MediaStore только при `OperationCanceledException`, `UnauthorizedAccessException`, `IOException`; Java-исключение (`Java.Lang.Exception`, например `SecurityException`) оставляет «висящую» запись `IS_PENDING = 1` и выходит наружу.

Зависит от: —
Среда: Linux (headless-тесты Avalonia, сборка Android-головы); проверка на телефоне — 198.

## Критерии готовности
- `AvaloniaNavigationService.AttachHost(host)`: если стек не пуст и новый хост отличается от прежнего — у прежнего хоста `Content = null`, текущая (верхняя) страница стека показывается в новом хосте тем же способом, что при навигации (`Show`, без повторного `OnAppearing`, если `Show` его вызывает — тогда вызывать как при возврате на страницу; выбрать и записать в «Отчёт»). Повторный `AttachHost` того же хоста ничего не меняет. Пустой стек — как сейчас.
- После переноса `GoBackAsync` и `GoToAsync` работают без исключений (страницы стека больше не привязаны к старому хосту).
- `MainView` при откреплении от дерева (`OnDetachedFromVisualTree`) закрывает все открытые overlay: вызывает `TryCloseTop()` у трёх overlay-служб, пока каждая возвращает `true` (диалоги завершаются как «Отмена»).
- `AndroidImageSaver.SaveAsync`: любое исключение после `Insert` (кроме `OperationCanceledException`, который пробрасывается после удаления записи) удаляет запись MediaStore; `Java.Lang.Exception` → `ImageSaveResult.Failed` с одной Warning в журнал (тип исключения, без пути и содержимого). Для этого сохранитель получает `ILogger<AndroidImageSaver>` через DI.
- Headless-тесты навигации на перенос хоста; Android-голова собирается без предупреждений.
- Пункт 198: повернуть экран, включить разделение экрана, сменить размер шрифта в настройках Android и вернуться — экран и кнопка Back работают; открытый диалог закрывается без зависания.

## Анализ
- `ImageGen/ImageGen.Avalonia/Services/Navigation/AvaloniaNavigationService.cs`: поле `_host`, `_stack` (`(Route, View)`), методы `RequireHost`, `Show`, `Leave`; тесты — `ImageGen/ImageGen.Avalonia.Tests/Services/Navigation/AvaloniaNavigationServiceTests.cs`.
- `ImageGen/ImageGen.Avalonia.Android/Views/MainView.axaml.cs`: конструктор (`AttachHost`, `MobileApp.Started`), `OnDetachedFromVisualTree`, `OnBackRequested` (порядок overlay-служб).
- `ImageGen/ImageGen.Avalonia.Android/Platform/AndroidImageSaver.cs`; регистрация — `RegisterAndroid` в `MobileApp.axaml.cs`.
- Десктоп вызывает `AttachHost` один раз — поведение не меняется.

## План
1. Падающие тесты в `AvaloniaNavigationServiceTests` (headless, два `ContentControl` в тестовом окне):
   - перейти на два маршрута с хостом A, затем `AttachHost(B)` → `B.Content` — страница второго маршрута, `A.Content == null`;
   - после этого `GoBackAsync` → `B.Content` — страница первого маршрута, исключения нет;
   - `AttachHost(A)` повторно на том же хосте → содержимое не меняется, `OnAppearing`/`OnDisappearing` страниц не вызваны лишний раз (проверить по существующим тестовым страницам-двойникам).
2. Реализация `AttachHost`.
3. `MainView.OnDetachedFromVisualTree` — закрытие overlay.
4. `AndroidImageSaver` — обработка исключений и логгер; сборка Android-головы.
5. Пункт в 198.

Файлы: `ImageGen/ImageGen.Avalonia/Services/Navigation/AvaloniaNavigationService.cs`, `ImageGen/ImageGen.Avalonia.Tests/Services/Navigation/AvaloniaNavigationServiceTests.cs`, `ImageGen/ImageGen.Avalonia.Android/Views/MainView.axaml.cs`, `ImageGen/ImageGen.Avalonia.Android/Platform/AndroidImageSaver.cs`, `ImageGen/ImageGen.Avalonia.Android/MobileApp.axaml.cs`, `tasks/backlog/198-avalonia-android-manual-check.md`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj -c Release
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia.Android/ImageGen.Avalonia.Android.csproj --verify-no-changes
bash scripts/check.sh
```

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы» и названные тесты; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): падающий тест → запуск, убедиться, что он падает именно по описанной причине → минимальный фикс → тест зелёный.
- Не меняй файлы вне строки «Файлы», не переименовывай существующие члены сверх плана, не чини соседние находки — они в отдельных задачах.
- Если код расходится с планом (нет такого метода, другая сигнатура, тест уже есть), не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: что изменено, команды и результаты, отличия от плана.

## Отчёт

- `AvaloniaNavigationService.AttachHost` теперь переносит верхнюю страницу стека в новый ContentControl, очищает старый host, вызывает `OnAppearing` при повторном показе и ничего не делает при повторном подключении того же host. Добавлен headless-тест с переходом назад после переноса.
- При откреплении `MainView` по очереди закрывает все открытые диалоги, просмотр и сравнение изображений; каждый overlay-сервис опрашивается до пустого стека.
- `AndroidImageSaver` теперь получает `ILogger<AndroidImageSaver>`. Публикация MediaStore выполняется внутри защищённого блока; отмена удаляет pending запись и пробрасывается, ошибки доступа и IO чистят запись, Java exception чистит её, возвращает `Failed` и пишет одну Warning только с типом исключения. Прочие исключения чистят запись и пробрасываются. Удаление pending записи не маскирует исходную ошибку.
- В задачу 198 добавлена ручная проверка поворота, split-screen, изменения шрифта, возврата назад и закрытия overlay после пересоздания Activity.
- Проверки: Avalonia headless — 180 passed, 2 skipped (Windows DPAPI и недоступная D-Bus-служба); Android Release build — 0 warnings/0 errors; `dotnet format` для Avalonia, Avalonia.Tests и Avalonia.Android — успешно; `bash scripts/check.sh` — успешно.
- Ручной запуск на Android-устройстве не выполнялся в этой среде; сценарий внесён в пункт 198.

## Комментарии

### 2026-11-06, claude-audit
Создано по ревью PR #78 по поручению владельца; план для Luna.

### 2026-11-06, владелец (в чате, записал claude-audit)
План одобрен: «заапрувь все таски которые будут».
