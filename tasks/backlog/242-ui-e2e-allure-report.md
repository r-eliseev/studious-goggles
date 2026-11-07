---
id: 242
status: approved
assignee:
claimed:
created: 2026-11-07
---

# UI-тесты: общий отчёт Allure HTML и статусы в PR

## Описание
Этап «отчёт» спецификации UI-тестов (ADR 0029, `docs/specs/ui-e2e.md`): собрать результаты Allure обеих платформ в один HTML-отчёт с видео и логами, загрузить артефактом и показать ссылку в сводке прогона.

Зависит от: 240, 241 (оба пишут `allure-results`).
Среда: GitHub Actions; локально — `scripts/check.sh`.

## Критерии готовности
- Оба E2E-проекта пишут результаты Allure в `reports/<платформа>/allure-results` (файл `allureConfig.json` в каждом проекте, `"directory": "<путь>"`; путь сверить с README `Allure.NUnit` 2.15.0), и этот каталог входит в артефакты `android-e2e` / `windows-e2e`.
- Job `report` (`ubuntu-latest`, `needs: [test-android, test-windows]`, `if: always()`): скачать оба артефакта с `continue-on-error: true` на каждый шаг скачивания (отсутствие одного не валит job); скопировать содержимое всех найденных `allure-results` в `reports/allure-results` (имена файлов Allure — UUID, конфликтов нет; при совпадении имени — ошибка с именем файла); если папка пуста — сводка «нет результатов» и выход 0.
- Генерация: `actions/setup-java@v4` (Temurin 21) и `npx -y allure-commandline@2.46.1 generate reports/allure-results --clean -o reports/allure-html` (версия закреплена; npm, 2026-11-07: latest 2.46.1). Артефакт `allure-report` (`reports/allure-html`, 14 дней).
- `$GITHUB_STEP_SUMMARY`: таблица по платформам — прошло / упало / пропущено (посчитать по `*-result.json`, поле `status`), и строка «Отчёт: артефакт allure-report» со ссылкой на прогон (`$GITHUB_SERVER_URL/$GITHUB_REPOSITORY/actions/runs/$GITHUB_RUN_ID`). Подсчёт — небольшой скрипт `ImageGen/ImageGen.E2E.Common/ci/allure-summary.sh` (bash + `jq`, `jq` есть на `ubuntu-latest`).
- Статусы в PR: отдельные проверки `test-android` и `test-windows` дают сами jobs; убедиться в PR задачи, что обе видны, и записать в «Отчёт» (скриншот не нужен — имена проверок).
- `docs/specs/ui-e2e.md`: статус «в работе» → «реализовано», если 238–242 закрыты и критерии спецификации выполнены; иначе оставить и перечислить невыполненное в «Отчёте».
- `docs/knowledge/ui-e2e.md`: как открыть отчёт (скачать артефакт, `npx allure-commandline open` или открыть `index.html` через локальный HTTP-сервер — проверить, открывается ли с `file://`).

## Анализ
`.github/workflows/ui-e2e.yml` (после 240/241), README `Allure.NUnit` (конфиг `allureConfig.json`), ADR 0029.

## План
1. Каталог результатов Allure в обоих проектах и артефактах.
2. Скрипт сводки (проверить локально на двух вручную созданных `*-result.json` в `tmp/`).
3. Job `report`, прогон в CI, проверка отчёта.
4. Документация.

Файлы: `ImageGen/ImageGen.E2E.Android/allureConfig.json` (новый), `ImageGen/ImageGen.E2E.Windows/allureConfig.json` (новый), csproj обоих проектов (копирование `allureConfig.json` в вывод), `ImageGen/ImageGen.E2E.Common/ci/allure-summary.sh` (новый), `.github/workflows/ui-e2e.yml`, `docs/specs/ui-e2e.md`, `docs/knowledge/ui-e2e.md`.

### Проверка
```
bash ImageGen/ImageGen.E2E.Common/ci/allure-summary.sh tmp/allure-sample
bash scripts/check.sh
# в GitHub Actions: job report, артефакт allure-report, сводка прогона
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
Создано по спецификации UI-тестов владельца: этап «Allure и CI-интеграция». Версия allure-commandline проверена в npm 2026-11-07. План для Luna.

### 2026-11-07, владелец (в чате, записал claude-audit)
Спецификация UI-тестов прислана владельцем; выбор: NUnit + Allure, сначала smoke, затем мок. План одобрен ранее данным поручением «заапрувь все таски которые будут». Изменение `.github/workflows/` по этой спецификации владелец попросил явно (ADR 0029).
