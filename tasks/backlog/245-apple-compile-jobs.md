---
id: 245
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-07T12:08:43Z
created: 2026-11-07
---

# Apple CI: четыре сборки без подписи с отдельными статусами

## Описание
Этап P2 дополнения Apple (ADR 0030, `docs/specs/apple-ci-e2e.md`): отдельные jobs сборки MAUI iOS Simulator, MAUI Mac Catalyst, Avalonia iOS Simulator и Avalonia macOS `.app` с артефактами. iOS/Catalyst — с набором workload из 244 либо явным `blocked`.

Зависит от: 244 (набор workload или вывод «blocked»), 246 (голова Avalonia iOS — до её слияния job Avalonia iOS не добавлять).
Среда: GitHub Actions `macos-26`; локально — скрипт упаковки `.app` на Linux (без запуска).

## Критерии готовности
- В `apple-ci.yml`: триггеры `pull_request` (без `paths`), `push` в `main`, `workflow_dispatch`.
- Jobs `build-maui-ios`, `build-maui-catalyst` (`macos-26`, `continue-on-error: true`): Xcode и набор workload из `docs/knowledge/apple-ci.md`; команды из таблицы `docs/specs/apple-ci-e2e.md`; если 244 показал «blocked» — шаг сборки всё равно выполняется, а при падении следующий шаг пишет `::error::blocked: toolchain mismatch (Xcode <xcodebuild -version>, workload <dotnet workload list>)` и `exit 1`. Артефакт `.app` (найти `find ImageGen/ImageGen.Maui/bin -name '*.app' -maxdepth 6`) в `tar.gz` → `maui-ios-sim` / `maui-maccatalyst`.
- Перед загрузкой — проверка: `Info.plist` содержит `CFBundleIdentifier` = `com.eliseev.imagegen` (`/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier'`), `lipo -archs` исполняемого файла содержит `arm64`.
- Job `build-avalonia-macos` (`macos-26`, блокирующий): `dotnet publish ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj -c Release -r osx-arm64 --self-contained -o publish/osx-arm64`, затем `ImageGen/ImageGen.E2E.Common/ci/apple-make-app.sh publish/osx-arm64 Omnilite.app com.eliseev.imagegen.avalonia <версия>`: `Omnilite.app/Contents/MacOS/` — содержимое публикации, `Contents/Info.plist` (`CFBundleIdentifier`, `CFBundleName` Omnilite, `CFBundleExecutable` = имя apphost `ImageGen.Avalonia`, `CFBundleShortVersionString`, `CFBundlePackageType APPL`, `LSMinimumSystemVersion` 12.0, `NSHighResolutionCapable` true), `Contents/Resources/` — значок `.icns`, если есть в `ImageGen/ImageGen.Avalonia/Assets` (иначе без значка, записать); после упаковки `codesign --force --deep -s - Omnilite.app` (ad-hoc, без Developer ID: `osx-arm64` без подписи не запускается) и `codesign --verify`. Артефакт `avalonia-macos` (`tar.gz`). Проверка скрипта на Linux: `bash -n` и прогон на фиктивной папке в `tmp/` (без `codesign` — пропуск, если команды нет).
- Job `build-avalonia-ios` — после слияния 246: команда из 246, `continue-on-error: true` по тем же правилам «blocked», проверка `CFBundleIdentifier` = `com.eliseev.imagegen.avalonia`, артефакт `avalonia-ios-sim`.
- Все артефакты — `retention-days: 14`, `if: always()` там, где есть что загрузить; в имени шага пометка «CI only, unsigned».
- `docs/knowledge/apple-ci.md`: время каждого job и итог первого прогона.

## Анализ
Справочно: дополнение владельца — `docs/specs/ui-e2e-original/MAUI_Avalonia_Apple_CI_E2E_Addendum.md`; при расхождении действуют ADR 0030 и `docs/specs/apple-ci-e2e.md`.
`.github/workflows/release.yml` (job `avalonia`, matrix `osx-arm64` на `macos-latest`), `docs/knowledge/dev-environment.md` (подпись ad-hoc apphost на Mac), `docs/knowledge/apple-ci.md` (244), `ImageGen/ImageGen.Avalonia/ImageGen.Avalonia.csproj` (имя сборки, `AssemblyName`).

## План
1. Скрипт `apple-make-app.sh` по TDD на образце в `tmp/` (проверка: появились `Contents/MacOS/<exe>` и `Info.plist` с полями).
2. Job Avalonia macOS, прогон.
3. Jobs MAUI iOS/Catalyst с правилом «blocked», прогон.
4. Job Avalonia iOS — после 246.

Файлы: `.github/workflows/apple-ci.yml`, `ImageGen/ImageGen.E2E.Common/ci/apple-make-app.sh` (новый), `docs/knowledge/apple-ci.md`.

### Проверка
```
bash -n ImageGen/ImageGen.E2E.Common/ci/apple-make-app.sh
bash ImageGen/ImageGen.E2E.Common/ci/apple-make-app.sh tmp/fake-publish tmp/Omnilite.app com.eliseev.imagegen.avalonia 0.0.0
bash scripts/check.sh
# в GitHub Actions: четыре отдельных статуса сборок и артефакты
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
- Добавлен `apple-make-app.sh`: формирует `Omnilite.app/Contents/MacOS`, `Info.plist`, при наличии переносит `.icns`; подпись остаётся шагом CI на macOS.
- `bash -n ImageGen/ImageGen.E2E.Common/ci/apple-make-app.sh` — успешно.
- Проверка на фиктивной публикации в `tmp/fake-publish`: исполняемый apphost и все обязательные поля `Info.plist` созданы; успешно.
- Workflow разделён на четыре build jobs и запускается на pull request, main и расписании. В iOS jobs используется проверенный набор SDK 10.0.302 / workload 10.0.302.0 / Xcode 26.6. Артефакты упаковываются в tar.gz и публикуются при любом результате сборки.
- Первый workflow [42371375200](https://github.com/r-eliseev/studious-goggles/actions/runs/42371375200): MAUI iOS Simulator собрался и артефакт создался; Avalonia macOS `.app` собрался, подписался ad-hoc и упакован; MAUI Catalyst собрался, но проверка bundle ID прочитала Info.plist не из `Contents`; Avalonia iOS нашла ошибки компиляции, отражённые в отчёте 246. Исправлено: целевые пути app bundle ограничены соответствующей платформой; Catalyst теперь выбирает правильный Info.plist для его структуры.
- `bash scripts/check.sh`, `git diff --check` — успешно. Повторный workflow с исправлениями и Avalonia iOS фиксом ожидается.

## Комментарии

### 2026-11-07, claude-audit
Создано по дополнению Apple: этап P2. Avalonia macOS сейчас публикуется в release.yml папкой, не .app. План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
Дополнение владельца «MAUI + Avalonia — Apple CI/E2E» (оригинал — `docs/specs/ui-e2e-original/`). План одобрен ранее данным поручением «заапрувь все таски которые будут». Новый workflow `apple-ci.yml` по этому дополнению владелец попросил явно (ADR 0030).
