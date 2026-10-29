---
id: 118
status: planned
assignee:
claimed:
created: 2026-10-29
---

# Добавить корневой README для пользователя и разработчика

## Описание
Найдено ревью 2026-10-29. В публичном репозитории нет корневого `README.md`. `AGENTS.md` и `docs/index.md` рассчитаны на агентов и внутреннюю разработку, но GitHub landing page не объясняет, что уже работает, где скачать релиз, как запустить приложение, настроить OpenRouter/fal и где лежат пользовательские данные.

Зависит от: 100, 116.
Среда: Linux или Windows.

## Критерии готовности
- Корневой README кратко разделяет «работает сейчас» и roadmap; не выдаёт Web/Docker/галерею/расходы за готовые функции.
- Есть ссылки на GitHub Releases, лицензию, `docs/index.md`, `docs/product.md` и troubleshooting/development docs.
- Для пользователя описаны варианты Avalonia/MAUI, первый запуск, ввод ключа, локальное хранение данных и предупреждение о Debug trace.
- Для разработчика указаны .NET 10, команды сборки Avalonia и четыре тестовых проекта из `AGENTS.md`; platform limits не дублируются подробно, а ссылаются на `docs/knowledge/dev-environment.md`.
- Инструкция ARM64 соответствует результату задачи 100; до её завершения README не рекомендует ошибочный MAUI win-arm64 asset.
- Нет badges или заявлений о поддержке, которые не подтверждаются CI/релизом.

## Анализ
- В репозитории есть `docs/design/README.md` и `poc/README.md`, но они не являются корневым introduction.
- README зависит от 116, чтобы не повторить устаревший Replicate, и от 100, чтобы не рекламировать неверный arm64 package.
- Скриншот не обязателен: стабильного пользовательского изображения в assets не найдено; не добавлять бинарник ради этой задачи.

## План
Для исполнителя (Haiku/Luna): задача только документационная. Не копируй большие разделы из docs и не выдумывай installation commands. Все команды сначала сверь с `AGENTS.md` и workflow.

Прочитай: `docs/product.md`, `docs/status.md`, `docs/index.md`, `docs/knowledge/dev-environment.md`, `AGENTS.md`, `.github/workflows/build.yml`, `.github/workflows/release.yml`, `LICENSE`.

1. Создать `README.md` со структурой: название/одно предложение; Current features; Download; First run and keys; Local data/privacy; Development; Documentation; Roadmap; License.
2. Current features перечисляет только реализованное в текущем коде: sessions/chat assistant, multi-model runs, OpenRouter/fal, configurable compatible providers, local SQLite/files, MAUI/Avalonia packages по факту release workflow.
3. Download ведёт на `https://github.com/r-eliseev/studious-goggles/releases`; platform table строить только по успешным артефактам после задачи 100. Для debug-signed Android явно написать «sideload, not store-signed».
4. First run не содержит ключей-примеров. Объяснить, что secrets сохраняются через platform secret store; fallback Avalonia без системного backend живёт только до выхода.
5. Privacy: prompts/images local; обычные logs не содержат bodies на Information; тела и подробности ошибок доступны на Debug, полный debug trace включается отдельно и содержит пользовательский контент. Не советовать публиковать debug folder.
6. Development: prerequisites .NET 10; команды build Avalonia и `dotnet test --project` для Core/Data/Presentation/Avalonia tests; MAUI и platform matrix — ссылка на dev-environment.
7. Проверить все относительные ссылки и выполнить harness.

### Проверка
```
bash scripts/check.sh
```

Файлы: `README.md`

## Отчёт

## Комментарии
