# 2026-10-29: VPS после релизов — оболочка Avalonia и слой Presentation

## Сделано
- `docs/knowledge/vps-session.md` дополнен: добавлены Presentation и Avalonia (сборка, headless-тесты, запуск окна под `Xvfb`), обновлены счётчики тестов, в рецепт установки добавлен `xvfb`.
- `docs/knowledge/dev-environment.md` — в колонке VPS вместо «не проверено» для `ImageGen.Presentation` и `ImageGen.Avalonia` проставлены проверенные значения; в список сред добавлено, что VPS собирает и тестирует все Linux-проекты.

## Проверка на машине (2026-10-29, `main` = 3bf171b)
- Сборка: `ImageGen.Core`, `ImageGen.Data`, `ImageGen.Presentation`, `ImageGen.Avalonia` — успешно; MAUI под Android — успешно.
- Тесты: Core.Tests 356/356, Data.Tests 70/70, Presentation.Tests 277/277, Avalonia.Tests 118/120 (два пропущены: Secret Service — нужен сессионный keyring, DPAPI — только Windows).
- **Окно Avalonia запускается** под `Xvfb`: приложение стартует, создаёт `imagegen.db` (схема версии 8 — миграции применяются), пишет журнал `logs/imagegen-20261029.log` первой строкой с версией, оболочкой, ОС и путями, переходит на экран сессий и экран ключа.
- `dotnet format --verify-no-changes` — проходит по всем проектам, включая Avalonia и Presentation.

## На что обратить внимание
- Счётчики тестов в `docs/knowledge/dev-environment.md` для колонки VPS устарели после релизов `v0.1.0`–`v0.1.4` и задач 012–015; обновлены здесь.
- При сборке `ImageGen.Avalonia` есть предупреждение Avalonia `AVLN5001`: `TextBox.Watermark` устарел, нужно `PlaceholderText` (`ImageGen.Avalonia/Views/SessionsListView.axaml:30`). Код не трогал — это не задача VPS; отмечено как находка.
- Запуск под `Xvfb` — это запуск, а не ручная проверка интерфейса: экран не перед глазами, кликать по окну по-прежнему нельзя. Задача 046 остаётся за человеком у экрана.
