---
id: 059
status: done
assignee:
claimed:
created: 2026-10-28
---

# Настройки внешнего вида: стиль и тема

## Описание
Первая задача на вёрстку по макетам задачи 051. Приложение получает две настройки: стиль (Quiet, Gallery, Notebook) и тему (System, Light, Dark), ADR 0014, `docs/specs/chat-sessions.md` S6. Здесь только модель настроек, хранение и сервис в `ImageGen.Presentation`, без разметки: оболочки подключат его в задачах 061 и 062, экран настроек — 065.

Справочник: `docs/design/implementation.md` (ключи ресурсов, значения, шрифты, значки — дословно оттуда); токены: `docs/design/tokens.json`; макеты: `docs/design/prototype.html` (открыть: `python3 -m http.server` в `docs/design/`, затем http://localhost:8000/prototype.html; опубликованная версия — https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решение: ADR 0014.

Зависит от: —.
Среда: Linux или Windows (только `ImageGen.Presentation` и его тесты).

## Критерии готовности
- В `ImageGen.Presentation` есть типы из «Плана»; настройки читаются из `appearance.json` в папке данных и пишутся туда же.
- Неизвестные значения и испорченный файл дают значения по умолчанию (Quiet, System) и предупреждение в журнал, приложение не падает.
- `AppearanceService.Changed` срабатывает при сохранении новых значений и не срабатывает при сохранении тех же.
- Тест-кейсы из «Плана» проходят; `dotnet format` без изменений.

## Анализ
- Похожий образец чтения файла из папки данных: `ImageGen.Presentation/Logging/LogLevelSettings.cs` (`logging.json`, `JsonDocument`, предупреждение вместо исключения).
- Папка данных: `IAppPaths.DataDirectory` (`ImageGen.Core/Platform/IAppPaths.cs`), зарегистрирован в DI обеих оболочек.
- Настройки внешнего вида — не секрет, поэтому файл, а не `ISecretStore` (`docs/conventions.md`, «Секреты и данные»).
- Регистрация в DI оболочек — в задачах 061 (Avalonia) и 062 (MAUI), здесь не делается.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, ключей и строк переноси дословно. Если шаг не получается так, как написано (компилятор не принимает разметку, API другое), не придумывай обход: опиши в «Комментариях», что пробовал и что получилось, поставь `blocked` и остановись.

Типы (каталог `ImageGen.Presentation/Appearance/`, пространство имён `ImageGen.Presentation.Appearance`, один тип на файл):
- `AppearanceStyle` — enum: `Quiet`, `Gallery`, `Notebook`.
- `AppearanceTheme` — enum: `System`, `Light`, `Dark`.
- `AppearanceSettings` — `sealed record AppearanceSettings(AppearanceStyle Style, AppearanceTheme Theme)` со статическим свойством `Default` = (Quiet, System).
- `AppearanceSettingsFile` — статический класс, чистые функции без ввода-вывода:
  - `const string FileName = "appearance.json"`;
  - `Parse(string? json)` → `(AppearanceSettings Settings, string? Warning)`: `null`/пусто → Default без предупреждения; поля `style` и `theme` — строки без учёта регистра (`quiet`, `gallery`, `notebook`; `system`, `light`, `dark`); отсутствующее поле → значение по умолчанию без предупреждения; неизвестное значение → значение по умолчанию и предупреждение `"appearance.json: unknown style 'x'; using quiet."` (для темы — `unknown theme`); не JSON → Default и предупреждение `"appearance.json is not valid JSON (...); using the defaults."`;
  - `Serialize(AppearanceSettings)` → `{"style":"quiet","theme":"system"}` (строчные, без пробелов, `JsonSerializer` с `JsonNamingPolicy` не нужен — пишется объектом с двумя строками).
- `AppearanceService` — `sealed class`, конструктор `(IAppPaths paths, ILogger<AppearanceService> logger)`:
  - `AppearanceSettings Current { get; }` — читается из файла при первом обращении (ленивая загрузка; предупреждение пишется в журнал `LogWarning("{Warning}", warning)`);
  - `Task SaveAsync(AppearanceSettings settings, CancellationToken cancellationToken)` — если равно `Current`, ничего не делает; иначе пишет файл (`File.WriteAllTextAsync`), обновляет `Current`, вызывает событие;
  - `event EventHandler<AppearanceSettings>? Changed`.

Тест-кейсы (`ImageGen.Presentation.Tests/Appearance/AppearanceSettingsFileTests.cs` и `AppearanceServiceTests.cs`; временная папка — как в существующих тестах Presentation, `Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))`, удалить в `Dispose`):
1. `Parse_Null_ReturnsDefaultWithoutWarning`.
2. `Parse_GalleryDark_ReturnsGalleryDark` (`{"style":"gallery","theme":"dark"}`).
3. `Parse_UpperCaseValues_AreAccepted` (`"NOTEBOOK"`, `"Light"`).
4. `Parse_MissingTheme_UsesSystemWithoutWarning`.
5. `Parse_UnknownStyle_UsesQuietAndWarns` — предупреждение содержит `unknown style`.
6. `Parse_InvalidJson_ReturnsDefaultAndWarns`.
7. `Serialize_ThenParse_RoundTrips` — для всех 9 сочетаний.
8. `Current_NoFile_ReturnsDefault`.
9. `SaveAsync_NewValue_WritesFileAndRaisesChanged` — файл содержит `"gallery"`, событие пришло один раз с новым значением.
10. `SaveAsync_SameValue_DoesNotRaiseChanged`.
11. `Current_AfterSaveInAnotherInstance_ReadsSavedValue` — второй `AppearanceService` на той же папке читает сохранённое.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Presentation/Appearance/*.cs` (новые), `ImageGen/ImageGen.Presentation.Tests/Appearance/*.cs` (новые)

## Отчёт

Сделано (Linux, `ImageGen.Presentation`):
- `Appearance/AppearanceStyle.cs`, `AppearanceTheme.cs`, `AppearanceSettings.cs` (record с `Default` = Quiet/System).
- `Appearance/AppearanceSettingsFile.cs`: `Parse` и `Serialize`; неизвестное значение, не-JSON и JSON не в виде объекта дают значения по умолчанию и предупреждение.
- `Appearance/AppearanceService.cs`: ленивая загрузка `appearance.json`, `SaveAsync` (без события при том же значении), предупреждения в журнал.

Отличие от плана: событие `Changed` объявлено как `Action<AppearanceSettings>`, а не `EventHandler<AppearanceSettings>` (у `EventHandler<T>` `T` должен быть `EventArgs`). Добавлен тест на JSON-массив в корне (в плане его не было: `TryGetProperty` на массиве бросает исключение).

Проверка: `ImageGen.Presentation.Tests` 153/153 (было 140), `dotnet format --verify-no-changes` для Presentation и его тестов — без изменений.

## Комментарии
