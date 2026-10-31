---
id: 028
status: cancelled
assignee:
claimed:
created: 2026-10-27
---

# Русский язык интерфейса

## Описание
Русская локализация общего `AppResources` и выбор ru/en (F10.1) для MAUI и Avalonia. Светлая/тёмная тема уже реализована 059–062, коды ошибок — 043; повторять их в этой задаче не нужно.

Зависит от: —
Среда: Linux или Windows; обе UI-оболочки.

Скелет готов, 016 не блокирует подготовку плана. Статус `todo`: перед реализацией одобрить конкретный механизм переключения/обновления строк.

## Критерии готовности
- Русский набор содержит все ключи актуального нейтрального AppResources.resx, сохраняет номера и количество format placeholders; neutral fallback остаётся английским.
- Выбор ru/en сохраняется и применяется до создания страниц; неверное/повреждённое значение имеет документированный безопасный fallback.
- Переключение даёт одинаковое поведение в MAUI/Avalonia: либо обновляет все открытые страницы, либо явно сообщает о необходимости перезапуска; окончательный вариант фиксируется в плане до реализации.
- Переводятся элементы интерфейса и коды ошибок; пользовательский текст, model/provider id, сохранённые старые provider messages и промпты не переводятся.
- Темы и формат хранения данных не меняются; decimal/JSON/SQL остаются culture-invariant.

## Анализ
`Presentation/Resources/Strings/AppResources.cs` уже использует CurrentUICulture, но MAUI x:Static и Avalonia `Markup/ResExtension` возвращают строки при построении view; одной смены CultureInfo недостаточно для живого UI. `SettingsPageModel.Styles` тоже создаёт локализованные строки один раз. CurrentCulture и CurrentUICulture имеют разные назначения: перевод не должен менять сетевой/SQL формат чисел.

Нужны новый AppResources.ru.resx, способ хранения выбора языка и политика обновления существующих PageModel/Views. Существующие ресурсные тесты рефлексией считают все публичные static свойства AppResources ключами, поэтому добавление туда свойства Culture сломает тест: настройку культуры держать в отдельном сервисе либо явно адаптировать фильтр теста.

Пересечения: 135 меняет SettingsPageModel и Settings UI; 131/133/135/136 добавляют строки. При rebase сохранять все новые ключи и дополнять русский набор до актуального английского. Для слабой модели лучше разделить перевод/проверку паритета и runtime-переключение, если второй пункт требует переделки всех markup bindings.

## План
Этап подготовки плана, код пока не писать:
1. Прочитать AppResources.cs/resx, AppResourcesTests, ResExtension, SettingsPageModel и bootstrap обеих оболочек. Зафиксировать политику переключения открытых экранов (refresh или restart), значение по умолчанию и поведение неизвестной culture; при отсутствии решения задать точный вопрос.
2. Перечислить все ключи, format placeholders, plural/count strings и error mappings; описать отдельный LocalizationService/settings file под IAppPaths.DataDirectory и порядок startup до создания Views. Не смешивать локаль с appearance.json без обратной совместимости.
3. Определить UI выбора языка и строки обратной связи. Для live refresh описать единый observable источник строк и пересоздание cached labels; для restart — не показывать частично переведённый экран.
4. Tests: одинаковые ключи нейтрального/ru набора (читать ru без parent fallback), все значения непустые; одинаковые format indices, форматирование ru/en с N>1; культура восстанавливается после теста и тесты не гоняются с общей mutable culture; startup/fallback/сохранение; нейтральный wire format под ru-RU.
5. Назвать точные новые файлы и затронутые registrations/Views, оформить небольшой одобряемый план (при необходимости разделить). После анализа `planned`, дождаться одобрения; в код Core UI-строки не добавлять.
6. Будущие проверки: Presentation.Tests, Avalonia.Tests, build MAUI Android, format изменённых проектов, `bash scripts/check.sh`. В отчёте указать ручную проверку обеих оболочек и снятие занятости по `docs/tasks.md`.

Файлы для анализа: `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.cs`, `ImageGen/ImageGen.Presentation/Resources/Strings/AppResources.resx`, `ImageGen/ImageGen.Presentation.Tests/Resources/AppResourcesTests.cs`, `ImageGen/ImageGen.Presentation/PageModels/SettingsPageModel.cs`, `ImageGen/ImageGen.Avalonia/Markup/ResExtension.cs`, `ImageGen/ImageGen.Presentation/Services/AppInitializer.cs`, `ImageGen/ImageGen.Maui/MauiProgram.cs`, `ImageGen/ImageGen.Avalonia/Composition/AppServices.cs`. Файлы будущей реализации назвать в окончательном плане.

## Отчёт

## Комментарии

### 2026-10-29, claude-536f
Заблокировано: Детализация после задачи 016 (ручная проверка у владельца). Задача остаётся без плана до снятия блокера; код не пишется.

### 2026-10-29, claude-audit
Аудит 2026-10-29: темы уже сделаны задачами 059–062, коды ошибок — 043. Из задачи осталась только русская локализация (`AppResources.ru.resx` нет). При детализации сузить задачу.

### 2026-10-29, codex-backlog-review
Ревью по поручению владельца: устаревшая блокировка ожиданием 016 снята; скелет уже существует. Задача возвращена в `todo` для подготовки одобряемого плана. Текущие границы и критерии уточнены по коду и спецификации; это не одобрение реализации. Исторические комментарии выше сохранены.

### 2026-10-31, владелец (в чате, записал claude-audit)
Убрать: интерфейс только на английском, промпты тоже; чат работает на том языке, на котором пишет человек. Задача отменена. Поведение ассистента уже такое (`AssistantPrompts`: «Reply in the user's language. The prompt is always in English.»); F10.1 в `docs/specs/image-client.md` и `docs/architecture.md` обновлены.
