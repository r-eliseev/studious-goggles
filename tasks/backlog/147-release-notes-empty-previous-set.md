---
id: 147
status: approved
assignee:
claimed:
created: 2026-10-30
---

# Не терять release notes при пустом предыдущем наборе задач

## Описание
Задача099 не закрывает крайний случай: предыдущий тег существует, но не содержит done задач. awk с NR==FNR принимает весь второй файл за первый, если первый пуст. Завершённые в текущем теге задачи исчезают из заметок.

Зависит от: — (099 возвращена из done аудитом; это её остаток).
Среда: Linux или Windows с Bash/Git. В этой задаче явно разрешены минимальная правка scripts/release-notes.sh и новый shell regression test; workflows и другие scripts не менять.

## Критерии готовности
- previous tag с нулём done + current tag с одной done → задача присутствует в notes.
- Непустой previous исключает только уже завершённые ids, пустая разность показывает штатный текст.
- Поддерживаются done как отдельные файлы и как строки tasks/DONE.md; cancelled исключены.
- Сохраняются validation тега, version ordering, заголовок и compare URL; тест запускается локально без сети.

## Анализ
Evidence: scripts/release-notes.sh:63–65 — awk NR==FNR; task099 критерий1 требует все задачи между тегами независимо от папки. Воспроизведено аудитом в tmp/audit-release-notes-b2sebc2x: v0.0.1 без задач, v0.0.2 с001 done → «Новых завершённых задач ... нет». Исправление scripts здесь разрешено непосредственно описанием задачи, объём ограничен set difference и тестом.

## План
1. Прочитать release-notes.sh, task_done_at в tasks-lib.sh и docs/knowledge/dev-environment.md раздел Релиз.
2. Создать каталог scripts/tests и scripts/tests/release-notes-empty-previous.sh: временный git repo строго под корневым tmp/, trap cleanup, локальные git user.name/email, скопировать необходимые два скрипта, минимум два коммита/тега. Без изменения глобального git config и существующих тегов проекта.
3. Первый тест воспроизводит пустой previous set и должен упасть до фикса. Вызывать настоящий скрипт из временного repo, проверять наличие001, а не только exit0.
4. Заменить различение входных файлов awk на сравнение FILENAME с именем первого входного файла (либо явную загрузку set через getline до чтения current). Алгоритм должен работать при нуле строк в первом файле; сохранить tab delimiter, id/title и порядок. Не переписывать task_done_at.
5. Regression scenarios: предыдущий набор пуст; непустой previous и новый done; нет новых done; first tag; archive DONE.md вместо task file; cancelled не попадает. Для compare URL задать origin https://github.com/example/release-notes-test.git без сетевого запроса; перед вызовом тестируемого скрипта удалить только GITHUB_REPOSITORY/GITHUB_SERVER_URL из окружения команды (не proxy/credentials). TMPDIR для fixture и дочернего скрипта указывает внутрь корневого tmp/. Добавить cases v0.1.9→v0.1.10, invalid prerelease/leading-zero/nonexistent tag, equal/larger explicit previous.
6. Запустить новый тест и существующие shell checks. Дописать команду теста в релизный раздел docs/knowledge/dev-environment.md. В099 добавить отчёт о закрытии gap и вернуть done только после этих проверок.

Файлы: `scripts/release-notes.sh`, `scripts/tests/release-notes-empty-previous.sh` (новый), `docs/knowledge/dev-environment.md`, `tasks/backlog/099-release-notes-since-tag.md` (или запись о ней после будущей архивации).

Проверка: `bash scripts/tests/release-notes-empty-previous.sh`; `bash -n scripts/release-notes.sh`; `bash scripts/check.sh`.

### Команды проверки

Выполнять по одной, из корня репозитория:

```bash
bash scripts/tests/release-notes-empty-previous.sh
bash -n scripts/release-notes.sh
bash scripts/check.sh
```

### Порядок исполнения и проверки
План рассчитан на Luna: прочитать перечисленные файлы, выполнить шаги по порядку; сначала падающий регрессионный тест, затем минимальный фикс (ub-mode из AGENTS.md). Не реализовывать соседние задачи. Сигнатуры сверять с текущим main; при rebase сохранять чужие изменения. Не использовать платные API и не записывать секреты. Команды выполнять последовательно: общие build outputs не допускают параллельные dotnet build/test.
После изменения каждого проекта: `dotnet format ImageGen/<Проект>/<Проект>.csproj --verify-no-changes`; после всех изменений — перечисленные тесты и `bash scripts/check.sh`. В отчёте указать команды, результаты, ручные ограничения и отличие от плана. Код писать только после одобрения плана по docs/tasks.md.

## Отчёт

## Комментарии

### 2026-10-30, codex-audit
Создано по поручению владельца: подробный план для Luna. В этой задаче код ещё не исправлен; статус planned не означает одобрение реализации.

### 2026-10-30, владелец (в чате, записал codex)
План одобрен явным поручением «Заапрувь таски на фиксы». Можно реализовывать по описанному плану после готовности зависимостей в main и проверки среды/занятости по docs/tasks.md.
