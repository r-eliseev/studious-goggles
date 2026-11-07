# 0030. Apple-платформы в CI: сборки без подписи, iOS Simulator и macOS

Статус: принято
Дата: 2026-11-07

## Контекст
Владелец прислал дополнение «MAUI + Avalonia — Apple CI/E2E» к спецификации UI-тестов (ADR 0029) и попросил перепроверить его и распланировать. Цель: без Apple ID, сертификатов, профилей и секретов получить на раннерах GitHub четыре сборки:
- MAUI iOS;
- MAUI Mac Catalyst;
- Avalonia iOS;
- Avalonia macOS.

Затем — запуск в iOS Simulator и UI-тесты. Windows и Android из ADR 0029 не меняются.

Факты, проверенные 2026-11-07:
- **Xcode на раннерах.** На `macos-26-arm64` (образ 20261026.0455.2) установлены Xcode 26.6 (по умолчанию), 26.5, 26.4.1, 26.3, 26.2, 26.1.1 и 26.0.1. Симуляторы iOS: 26.2, 26.4, 26.5. .NET SDK: до 10.0.403.
- **Workload Apple.** Блог .NET от 2026-10-19 пишет, что выпущенные workload iOS и Mac Catalyst .NET 10 (27.0.10722) требуют Xcode 27.0. В `docs/knowledge/dev-environment.md` записано, что на 2026-10-29 Mac Catalyst SDK 27.0 требовал Xcode 27.1. Из-за этого сборка Mac Catalyst в релизе удалена (`release.yml`, комментарий у строки 290; `v0.1.3` упал на ней).
- **Вывод.** С workload по умолчанию сборка MAUI iOS и Mac Catalyst на раннерах GitHub не пройдёт. Avalonia iOS тоже собирается workload `ios`, поэтому ограничение то же. Avalonia macOS (`osx-arm64`, обычный `net10.0`) от Xcode не зависит и уже собирается в `release.yml`, но как папка, а не как `.app`.
- **Тесты на Ubuntu уже есть.** Jobs `unit-tests` и `avalonia-headless` из дополнения уже существуют: `core` и `avalonia` в `.github/workflows/build.yml`.
- **Драйверы Appium (npm).** `appium-xcuitest-driver` 13.x требует Appium 4 beta; последняя версия для Appium 3 — 12.22.8. `appium-mac2-driver` 4.6.1 работает с Appium 3.
- **Avalonia iOS.**
  - `Avalonia.iOS` есть на nuget.org: 12.1.4 — та же версия, что в проекте.
  - Головы iOS в репозитории нет.
  - В `docs/product.md` Avalonia записана только для Linux desktop и Android. Значит, Avalonia iOS — новое приложение, и решение о нём даёт это дополнение.
- **Разметка Avalonia.** В ней `AutomationProperties.Name` встречается 108 раз, а `AutomationProperties.AutomationId` — ни разу.
- **Стоимость.** Репозиторий публичный, поэтому стандартные раннеры GitHub, включая macOS, для него бесплатны.

## Решение
1. **Отдельный workflow** `.github/workflows/apple-ci.yml`:
   - запускается на `pull_request`, `push` в `main` и `workflow_dispatch`;
   - `permissions: contents: read`;
   - без секретов, ничего не публикует.
   `release.yml` не меняется. Дублировать `unit-tests` и `avalonia-headless` не нужно: их роль выполняют `core` и `avalonia` из `build.yml`.
2. **Сначала проверка инструментов (preflight, задача 244).** Она снимает таблицу комбинаций и ищет набор workload (`dotnet workload install … --version <набор>`), совместимый с Xcode 26.x на раннере. Пример — набор с macios `dotnet-10.0.1xx-xcode26.6-*`. Если такой набор MAUI 10.0.110 собирает, сборки iOS и Catalyst закрепляют его. Если нет, job явно пишет `blocked: toolchain mismatch (Xcode <версии>, workload <версия>)` и падает. Такой job не блокирует PR (`continue-on-error: true`), но и успехом не считается.
3. **Avalonia iOS — новая голова** `ImageGen/ImageGen.Avalonia.iOS`:
   - устроена так же, как Android-голова (ADR 0026): исходники desktop подключаются ссылками, а платформенные службы — свои;
   - bundle id `com.eliseev.imagegen.avalonia`, как у Avalonia Android;
   - собирается только для симулятора и не публикуется.
4. **Avalonia macOS** — `.app`, собранный из `dotnet publish -r osx-arm64 --self-contained` скриптом упаковки. Bundle id `com.eliseev.imagegen.avalonia`, без Developer ID. Подпись ad-hoc делает SDK на Mac (см. `docs/knowledge/dev-environment.md`).
5. **Проверки iOS Simulator** — `xcrun simctl`:
   - устройство создаётся и удаляется на каждый job, UDID ищется динамически;
   - конфиг мока подкладывается в контейнер данных приложения (`simctl get_app_container … data`);
   - корневой сертификат мока ставится через `simctl keychain … add-root-cert` (тот же мок по HTTPS, что в задаче 243);
   - видео пишется через `simctl io recordVideo`.
6. **UI-тесты iOS** — NUnit + Allure, как в ADR 0029. Драйвер — Appium 3.9.0 + `xcuitest@12.22.8`. Один проект `ImageGen.E2E.iOS` для двух приложений: приложение выбирается переменной, сценарии и `TestIds` общие. Локаторы — по реальному дереву accessibility: у Avalonia нужно сначала проверить, во что превращается `AutomationProperties.AutomationId`.
7. **macOS GUI** — сначала preflight: есть ли сессия, даёт ли `screencapture` содержательный кадр, есть ли права Accessibility. Только если он проходит — `mac2@4.6.1`. До этого UI-тесты macOS не считаются успешными.
8. **Отчёт** — job `apple-report`: Allure HTML по Apple-платформам. Подход и скрипт подсчёта — из задачи 242.

## Альтернативы
- **Ждать Xcode 27 на раннерах.** Срок неизвестен; пока сборки iOS и Catalyst были бы просто красными. Проверка с поиском совместимого набора workload даёт результат раньше.
- **Свой macOS-runner.** Противоречит условию «только раннеры GitHub».
- **Отдельные E2E-проекты для MAUI iOS и Avalonia iOS.** Сценарии одинаковые, различается только приложение. Поэтому один проект с выбором приложения.
- **Не делать Avalonia iOS.** Дополнение просит её явно, а сравнение двух оболочек на мобильных — цель ADR 0026.

## Последствия
- CI на PR дольше на время macOS-jobs. Насколько — неизвестно, измерит задача 244.
- Пока Xcode на раннерах не совпадает с workload, статусы iOS и Catalyst будут «blocked». Это видно в PR, а не спрятано.
- Появляется третья голова Avalonia (iOS). Её платформенные службы — Keychain, пути, сохранение картинок, шаринг логов — нужно поддерживать.
- В `docs/product.md` добавляется строка про Avalonia iOS и macOS для CI. Публикации и подписи нет.
