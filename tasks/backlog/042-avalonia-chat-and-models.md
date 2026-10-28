---
id: 042
status: planned
assignee:
claimed:
created: 2026-10-28
---

# Avalonia: чат сессии, карточки генераций и модели сессии

## Описание
Сделать в Avalonia то, что в MAUI делают задачи 014–015: страницу чата (лента сообщений и прогонов, ввод, кнопка моделей, карточки генераций с Repeat / New seed / Edit, обновление карточек по событию) и страницу моделей сессии (поиск, галочки, число запусков, Refresh, Save). Модели страниц — общие из `ImageGen.Presentation`.

Зависит от: 041.
Среда: Linux или Windows.

## Критерии готовности
- Отправка сообщения с ответом ассистента «generate_images» (подмена HTTP) даёт прогон с карточками; карточка проходит «Queued/Generating...» → «Done» с картинкой без перезагрузки страницы.
- Repeat, New seed, Edit создают новые прогоны; Edit спрашивает промпт.
- Экран моделей сохраняет выбор и число запусков; подпись кнопки в чате меняется на «Models: N × M».
- Headless-тесты из плана проходят на Linux; после этой задачи Avalonia повторяет скелет MAUI (ручная проверка — как задача 016, на Linux и Windows).

## Анализ
- Строки ленты двух типов: в Avalonia шаблоны выбираются по типу данных (`DataTemplate DataType=…` в `ItemsControl`/`ListBox`), отдельный селектор не нужен; логика `ChatItemTemplateChoice` остаётся общей и на всякий случай используется в тесте.
- Картинка карточки: `GenerationItemViewModel.ImageFilePath` → конвертер в `Bitmap` (аналог `FilePathToImageSourceConverter`). Файл может появиться позже, чем карточка: конвертер возвращает `null` для пустого пути.
- Обновление карточек приходит из фонового потока через `IMainThread` (реализация 039 — `Dispatcher.UIThread.Post`).
- Карточки в несколько колонок с переносом: `WrapPanel` в `ItemsControl` (аналог `FlexLayout` в MAUI).
- Степпер числа запусков: `NumericUpDown` (1–10, шаг 1); `SessionModelsPageModel.RunsPerModel` сейчас `double` под MAUI-`Stepper` — совместимо.

## План
1. `Converters/FilePathToBitmapConverter` (полный путь → `Bitmap`, ошибка чтения → `null`).
2. `Views/ChatView`: верх — кнопка с `ModelsButtonText`; лента `ItemsControl` с шаблонами `MessageItemViewModel` (роль, текст, ошибка красным) и `RunItemViewModel` (промпт, `WrapPanel` карточек: картинка 256 по высоте или индикатор, статус, детали, ошибка, три кнопки); строка «Assistant is thinking...» при `IsBusy`; внизу многострочное поле и Send (Enter — перевод строки, Ctrl+Enter — отправка). Автопрокрутка к последней строке после перезагрузки ленты.
3. `Views/SessionModelsView`: поиск, `NumericUpDown`, Refresh, индикатор, список с `CheckBox`, Save.
4. Маршруты `chat` и `models` в `AvaloniaNavigationService` (заглушка из 041 убирается).
5. Тест-кейсы (headless; реальные сервисы ядра и Data на временной БД, HTTP подменён):
   - отправка «draw a red fox» с ответом-вызовом инструмента → в ленте сообщение и прогон, карточка в «Done» после `WhenIdleAsync`, картинка загружена конвертером;
   - событие генерации обновляет карточку без пересоздания ленты (тот же экземпляр элемента);
   - Edit с промптом из диалога (подмена) → новый прогон с этим промптом; отмена → нового прогона нет;
   - кнопка моделей → `SessionModelsView`; выбрать модель, число 3, Save → назад в чат, подпись «Models: 1 × 3»;
   - поиск фильтрует список, отмеченные модели не теряются;
   - ошибка ассистента (429) → сообщение-ошибка в ленте красным, текст из поля не теряется.
6. Документация: `docs/knowledge/avalonia.md`, `docs/architecture.md`; в задачу 016 (или новую задачу ручной проверки) добавить сценарий для Avalonia на Linux и Windows — по решению владельца.

### Команды проверки
```
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Avalonia/Views/ChatView.axaml*`, `ImageGen/ImageGen.Avalonia/Views/SessionModelsView.axaml*`, `ImageGen/ImageGen.Avalonia/Converters/FilePathToBitmapConverter.cs`, `ImageGen/ImageGen.Avalonia/Services/AvaloniaNavigationService.cs`, `ImageGen/ImageGen.Avalonia/App.axaml.cs`, `ImageGen/ImageGen.Avalonia.Tests/**`, `docs/knowledge/avalonia.md`, `docs/architecture.md`

## Отчёт

## Комментарии
