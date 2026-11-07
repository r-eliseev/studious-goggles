---
id: 250
status: approved
assignee:
claimed:
created: 2026-11-07
---

# Apple CI: отчёт Allure по Apple-платформам

## Описание
Дополнение Apple (ADR 0030 п. 8, `docs/specs/apple-ci-e2e.md`): job `apple-report` в `apple-ci.yml` собирает `allure-results` iOS-тестов (и macOS, если появятся) в один HTML-отчёт, не смешивая метки платформ и приложений, со сводкой прогона. Подход и скрипт — из задачи 242.

Зависит от: 242 (`allure-summary.sh`, генерация), 248.
Среда: GitHub Actions; локально — `scripts/check.sh` и проверка скрипта на образцах.

## Критерии готовности
- Job `apple-report` (`ubuntu-latest`, `needs` всех Apple test/smoke-jobs, `if: always()`): скачать доступные артефакты (`continue-on-error` на каждое скачивание), слить `allure-results` в `reports/apple-allure-results`, сгенерировать `allure-commandline@2.46.1` (Temurin 21) → артефакт `apple-allure-html` (14 дней).
- Метки: каждый результат несёт `parentSuite` = `ios-maui` / `ios-avalonia` / `macos-<shell>` (ставят фикстуры 248 через `AllureApi`); проверить в сгенерированном отчёте, что группы не смешаны.
- Сводка `$GITHUB_STEP_SUMMARY`: таблица по `parentSuite` — прошло / упало / пропущено (`allure-summary.sh` из 242, расширить группировкой по `parentSuite` с обратной совместимостью), строки «blocked» для сборок iOS/Catalyst из итогов 245 (job-выходы `outputs.blocked`), ссылка на прогон.
- Нет результатов — сводка «нет результатов», выход 0.
- `docs/specs/apple-ci-e2e.md`: статус «реализовано», если 244–250 закрыты и критерии выполнены; иначе перечислить невыполненное в «Отчёте».

## Анализ
Справочно: дополнение владельца — `docs/specs/ui-e2e-original/MAUI_Avalonia_Apple_CI_E2E_Addendum.md`; при расхождении действуют ADR 0030 и `docs/specs/apple-ci-e2e.md`.
Задача 242 (`allure-summary.sh`, job `report`), `.github/workflows/apple-ci.yml` (после 245–249).

## План
1. Группировка по `parentSuite` в `allure-summary.sh` по TDD на образцах в `tmp/` (два `*-result.json` с разными `parentSuite`).
2. Outputs `blocked` в jobs сборки; job `apple-report`; прогон.
3. Документация.

Файлы: `ImageGen/ImageGen.E2E.Common/ci/allure-summary.sh`, `.github/workflows/apple-ci.yml`, `ImageGen/ImageGen.E2E.iOS/IosTestBase.cs` (метка, если 248 её не поставил), `docs/specs/apple-ci-e2e.md`, `docs/knowledge/apple-ci.md`.

### Проверка
```
bash ImageGen/ImageGen.E2E.Common/ci/allure-summary.sh tmp/allure-apple-sample
bash scripts/check.sh
# в GitHub Actions: apple-report, артефакт apple-allure-html, сводка
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
Создано по дополнению Apple (A8). План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
Дополнение владельца «MAUI + Avalonia — Apple CI/E2E» (оригинал — `docs/specs/ui-e2e-original/`). План одобрен ранее данным поручением «заапрувь все таски которые будут». Новый workflow `apple-ci.yml` по этому дополнению владелец попросил явно (ADR 0030).
