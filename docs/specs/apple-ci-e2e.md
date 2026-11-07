# Apple CI и UI-тесты: MAUI и Avalonia на iOS и macOS

Статус: черновик
Код: `.github/workflows/apple-ci.yml`, `ImageGen/ImageGen.Avalonia.iOS`, `ImageGen/ImageGen.E2E.iOS`, `ImageGen/ImageGen.E2E.Common/ci/apple-*` — появятся в задачах 244–250.

Основа — дополнение владельца «MAUI + Avalonia — Apple CI/E2E» от 2026-11-07. Оригинал лежит в `docs/specs/ui-e2e-original/`. Это дополнение к `docs/specs/ui-e2e.md`, а не его замена. Решения и проверенные факты — в ADR 0030.

## Цель
Получить на раннерах GitHub без Apple ID, сертификатов и секретов:
- раздельные статусы четырёх сборок — MAUI iOS, MAUI Mac Catalyst, Avalonia iOS, Avalonia macOS;
- запуск обоих iOS-приложений в Simulator с доказательствами;
- UI-тесты iOS;
- проверку GUI на macOS.

Ничего из этого не публикуется как релиз.

## Требования
1. **Несовместимость инструментов видна явно.**
   - Workflow `apple-ci.yml` не требует секретов и не публикует релизы. `release.yml` не меняется.
   - Несовместимость Xcode и workload выводится явно: `blocked: toolchain mismatch` с версиями.
   - Job с такой несовместимостью падает, но не блокирует PR. Зелёным он не бывает.
2. **Матрица сборок.**

   | Приложение | Цель | Команда (уточняет 244/245) | Bundle id |
   |---|---|---|---|
   | MAUI | iOS Simulator | `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-ios -c Debug -r iossimulator-arm64 -p:EnableCodeSigning=false` | `com.eliseev.imagegen` |
   | MAUI | Mac Catalyst | `… -f net10.0-maccatalyst -c Debug -r maccatalyst-arm64 -p:EnableCodeSigning=false` | `com.eliseev.imagegen` |
   | Avalonia | iOS Simulator | `dotnet build ImageGen/ImageGen.Avalonia.iOS/ImageGen.Avalonia.iOS.csproj -c Debug -r iossimulator-arm64` | `com.eliseev.imagegen.avalonia` |
   | Avalonia | macOS | `dotnet publish ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj -c Release -r osx-arm64 --self-contained`, затем упаковка в `.app` | `com.eliseev.imagegen.avalonia` |

   Отдельные jobs под x64 не нужны. Если понадобятся, их добавят не смешивая с arm64.
3. **Артефакты.**
   - `.app` упаковываются в `tar.gz`, чтобы сохранились права.
   - Артефакты подписаны пометкой «только CI», загружаются при любом исходе и хранятся 14 дней.
   - Неподписанные `.app` не годятся для устройств и для распространения.
4. **Запуск в iOS Simulator.**
   - Новый iPhone создаётся из доступных устройств и среды выполнения iOS. UDID ищется динамически.
   - Шаги: `boot`, ожидание `bootstatus` (с тайм-аутом), установка, запуск. Затем проверяется, что процесс жив через 10 с и что нет отчёта о падении.
   - Сохраняются скриншот, лог запуска, лог системы симулятора и отчёты о падениях.
   - Симулятор удаляется в `always()`.
5. **UI-тесты iOS.**
   - Стек: NUnit + Allure + Appium 3.9.0 + `xcuitest@12.22.8`.
   - Тесты идут последовательно, на один симулятор; данные изолируются переустановкой.
   - Повторы не скрывают нестабильность: повторных запусков нет.
   - Папка теста: `reports/ios/<maui|avalonia>/<Класс>.<Тест>/`. В ней `recording.mp4`, при падении — `screenshot.png` и `ui.xml`, а также `appium.log`, `simulator.log` и `metadata.json`.
6. **Сценарии.**
   - Smoke на каждое iOS-приложение: запуск и экран первого запуска; ввод и навигация; возврат или отмена; перезапуск без потери состояния.
   - Генерация — только через мок по HTTPS из задачи 243. Настоящие платные API запрещены.
7. **macOS.** Сначала проверка GUI: сессия, содержательный скриншот, права Accessibility. Если проверка не прошла — ошибка инфраструктуры с доказательствами, а не зелёный тест.
8. **Секреты.** В скриншотах, видео и логах нет настоящих ключей. Используются только фиктивные ключи из ADR 0029.

## Критерии готовности
- Windows и Android из ADR 0029 работают без регрессии.
- В Actions нет сертификатов, профилей и новых обязательных секретов Apple.
- У каждой из четырёх сборок свой статус. Неготовая голова Avalonia iOS не выдаётся за успех.
- Несовместимость Xcode и .NET даёт явное `blocked` с версиями.
- Оба iOS-приложения ставятся и открываются в Simulator. Доказательства сохраняются и при упавшем job.
- UI-тесты iOS дают NUnit, TRX, Allure, видео, дерево UI и скриншоты.
- UI-тесты macOS не считаются успешными, пока не прошла проверка GUI.
- Нет публикации IPA или DMG.

## Ограничения и вне рамок
- Подпись, нотаризация и установка на iPhone — вне рамок.
- Avalonia Android E2E — не здесь, см. ADR 0029.
- Требование дополнения «unit-tests и Avalonia Headless на PR» уже выполнено: jobs `core` и `avalonia` в `build.yml`.

## Тесты
- Сами E2E-проекты запускает CI.
- Скрипты упаковки `.app` и разбора `simctl list -j` получают проверки в shell, на файлах-образцах в `tmp/` (задачи 244, 245).
