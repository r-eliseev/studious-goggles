---
id: 249
status: in-progress
assignee: codex-gpt6
claimed: 2026-11-07T12:19:55Z
created: 2026-11-07
---

# macOS: проверка GUI на раннере, запуск двух .app и, если получится, UI-тесты Mac2

## Описание
Этап P5 дополнения Apple (ADR 0030 п. 7, `docs/specs/apple-ci-e2e.md` требование 7): на `macos-26` выяснить, есть ли GUI-сессия, содержательный скриншот и права Accessibility; запустить `Omnilite.app` Avalonia и MAUI Mac Catalyst (если 245 его собрал), сохранить доказательства. UI-тесты (Appium Mac2 4.6.1) — только если проверка прошла; иначе задача закрывается записанным блокером, а не зелёными тестами.

Зависит от: 245 (артефакты `avalonia-macos`, `maui-maccatalyst`).
Среда: GitHub Actions `macos-26`.

## Критерии готовности
- Скрипт `ImageGen/ImageGen.E2E.Common/ci/apple-macos-preflight.sh` → `reports/macos-preflight/`:
  1. Сессия: `who`, `launchctl print gui/$(id -u) | head -20` (есть ли GUI-домен), `system_profiler SPDisplaysDataType` (разрешение).
  2. Скриншот: `screencapture -x desktop.png`; проверка «не однотонный» тем же правилом, что в 238 (доля пикселей, отличных от среднего больше чем на 16, > 1 %), — через `python3` без сторонних пакетов: `sips -s format bmp` и разбор BMP вручную, либо `python3 -I` с `struct`.
  3. Accessibility: `osascript -e 'tell application "System Events" to get name of every process whose background only is false'` — успех или текст ошибки (ошибка `-1743`/«not allowed assistive access» = прав нет); записать.
  4. `summary.json`: guiSession, resolution, nonUniformRatio, accessibilityOk.
- Запуск `.app` (для каждого доступного артефакта): распаковать, `open -n <app>` (Avalonia) и тоже для Catalyst; через 15 с — процесс жив (`pgrep -f`), `screencapture -x app-<shell>.png`, отчёты о падении из `~/Library/Logs/DiagnosticReports`; закрыть (`osascript -e 'quit app "Omnilite"'`, затем `pkill`). Если Catalyst «blocked» в 245 — пропуск с записью.
- Job `smoke-macos` (`continue-on-error: true`, `workflow_dispatch` + `schedule`), артефакт `macos-e2e-smoke` при любом исходе.
- Если `accessibilityOk` и `nonUniformRatio` > 1 %: проект UI-тестов не создавать в этой задаче — записать в `docs/status.md` предложение новой задачи «UI-тесты macOS через Mac2 4.6.1» с фактами (решение о ней — владельцу). Если нет — записать блокер в `docs/knowledge/apple-ci.md` («hosted macOS: нет прав Accessibility / пустой кадр»), UI-тесты macOS не заводить.
- Результаты трёх прогонов — в `docs/knowledge/apple-ci.md`.

## Анализ
Справочно: дополнение владельца — `docs/specs/ui-e2e-original/MAUI_Avalonia_Apple_CI_E2E_Addendum.md`; при расхождении действуют ADR 0030 и `docs/specs/apple-ci-e2e.md`.
ADR 0030, задача 238 (правило «не однотонный» и скрипт Windows как образец), `docs/knowledge/apple-ci.md`.

## План
1. Скрипт preflight; разбор BMP проверить локально на сгенерированных однотонном и пёстром BMP в `tmp/`.
2. Запуск `.app` и job.
3. Три прогона и вывод.

Файлы: `ImageGen/ImageGen.E2E.Common/ci/apple-macos-preflight.sh` (новый), `ImageGen/ImageGen.E2E.Common/ci/image-nonuniform.py` (новый), `.github/workflows/apple-ci.yml`, `docs/knowledge/apple-ci.md`, `docs/status.md`.

### Проверка
```
bash -n ImageGen/ImageGen.E2E.Common/ci/apple-macos-preflight.sh
python3 -I ImageGen/ImageGen.E2E.Common/ci/image-nonuniform.py tmp/solid.bmp
python3 -I ImageGen/ImageGen.E2E.Common/ci/image-nonuniform.py tmp/noisy.bmp
bash scripts/check.sh
# в GitHub Actions: smoke-macos и артефакт
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
- Первый автоматический smoke на `main` — run [42381579325](https://github.com/r-eliseev/studious-goggles/actions/runs/42381579325) — остановился до проверки GUI-приложений: `sips -s format bmp` сформировал 32-bit BMP с `BI_BITFIELDS` (`compression=3`), а парсер принимал только `compression=0`.
- Добавил поддержку несжатого 32-bit BMP с RGB masks BGRA и проверил на сохранённом runner screenshot: `nonUniformRatio=0.9991188`; стандартные 24-bit fixture `solid.bmp` и `noisy.bmp` тоже проходят.
- В `smoke_app` добавлена ad-hoc подпись и строгая проверка `.app` перед `open`, поскольку соседние iOS smoke выявили те же unsigned simulator bundle подписи. Следующий CI запуск должен проверить macOS GUI и оба запускаемых приложения.
- Run [42386890196](https://github.com/r-eliseev/studious-goggles/actions/runs/42386890196): скриншот содержательный (`nonUniformRatio=0.9991404`), оба приложения оставались запущенными и создали скриншоты, crash reports отсутствовали. Job завершился с `app_failures[*]: unbound variable` из-за `set -u` и пустого массива; чтение массива исправлено через пустое значение по умолчанию. Hosted confirmation ожидается.

## Комментарии

### 2026-11-07, claude-audit
Создано по дополнению Apple: этап P5. Есть ли на hosted macOS права Accessibility — неизвестно, поэтому UI-тесты Mac2 только после проверки и отдельным решением владельца. План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
Дополнение владельца «MAUI + Avalonia — Apple CI/E2E» (оригинал — `docs/specs/ui-e2e-original/`). План одобрен ранее данным поручением «заапрувь все таски которые будут». Новый workflow `apple-ci.yml` по этому дополнению владелец попросил явно (ADR 0030).

### 2026-11-07, codex-gpt6
Run 42381579325 провалился на конвертированном `desktop.bmp`: заголовок BMP использует compression `BI_BITFIELDS` и 32-bit BGRA masks, законный вариант вывода `sips`. Теперь парсер принимает этот формат. Следующий прогон проверит, что GUI-preflight продолжит выполнение и запустит app smokes.
