---
id: 244
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-07T11:36:10Z
created: 2026-11-07
---

# Apple CI: проверка инструментов (preflight) и таблица совместимости Xcode/.NET

## Описание
Этап P0 дополнения Apple (ADR 0030, `docs/specs/apple-ci-e2e.md`). На раннерах GitHub (`macos-26-arm64`, образ 20261026) Xcode до 26.6, а выпущенные workload iOS/Mac Catalyst .NET 10 (27.0.x) требуют Xcode 27 (блог .NET 2026-10-19; в `docs/knowledge/dev-environment.md` — Mac Catalyst SDK 27.0 требовал Xcode 27.1). Нужно на раннере снять инвентарь и найти набор workload, который собирает MAUI iOS и Mac Catalyst с установленным Xcode, — или доказать, что такого нет.

Зависит от: —
Среда: GitHub Actions (`macos-26`, при наличии ещё `macos-latest` — записать, на что он указывает); локально — YAML, скрипты и `scripts/check.sh`.

## Критерии готовности
- Новый `.github/workflows/apple-ci.yml`: `on: workflow_dispatch` и `pull_request` с `paths: ['.github/workflows/apple-ci.yml', 'ImageGen/ImageGen.E2E.Common/ci/apple-*']` (на этом этапе), `permissions: contents: read`, `concurrency` по ref с отменой. Секретов нет.
- Job `apple-preflight` (`runs-on: macos-26`, `timeout-minutes: 60`), скрипт `ImageGen/ImageGen.E2E.Common/ci/apple-preflight.sh` пишет в `reports/apple-preflight/`:
  1. `inventory.txt`: `sw_vers`, `uname -m`, `xcodebuild -version`, `xcode-select -p`, `ls /Applications | grep Xcode`, `xcrun simctl list runtimes`, `dotnet --info`, `dotnet workload list`; `devices.json` — `xcrun simctl list -j devices available`, `runtimes.json` — `xcrun simctl list -j runtimes`, `devicetypes.json` — `xcrun simctl list -j devicetypes` (нужны задаче 247).
  2. Попытка A (по умолчанию): `actions/setup-dotnet` 10.0.x, `dotnet workload install maui` (целиком, не `maui-maccatalyst` — `NETSDK1147`, см. `dev-environment.md`), `dotnet build ImageGen/ImageGen.Maui/ImageGen.Maui.csproj -f net10.0-ios -c Debug -r iossimulator-arm64 -p:EnableCodeSigning=false` и то же с `-f net10.0-maccatalyst -r maccatalyst-arm64`; результат и последние 80 строк вывода — в `attempt-default.txt`.
  3. Попытка B (совместимый набор): найти в https://github.com/dotnet/macios/releases последний выпуск `dotnet-10.0.1xx-xcode26.6-*` (или для старшего Xcode на раннере) и версию набора workload .NET SDK, в которой он поставляется (страница выпуска или https://github.com/dotnet/workload-versions); `sudo xcode-select -s /Applications/Xcode_26.6.app`; `dotnet workload install maui --version <набор>`; те же две сборки → `attempt-pinned.txt`. Если набор найти не удалось — записать, что искали и где.
  4. Итог `summary.json`: `xcode`, `dotnetSdk`, `workloadSet`, `mauiIos` / `mauiCatalyst` — `ok` или `blocked: toolchain mismatch (<текст ошибки>)`, время шагов.
- Скрипт не прячет ошибки: каждая попытка ловится и пишется в файл, job падает, только если инвентарь не снялся. Артефакт `apple-preflight` при любом исходе (`if: always()`, 14 дней).
- Прогнать workflow на ветке задачи (`workflow_dispatch`; если нет прав — попросить владельца в «Комментариях»), записать в новый `docs/knowledge/apple-ci.md` таблицу: раннер, Xcode, SDK, набор workload, MAUI iOS, MAUI Catalyst, время; и вывод — какой набор закреплять в 245 или что сборки iOS/Catalyst сейчас «blocked». Ссылку на прогон.
- `docs/index.md`: строка про `knowledge/apple-ci.md`. ADR 0030: строка «Проверено задачей 244» с итогом.

## Анализ
Справочно: дополнение владельца — `docs/specs/ui-e2e-original/MAUI_Avalonia_Apple_CI_E2E_Addendum.md`; при расхождении действуют ADR 0030 и `docs/specs/apple-ci-e2e.md`.
`ImageGen/ImageGen.Maui/ImageGen.Maui.csproj` (строки 4–6: `net10.0-ios`/`net10.0-maccatalyst` вне Linux), `.github/workflows/release.yml` (комментарий ≈290), `docs/knowledge/dev-environment.md` (≈81–82), ADR 0030.

## План
1. Скрипт preflight (проверить синтаксис `bash -n` локально) и workflow.
2. Прогон, разбор артефакта; до 3 попыток правки скрипта.
3. Таблица и вывод в документации.

Файлы: `.github/workflows/apple-ci.yml` (новый), `ImageGen/ImageGen.E2E.Common/ci/apple-preflight.sh` (новый), `docs/knowledge/apple-ci.md` (новый), `docs/decisions/0030-apple-ci-e2e.md`, `docs/index.md`.

### Проверка
```
bash -n ImageGen/ImageGen.E2E.Common/ci/apple-preflight.sh
bash scripts/check.sh
# в GitHub Actions: workflow apple-ci, job apple-preflight, артефакт
```

### Порядок исполнения (для Luna)
- Сначала прочитай только файлы из строки «Файлы» и названные тесты; сигнатуры сверяй с текущим `main`.
- Шаги строго по порядку (ub-mode): падающий тест → запуск, убедиться, что он падает именно по описанной причине → минимальный фикс → тест зелёный.
- Не меняй файлы вне строки «Файлы», не переименовывай существующие члены сверх плана, не чини соседние находки — они в отдельных задачах.
- Если код расходится с планом (нет такого метода, другая сигнатура, тест уже есть), не импровизируй: остановись и запиши вопрос в «Комментарии».
- Команды `dotnet` выполняй по одной, не параллельно. После каждого изменённого проекта — `dotnet format <проект> --verify-no-changes`.
- В «Отчёте»: что изменено, команды и результаты, отличия от плана.

## Отчёт

## Комментарии

### 2026-11-07, claude-audit
Создано по дополнению Apple: этап P0. Проверено 2026-11-07: macos-26 — Xcode до 26.6; workload iOS/Catalyst .NET 10 требует Xcode 27. План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
Дополнение владельца «MAUI + Avalonia — Apple CI/E2E» (оригинал — `docs/specs/ui-e2e-original/`). План одобрен ранее данным поручением «заапрувь все таски которые будут». Новый workflow `apple-ci.yml` по этому дополнению владелец попросил явно (ADR 0030).
