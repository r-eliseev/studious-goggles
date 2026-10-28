---
id: 072
status: approved
assignee:
claimed:
created: 2026-10-28
---

# Диалоги Avalonia в новом стиле и порядок кнопок по ОС

## Описание
Диалоги Avalonia (`AvaloniaDialogService`: подтверждение, ввод, выбор из 069) — по макету «Диалоги»: заголовок 17, текст, поле ввода (для Edit prompt — многострочное), кнопки внизу. Порядок кнопок: Windows и Linux — основная первой; macOS — основная справа (`implementation.md`, раздел 7). Опасное действие (Delete) — `Button.danger`. В MAUI диалоги системные и уже в порядке платформы — не меняются.

Справочник: `docs/design/implementation.md` (ключи ресурсов, значения, шрифты, значки — дословно оттуда); токены: `docs/design/tokens.json`; макеты: `docs/design/prototype.html` (открыть: `python3 -m http.server` в `docs/design/`, затем http://localhost:8000/prototype.html; опубликованная версия — https://claude.ai/artifact/M9QzPf39KLkRFTnYBtCi6F); решение: ADR 0014.

Зависит от: 063.
Среда: Linux или Windows (Avalonia).

## Критерии готовности
- Окна диалогов используют стили 063 и ресурсы `Ig*` (фон `IgSurfaceBrush`, радиус `IgRadiusLg`).
- Порядок кнопок зависит от ОС; на macOS основная справа (проверяется тестом с подменой признака ОС).
- В `IDialogService` есть перегрузка `ConfirmAsync(..., bool destructive)`; при `destructive: true` основная кнопка Avalonia — `danger`. Место вызова (удаление сессии) переключает задача 067: эта задача `SessionsPageModel` не трогает, чтобы идти параллельно с 066.
- Esc закрывает как «Cancel», Enter — основная кнопка (для однострочного ввода).
- Тесты из «Плана» проходят.

## Анализ
- `AvaloniaDialogService` строит окна в коде (`ShowAsync`), без разметки. Порядок задаётся порядком добавления кнопок в панель.
- Признак ОС для теста: конструктор получает `bool isMacOs` (по умолчанию `OperatingSystem.IsMacOS()`), тест передаёт значение.
- Опасное действие: перегрузка `ConfirmAsync(title, message, accept, cancel, bool destructive)` — метод интерфейса, реализации в Avalonia, MAUI (`DisplayAlert`, то же самое) и подменах тестов. Вызов с `destructive: true` из `SessionsPageModel` (удаление сессии) делает задача 067.

## План
Для исполнителя (план рассчитан на небольшую модель): делай шаги строго по порядку, по TDD (сначала падающий тест, потом минимальный код), после каждого шага — сборка и тесты из «Проверки». Имена файлов, типов, ключей и строк переноси дословно. Если шаг не получается так, как написано (компилятор не принимает разметку, API другое), не придумывай обход: опиши в «Комментариях», что пробовал и что получилось, поставь `blocked` и остановись.

1. Presentation: `IDialogService.ConfirmAsync(..., bool destructive)` (новая перегрузка). Обновить все подмены `IDialogService` в тестах (Presentation.Tests и Avalonia.Tests).
2. Avalonia `AvaloniaDialogService`: окно без системного заголовка не делать (оставить системное окно), содержимое — `Border` с отступами 20, заголовок `TextBlock` 17/600, текст, поле (`TextBox`, при `PromptAsync` для Edit — `AcceptsReturn=true`, высота 96), панель кнопок справа; порядок: `isMacOs` ? [Cancel, Accept] : [Accept, Cancel]; `Accept` — классы `primary` или `danger`; `IsDefault`/`IsCancel` у кнопок.
3. MAUI `ShellDialogService` — реализовать новую перегрузку (тот же `DisplayAlert`).
4. Тесты Avalonia `Services/AvaloniaDialogServiceTests.cs` (есть ли — проверить, иначе создать): `Confirm_NotMac_AcceptIsFirst`, `Confirm_Mac_AcceptIsLast`, `Confirm_Destructive_AcceptHasDangerClass`, `Escape_Cancels`.

### Проверка
```
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet test --project ImageGen/ImageGen.Avalonia.Tests/ImageGen.Avalonia.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-android -p:AndroidSdkDirectory=$ANDROID_HOME
dotnet format ImageGen/ImageGen.Presentation/ImageGen.Presentation.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj --verify-no-changes
dotnet format ImageGen/ImageGen.Maui/ImageGen.Maui.csproj --verify-no-changes
scripts/check.sh
```

Файлы: `ImageGen/ImageGen.Presentation/Services/IDialogService.cs`, `ImageGen/ImageGen.Avalonia/Services/AvaloniaDialogService.cs`, `ImageGen/ImageGen.Maui/Services/ShellDialogService.cs`, тестовые подмены `IDialogService`, тесты

## Отчёт

## Комментарии
