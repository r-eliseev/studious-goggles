> Дополнение владельца к спецификации UI-тестов (2026-11-07), без правок, справочно для исполнителя задач 244–250. Действуют поправки из ADR 0030 и `docs/specs/apple-ci-e2e.md`: jobs `unit-tests` и `avalonia-headless` уже есть в `build.yml` (`core`, `avalonia`); на раннерах GitHub сейчас Xcode до 26.6, а workload iOS/Mac Catalyst .NET 10 требует Xcode 27, поэтому сборки iOS/Catalyst начинаются с проверки совместимости; для Appium 3 — `xcuitest@12.22.8` и `mac2@4.6.1`. Папку `docs/specs/ui-e2e-original/` удалить при архивировании задач 238–250.

К документу «UI E2E тестирование .NET MAUI — Windows + Android ·
GitHub-hosted runners · Видео, логи и Allure» (07.11.2026).

Репозиторий: https://github.com/r-eliseev/studious-goggles \| Статус:
спецификация к реализации; не означает, что workflows уже внедрены.

# A1. Область действия и принцип аддитивности

Это отдельное дополнение. Исходные разделы 1–12, существующие
Windows/Android jobs, их артефакты, NUnit, Allure, video/log contract и
требования безопасности остаются в силе. Новые Apple jobs не заменяют и
не изменяют логику существующих релизов. При противоречии по
Apple-платформам применять это дополнение; по Windows/Android — исходную
спецификацию.

Цель первого этапа: без Apple ID, Apple Developer Program, сертификатов,
provisioning profiles и приватных GitHub Secrets получать четыре
компиляционные проверки, тесты логики, headless UI и smoke-запуск в
симуляторе там, где это возможно. Неподписанные артефакты не считать
пригодными для установки на физические iPhone или публичной дистрибуции.

# A2. Платформенная матрица

| Оболочка | Цель  | Рантайм / формат CI                     | Особенность                                                         |
|----------|-------|-----------------------------------------|---------------------------------------------------------------------|
| MAUI     | iOS   | net10.0-ios / iossimulator-arm64        | Simulator .app без distribution signing                             |
| MAUI     | macOS | net10.0-maccatalyst / maccatalyst-arm64 | Mac Catalyst, не native AppKit                                      |
| Avalonia | iOS   | Avalonia iOS host / iossimulator-arm64  | Добавить отдельный iOS host, не переиспользовать Desktop entrypoint |
| Avalonia | macOS | Avalonia desktop / osx-arm64            | Self-contained .app, без Developer ID                               |

Фактические TFM, имена проектов, bundle IDs и пути определить по csproj
на момент реализации; таблица — целевая конфигурация. При необходимости
x64 для macOS добавить отдельные jobs, не смешивать с Apple Silicon.

# A3. Новая архитектура GitHub Actions

Добавить независимый workflow .github/workflows/apple-ci.yml с событиями
pull_request, push (main) и workflow_dispatch; permissions: contents:
read. Он не должен публиковать GitHub Release и не должен требовать
GitHub Secrets. Ресурсозатратные E2E jobs по умолчанию запускать по
workflow_dispatch / расписанию, а на PR — только после доказанной
стабильности.

| Job                       | Runner        | Что проверяет                                      | Статус на старте                         |
|---------------------------|---------------|----------------------------------------------------|------------------------------------------|
| apple-preflight           | macOS hosted  | Xcode, xcrun, SDK, .NET, iOS runtime, capabilities | PR required после стабилизации           |
| unit-tests                | ubuntu-latest | Общие сервисы, ViewModels, без UI                  | PR required                              |
| avalonia-headless         | ubuntu-latest | Controls, bindings, navigation, input              | PR required                              |
| build-maui-ios            | macOS hosted  | Unsigned simulator .app                            | PR required после preflight              |
| build-maui-catalyst       | macOS hosted  | Unsigned Catalyst .app                             | PR required после preflight              |
| build-avalonia-ios        | macOS hosted  | iOS host + simulator .app                          | PR required после создания host          |
| build-avalonia-macos      | macOS hosted  | Desktop publish + .app packaging                   | PR required                              |
| smoke-ios-{maui,avalonia} | macOS hosted  | Boot → install → launch → evidence                 | Nightly/manual → PR                      |
| e2e-ios-{maui,avalonia}   | macOS hosted  | Appium XCUITest + NUnit + evidence                 | Manual/nightly                           |
| smoke-macos / e2e-macos   | macOS hosted  | Запуск .app; затем platform UI automation          | Preflight отдельно; initially non-gating |
| apple-report              | ubuntu-latest | Allure HTML + aggregate                            | always()                                 |

# A4. Preflight: mandatory fail-fast

- Вывести sw_vers, uname -m, xcodebuild -version, xcode-select -p, xcrun
  simctl list -j devices available, xcrun simctl list runtimes, dotnet
  --info, dotnet workload list, список доступных SDK.

- Сопоставить версию установленного .NET workload с Xcode.
  Несовместимость фиксировать явно: «blocked: toolchain mismatch», а не
  маскировать как падение кода приложения.

- Убедиться, что доступен iPhone simulator на выбранной архитектуре;
  обнаруживать device UDID динамически, не hardcode модель/версию iOS.

- Установить timeout для bootstatus и каждого теста; прекращать зависший
  simulator job с понятной диагностикой.

- Для macOS оконной автоматизации провести отдельный preflight: GUI
  session, Accessibility permissions, возможность найти окно/получить
  содержательный screenshot. При провале не маркировать UI-тесты
  успешными.

Пример диагностических команд (не полный workflow):

xcodebuild -version  
xcrun simctl list devices available  
xcrun simctl list runtimes  
dotnet --info  
dotnet workload list

# A5. Компиляция без подписи

Build job должен использовать SDK, соответствующий репозиторию (в
global.json сейчас задаются настройки test runner, поэтому конкретную
версию SDK закрепить отдельно/в workflow). Команды — пример; правильные
project paths, TFM и свойства проверить по файлам решения и доступному
.NET SDK.

dotnet build \<maui.csproj\> -f net10.0-ios -c Debug \\  
-r iossimulator-arm64 -p:EnableCodeSigning=false  
  
dotnet build \<maui.csproj\> -f net10.0-maccatalyst -c Debug \\  
-r maccatalyst-arm64 -p:EnableCodeSigning=false  
  
dotnet publish \<avalonia-desktop.csproj\> -c Release \\  
-r osx-arm64 --self-contained true

Для Avalonia iOS создать host-проект по платформенному шаблону,
подключить общее Avalonia Application и использовать отдельный simulator
RID. Не переносить в него Avalonia.Desktop/классический desktop
lifetime. Команду сборки определить после создания host.

Не смешивать unsigned simulator .app с device-ready IPA. Для MAUI Mac
Catalyst проверять, что артефакт действительно запускается на hosted
macOS; запуск приложения в CI может ограничиваться GUI-сессией.

# A6. iOS Simulator smoke: два независимых приложения

1.  Создать/выбрать iPhone Simulator из доступных, запустить xcrun
    simctl boot и дождаться bootstatus с timeout.

2.  Скачать соответствующий simulator .app как artifact, проверить его
    структуру, CFBundleIdentifier и целевую архитектуру.

3.  Выполнить xcrun simctl install \<UDID\> \<App.app\> и xcrun simctl
    launch \<UDID\> \<BundleId\>.

4.  Проверить процесс и crash report; сохранить screenshot,
    simctl/launch logs, syslog/crash diagnostics. Один успешный launch
    сам по себе не доказывает работоспособность UI.

5.  Shutdown/delete временный симулятор в always(); сохранять evidence
    даже при ошибке.

xcrun simctl boot "\$UDID"  
xcrun simctl bootstatus "\$UDID" -b  
xcrun simctl install "\$UDID" "path/to/App.app"  
xcrun simctl launch "\$UDID" "com.example.app"  
xcrun simctl io "\$UDID" screenshot reports/ios/smoke.png

# A7. UI E2E и диагностика: расширение исходного контракта

Сохранить стэк NUnit + Appium + Allure из исходных разделов 5–8.
Добавить iOS Appium XCUITest driver. Для MAUI и Avalonia применять общие
сценарии/контракты тестовых IDs, но платформенные локаторы определять по
реальному accessibility tree (AutomationId ≠ гарантированно
accessibility id).

- Минимальные сценарии на каждое iOS приложение: boot+launch, главный
  экран доступен, ввод/навигация, возврат/отмена, восстановление после
  перезапуска. Сетевая генерация не является обязательной частью smoke;
  для E2E использовать fixture/mock backend без платных API.

- Каждый тест: отдельная папка reports/ios/\<shell\>/\<Test.ID\>;
  recording.mp4 (если screen recording поддерживается), screenshot.png
  при ошибке, ui.xml/page-source, appium.log, simulator.log, TRX, Allure
  attachments.

- Для iOS видеозаписи использовать поддерживаемый Appium/XCUITest
  механизм либо xcrun simctl io recordVideo с корректным завершением;
  различия .mp4/.mov нормализовать при необходимости. Проверять, что
  видео не пустое и воспроизводится.

- Не запускать одновременно несколько E2E с одним simulator:
  последовательное выполнение и отдельные UDID / изоляция данных. Повтор
  тестов не должен скрывать flaky результат.

- macOS UI E2E: сначала проверить доступность GUI/Accessibility на
  hosted runner, затем выбрать Appium Mac2 / подходящий инструмент. Если
  окружение недоступно — инфраструктурная ошибка с evidence, не green
  test.

# A8. Артефакты и отчёты

| Artifact                          | Что хранит                                                        |
|-----------------------------------|-------------------------------------------------------------------|
| apple-preflight                   | Toolchain inventory, simulator inventory, diagnostic logs         |
| maui-ios-sim / avalonia-ios-sim   | Неподписанные simulator .app (в ZIP или tar для сохранения прав)  |
| maui-maccatalyst / avalonia-macos | Unsigned .app (только CI/диагностика)                             |
| ios-e2e-maui / ios-e2e-avalonia   | TRX, per-test video, PNG, XML, crash/console logs, Allure results |
| macos-e2e-\*                      | Preflight evidence, UI results where available                    |
| apple-allure-html                 | Объединённый отчёт без смешивания платформенных labels            |

Во всех test jobs: upload-artifact при if: always(), retention-days: 14
(как в исходной спецификации). Report job — always() и устойчивость к
отсутствующим artifacts. Не загружать пользовательские токены, приватные
изображения, passwords и API keys в screenshots, XML, видео или логи.

# A9. Тестовые данные и условия безопасности

- Строго офлайновые smoke/головные сценарии или отдельный mock/fake
  provider. Приложение не должно требовать OpenRouter/fal.ai/Together
  ключей ради успешного CI-теста.

- Если всё же понадобятся реальные сервисы — отдельная будущая
  спецификация, isolated secrets + budget/quotas; в настоящем этапе
  запрещать реальные платные запросы.

- Уникальные Bundle IDs для MAUI и Avalonia iOS и для двух macOS
  оболочек; независимые каталоги данных и настройки. Идентификаторы
  выбрать согласованно с текущим Android coexistence.

- Не делать unattended release, signing или notarization этим workflow.
  Existing release workflow оставить без изменений, кроме возможной
  передачи artifact при явно согласованном расширении.

# A10. План внедрения без конфликтов с текущей работой

6.  P0: снять инвентаризацию проектов/TFM и инструментов; собрать
    preflight на hosted macOS. Результат: таблица поддерживаемых
    комбинаций Xcode/.NET.

7.  P1: зелёные unit-tests + Avalonia.Headless на Ubuntu; добавить общие
    UI TestIds, сохранив IDs из исходной спецификации при их наличии.

8.  P2: четыре Apple compile jobs. Для Avalonia iOS создать минимальный
    host, подключающий существующую UI/DI структуру; доказать
    компиляцию.

9.  P3: iOS smoke через simctl для обоих apps; сохранять
    screenshot/launch/crash evidence; стабильность 3 последовательных
    прогонов.

10. P4: Appium XCUITest, NUnit, video + Allure для обоих iOS apps, 3–5
    стабильных сценариев; затем PR gating.

11. P5: macOS GUI preflight, запуск/скриншот двух .app, по результату —
    GUI automation. Проблемы hosted runner оформить как blocker и не
    обещать green E2E.

# A11. Acceptance criteria

- [ ] Windows/Android из исходного документа продолжают работать без
  регрессии.

- [ ] В Actions нет Apple сертификатов, профилей, Apple ID, ключей и
  других новых обязательных secrets.

- [ ] Каждая из четырёх платформ имеет отдельный compile status;
  неготовый Avalonia iOS host не маскируется успехом.

- [ ] При несовместимости Xcode/.NET возникает явный blocker с версиями,
  а не ложный pass.

- [ ] Оба iOS приложения устанавливаются и открываются в iOS Simulator;
  evidence сохраняется после failed job.

- [ ] Unit + Avalonia Headless проходят на PR; iOS E2E предоставляет
  NUnit/TRX/Allure, видео, UI tree и скриншоты.

- [ ] На macOS интерактивный UI-тест не считается успешным до
  прохождения preflight.

- [ ] Отчёты и видео не содержат реальных токенов или пользовательских
  секретов.

- [ ] Нет публикации IPA/DMG как готового релиза: только неподписанные
  CI artifacts.

# A12. Зависимости, открытые решения и референсы

До реализации определить актуальные csproj пути, версию SDK в CI,
валидные macOS runners/Xcode, совместимый iOS Simulator runtime, Bundle
IDs и уровень тестирования Catalyst. Эти параметры нельзя достоверно
назначить только по исходной Windows/Android спецификации.

Официальные ориентиры:
https://docs.github.com/en/actions/using-github-hosted-runners/using-github-hosted-runners
; https://learn.microsoft.com/en-us/dotnet/maui/ ;
https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform
; https://appium.io/docs/en/latest/ ;
https://developer.apple.com/documentation/xcode/running-your-app-in-simulator-or-on-a-device

Связь с исходным документом: A3 ↔ §2, §4, §9; A7–A8 ↔ §5–8; A11 ↔ §10;
A9–A10 ↔ §11. Это приложение не переписывает исходную спецификацию.
