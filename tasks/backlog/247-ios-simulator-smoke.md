---
id: 247
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-07T12:11:27Z
created: 2026-11-07
---

# iOS Simulator: smoke-запуск MAUI и Avalonia с доказательствами

## Описание
Этап P3 дополнения Apple (ADR 0030, `docs/specs/apple-ci-e2e.md`, требование 4): для каждого iOS-приложения в `apple-ci.yml` — создать симулятор, поставить `.app` из артефакта, запустить, убедиться, что процесс жив и не упал, сохранить скриншот и логи.

Зависит от: 245 (артефакты `maui-ios-sim`, `avalonia-ios-sim`), 246.
Среда: GitHub Actions `macos-26`.

## Критерии готовности
- Скрипт `ImageGen/ImageGen.E2E.Common/ci/apple-sim-smoke.sh <app.tar.gz> <bundle-id> <out-dir>`:
  1. Выбор среды: последняя iOS из `xcrun simctl list -j runtimes` с `isAvailable: true`; тип устройства — первый `iPhone` из `xcrun simctl list -j devicetypes`, поддерживаемый этой средой (разбор через `plutil`/`python3` — `python3` есть на образе; не зашивать модель и версию). `UDID=$(xcrun simctl create e2e-<run_id> <type> <runtime>)`.
  2. `xcrun simctl boot`, затем `timeout 300 xcrun simctl bootstatus "$UDID" -b` (нет `timeout` на macOS — `gtimeout` из coreutils через `brew install coreutils`, или свой цикл с `sleep`; выбрать и записать).
  3. Распаковать артефакт, найти `.app`, проверить `CFBundleIdentifier` и что `lipo -archs` содержит `arm64`.
  4. `xcrun simctl install`, `xcrun simctl launch --console-pty "$UDID" <bundle-id> > launch.log 2>&1 &`; через 15 с — `xcrun simctl spawn "$UDID" launchctl list | grep <bundle-id>` (процесс жив), `xcrun simctl io "$UDID" screenshot screenshot.png`; ещё через 15 с — второй скриншот `screenshot-30s.png`.
  5. Логи: `xcrun simctl spawn "$UDID" log show --last 5m --predicate 'process CONTAINS "ImageGen" OR process CONTAINS "Omnilite"' > simulator.log`; отчёты о падении — `~/Library/Logs/DiagnosticReports/*` и папка симулятора `~/Library/Developer/CoreSimulator/Devices/$UDID/data/Library/Logs/CrashReporter` → `crash/`.
  6. Итог `summary.json`: runtime, deviceType, launched, aliveAfter15s, crashReports (число). Выход 1, если процесс не жив или есть отчёт о падении.
  Удаление симулятора — `trap` на выход: `xcrun simctl shutdown` + `delete`.
- Jobs `smoke-ios-maui` и `smoke-ios-avalonia` (`macos-26`, `needs` соответствующей сборки, `timeout-minutes: 30`, `continue-on-error: true` пока сборка «blocked»): скачать артефакт, скрипт, загрузить `reports/ios-smoke/<maui|avalonia>/` при любом исходе. На этом этапе — `workflow_dispatch` и `schedule` (ночью, `cron: '17 2 * * *'`), не на каждый PR.
- Три прогона подряд зелёные для каждого приложения (или записанная причина) — ссылки в «Отчёте». Предложение включить на PR — строкой в `docs/status.md` (вопрос владельцу), не самой задачей.
- Один успешный запуск не считается проверкой UI — так и написать в `docs/knowledge/apple-ci.md` рядом с результатами.

## Анализ
Справочно: дополнение владельца — `docs/specs/ui-e2e-original/MAUI_Avalonia_Apple_CI_E2E_Addendum.md`; при расхождении действуют ADR 0030 и `docs/specs/apple-ci-e2e.md`.
ADR 0030, `docs/specs/apple-ci-e2e.md`, `docs/knowledge/apple-ci.md` (244/245), `man simctl` (`xcrun simctl help <команда>` на раннере — вывод приложить к первому прогону).

## План
1. Скрипт: выбор среды и устройства — проверить на сохранённом `devices.json`/`runtimes.json` из артефакта 244 локально (`python3`), затем остальные шаги в CI.
2. Jobs, расписание, артефакты.
3. Три прогона, запись.

Файлы: `ImageGen/ImageGen.E2E.Common/ci/apple-sim-smoke.sh` (новый), `ImageGen/ImageGen.E2E.Common/ci/apple-sim-pick.py` (новый, выбор среды/устройства), `.github/workflows/apple-ci.yml`, `docs/knowledge/apple-ci.md`, `docs/status.md`.

### Проверка
```
bash -n ImageGen/ImageGen.E2E.Common/ci/apple-sim-smoke.sh
python3 -I ImageGen/ImageGen.E2E.Common/ci/apple-sim-pick.py tmp/apple-preflight/runtimes.json tmp/apple-preflight/devicetypes.json
bash scripts/check.sh
# в GitHub Actions: smoke-ios-maui и smoke-ios-avalonia, артефакты
```

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы» и названные тесты; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): падающий тест → запуск, убедиться, что он падает именно по описанной причине → минимальный фикс → тест зелёный.
- Не меняй файлы вне строки «Файлы», не переименовывай существующие члены сверх плана, не чини соседние находки — они в отдельных задачах.
- Если код расходится с планом (нет такого метода, другая сигнатура, тест уже есть), не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: что изменено, команды и результаты, отличия от плана.

## Отчёт

### 2026-11-07, codex-gpt6 (в работе)
- `apple-sim-pick.py` выбирает последнюю доступную iOS runtime и первый поддерживаемый ею iPhone, без зашитых версии/модели. Проверка на сохранённом инвентаре `tmp/apple-preflight-run4/` выбрала iOS 26.5 / iPhone 17 Pro.
- `apple-sim-smoke.sh` проверяет bundle id и arm64, создаёт/загружает/запускает приложение, проверяет процесс через 15 секунд, сохраняет два скриншота и логи, собирает crash reports и удаляет симулятор через `trap`.
- Добавлены отдельные schedule/dispatch jobs для MAUI и Avalonia; оба артефакта публикуются при любом исходе. На pull request smoke jobs пока пропущены; вопрос о включении после трёх ночных прогонов добавлен в `docs/status.md`.
- Локально: `python3 -I .../apple-sim-pick.py` — успешно; `bash -n .../apple-sim-smoke.sh` и YAML parse — успешно; shell smoke и GitHub-прогоны ещё ожидаются.
- После merge run [42381579325](https://github.com/r-eliseev/studious-goggles/actions/runs/42381579325) автоматически выполнил оба simulator smoke. Оба crash report указывают `CODESIGNING`, `Invalid Page`, `SIGKILL (Code Signature Invalid)`; MAUI был виден живым через 15 секунд, Avalonia — нет. Simulator apps собирались с `EnableCodeSigning=false` и устанавливались без последующей подписи. Перед установкой добавлена ad-hoc подпись и строгая проверка `codesign`; CI-подтверждение ожидается.
- Run [42386890196](https://github.com/r-eliseev/studious-goggles/actions/runs/42386890196) проверил обе сборочные стратегии: подпись на этапе сборки и ad-hoc переподпись готового `.app` не устранили падение. В обоих crash report faulting image — системный `dyld_sim`, который падает при разборе Mach-O; причина не доказана как проблема подписи приложения. Workflow возвращён к штатной unsigned simulator-сборке .NET; записан блокер Apple runtime, повторная проверка ожидается.
- Run [42392573546](https://github.com/r-eliseev/studious-goggles/actions/runs/42392573546) проверил MAUI и Avalonia на iOS 26.4: оба снова получили `CODESIGNING: Invalid Page` внутри `dyld_sim`, как и на iOS 26.5. Первая попытка согласованного Xcode 26.4/.NET 10.0.203 не дошла до сборки: `Xcode_26.4.app` на runner не содержит MacOSX SDK, `actool` отсутствует. Во втором прогоне Xcode 26.4.1/.NET 10.0.203 обе сборки прошли, но приложения завершились с тем же crash. Тест SDK signing после этого остановился при сборке с ошибкой `No valid iOS code signing keys found in keychain`; добавлен явный ad-hoc signing identity `CodesignKey=-`. Следующий прогон проверит подписанное SDK приложение без переподписи в smoke. Похожий открытый отчёт .NET MAUI: https://github.com/dotnet/maui/issues/35792.

## Комментарии

### 2026-11-07, claude-audit
Создано по дополнению Apple: этап P3. План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
Дополнение владельца «MAUI + Avalonia — Apple CI/E2E» (оригинал — `docs/specs/ui-e2e-original/`). План одобрен ранее данным поручением «заапрувь все таски которые будут». Новый workflow `apple-ci.yml` по этому дополнению владелец попросил явно (ADR 0030).

### 2026-11-07, codex-gpt6
В run 42381579325 оба iOS smoke jobs запускались автоматически после merge, но приложения завершались с `SIGKILL (Code Signature Invalid)`, `CODESIGNING: Invalid Page`. Приложения собираются unsigned для simulator. Добавил ad-hoc signing извлечённого `.app` с `codesign --verify --deep --strict` до `simctl install`; проверка на hosted macOS ожидается.

### 2026-11-07, claude-audit (разбор падения Avalonia на `main`)
Run [42398374908](https://github.com/r-eliseev/studious-goggles/actions/runs/42398374908): после перехода на подпись SDK (`-p:CodesignKey=-`) `smoke-ios-maui` зелёный, а `smoke-ios-avalonia` — нет. Причина уже не в подписи. В `launch.log` артефакта `ios-smoke-avalonia`: `ObjCRuntime.RuntimeException: Unable to create an instance of the type Avalonia.iOS.AvaloniaAppDelegate\`1` → `System.MemberAccessException: Cannot access member` в `RuntimeHelpers.GetUninitializedObject`, затем `SIGABRT`. Отчёта о падении нет, поэтому `crashReports=0`: процесс умирает от необработанного управляемого исключения.

Рантайм пытается создать открытый generic-базовый класс `AvaloniaAppDelegate<TApp>` вместо нашего `AppDelegate`. `AppDelegate.cs` и `Main.cs` совпадают с официальным шаблоном Avalonia 12 и с `samples/ControlCatalog.iOS`. Гипотеза: регистратор Objective-C по умолчанию в Debug-сборке для симулятора не сопоставляет класс `AppDelegate` с нашим типом. Проверка: сборка с `-p:Registrar=static` (ветка `review/2026-11-01-windows-tasks-close-ghnt1m`, коммит `d802a0e`). Итог допишу сюда.

Заодно: `apple-sim-smoke.sh` сейчас не показывает причину в выводе job. Стоит печатать последние 30 строк `launch.log`, если процесс не жив.

### 2026-11-07, claude-audit (итог эксперимента со статическим регистратором)
Run [42402076497](https://github.com/r-eliseev/studious-goggles/actions/runs/42402076497) (`-p:Registrar=static`): ошибка та же, но сообщение точнее: `Cannot construct an instance of the type 'Avalonia.iOS.AvaloniaAppDelegate\`1' from Objective-C because the type is generic`.

В собранном `ImageGen.Avalonia` есть оба класса, `_OBJC_CLASS_$_AppDelegate` и `_OBJC_CLASS_$_Avalonia_iOS_AvaloniaAppDelegate_1` (проверено `llvm-nm` по артефакту `avalonia-ios-sim`). Значит, Objective-C-класс `AppDelegate` создан, но рантайм не сопоставляет его с нашим управляемым типом и доходит до generic-базы. Регистратор поэтому ни при чём.

Отличие от MAUI, которое запускается: сборка Avalonia iOS идёт с `-p:TrimMode=copy`. Через шаги ILLink в .NET iOS проходит и регистрация. Следующая проверка — без `TrimMode=copy`, а предупреждения линкера не как ошибки (`-p:ILLinkTreatWarningsAsErrors=false`), коммит `ea773d0`.
