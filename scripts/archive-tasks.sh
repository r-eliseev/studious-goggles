#!/usr/bin/env bash
# Убирает из tasks/ выполненные и отменённые задачи: строка в tasks/DONE.md, файл удаляется через git rm.
# Полный текст задачи остаётся в истории git (путь записан в строке журнала).
# Использование: scripts/archive-tasks.sh [--dry-run]
set -eu
cd "$(dirname "$0")/.."
. scripts/tasks-lib.sh
dry=0
[ "${1:-}" = "--dry-run" ] && dry=1
journal=tasks/DONE.md
if [ ! -f "$journal" ] && [ $dry -eq 0 ]; then
  printf '# Завершённые задачи\n\nЗадачи со статусом `done` или `cancelled`, убранные из `tasks/` скриптом `scripts/archive-tasks.sh`. Текст задачи — в истории git: `git log --all -- <путь>`, затем `git show <коммит>^:<путь>`. Номера отсюда не используются повторно.\n\n' > "$journal"
fi
n=0
while IFS= read -r f; do
  st=$(task_field "$f" status)
  case "$st" in done|cancelled) ;; *) continue ;; esac
  id=$(task_field "$f" id); as=$(task_field "$f" assignee)
  if [ -n "$as" ]; then echo "Пропуск $f: статус $st, но занята ($as)" >&2; continue; fi
  if ! git diff --quiet HEAD -- "$f" 2>/dev/null; then echo "Пропуск $f: есть незакоммиченные правки, сначала закоммить" >&2; continue; fi
  title=$(grep -m1 '^# ' "$f" | sed 's/^# //')
  rel=$(basename "$(dirname "$f")")
  date=$(git log -1 --format=%cs -- "$f" 2>/dev/null || true)
  line="- $id | $rel | $st | ${date:-неизвестно} | $title | \`$f\`"
  if [ $dry -eq 1 ]; then echo "$line"; else echo "$line" >> "$journal"; git rm -q "$f"; fi
  n=$((n + 1))
done < <(find tasks -type f -name '[0-9][0-9]*-*.md' | sort)
[ $dry -eq 0 ] && [ -f "$journal" ] && git add "$journal"
echo "Задач в архив: $n"
