---
id: 148
status: approved
assignee:
claimed:
created: 2026-10-30
---

# MAUI Windows viewer: навигация стрелками клавиатуры

## Описание
В131 явно требуются Desktop Left/Right. Windows handler MauiImageViewerPage.OnWindowsKeyDown сейчас обрабатывает только Escape; навигация стрелками не реализована.

Зависит от: 146, 149.
Среда: только Windows (WinUI handler и проверка клавиатуры).

## Критерии готовности
- Left/Right переключают изображения того же run без wrap, Escape по-прежнему закрывает.
- Обработанные клавиши не выполняют вторую навигацию; остальные клавиши не поглощаются.
- После смены handler нет дублированных подписок, закрытая страница не реагирует.
- Проверена Windows сборка и клавиатура; Android ветка компилируется без WinUI типов вне #if WINDOWS.

## Анализ
Evidence: MauiImageViewerService.cs:125–133 содержит только VirtualKey.Escape;131 критерий Desktop Left/Right. Это пропуск исходного требования. Общая навигация/failed loads сначала исправляются146, background tap149 меняет тот же файл, затем Windows часть. Не брать задачу на Linux ради source-only проверки.

## План
1. Прочитать OnHandlerChanged/OnWindowsKeyDown, Navigate и общий cursor после146; проверить доступную Windows среду.
2. Добавить проверку клавиатурного mapping в имеющийся Windows test project, если он может загрузить handler; иначе вынести только mapping в маленькую тестируемую функцию без WinUI типов и проверить её в Presentation.Tests. Не ограничиваться поиском строк в исходнике.
3. В #if WINDOWS handler добавить Left→Navigate(-1), Right→Navigate(1), Handled=true; Escape сохранить. Navigation state146 обеспечивает границы, не дублировать вычисление индексов.
4. Сохранить снятие старой native subscription перед подключением новой; при уничтожении handler подписка тоже снимается. Другие клавиши оставить необработанными.
5. На Windows открыть viewer из средней картинки и пройти Left/Right/границы/Escape, повторно открыть; проверить фокус на Close и отсутствие double action. Записать commit/OS/результат в016 и отчёт.
6. После слияния146/148/149 проверить все остатки131 и вернуть131 в done при выполнении критериев. Не закрывать131 только потому, что выполнена одна148.

Файлы: `ImageGen/ImageGen.Maui/Services/MauiImageViewerService.cs` (#if WINDOWS), подходящий тестовый helper и тест при необходимости, `tasks/v0.1/016-skeleton-manual-check.md`, `tasks/backlog/131-fullscreen-image-viewer.md`.

Проверка: `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0`; Presentation.Tests; Android build по CI; Windows ручная проверка.

### Команды проверки

Выполнять по одной, из корня репозитория:

```bash
dotnet test --project ImageGen/ImageGen.Presentation.Tests/ImageGen.Presentation.Tests.csproj
dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-windows10.0.19041.0
bash scripts/check.sh
```

### Порядок исполнения и проверки
План рассчитан на Luna: прочитать перечисленные файлы, выполнить шаги по порядку; сначала падающий регрессионный тест, затем минимальный фикс (ub-mode из AGENTS.md). Не реализовывать соседние задачи. Сигнатуры сверять с текущим main; при rebase сохранять чужие изменения. Не использовать платные API и не записывать секреты. Команды выполнять последовательно: общие build outputs не допускают параллельные dotnet build/test.
После изменения каждого проекта: `dotnet format ImageGen/<Проект>/<Проект>.csproj --verify-no-changes`; после всех изменений — перечисленные тесты и `bash scripts/check.sh`. В отчёте указать команды, результаты, ручные ограничения и отличие от плана. Код писать только после одобрения плана по docs/tasks.md.

## Отчёт

## Комментарии

### 2026-10-30, codex-audit
Создано по поручению владельца: подробный план для Luna. В этой задаче код ещё не исправлен; статус planned не означает одобрение реализации.

### 2026-10-30, владелец (в чате, записал codex)
План одобрен явным поручением «Заапрувь таски на фиксы». Можно реализовывать по описанному плану после готовности зависимостей в main и проверки среды/занятости по docs/tasks.md.
