#!/usr/bin/env bash
# Черновик заметок о релизе из выполненных задач. Использование: scripts/release-notes.sh <релиз>
set -eu
cd "$(dirname "$0")/.."
. scripts/tasks-lib.sh
[ $# -eq 1 ] || { echo "Использование: $0 <релиз>" >&2; exit 1; }
dir="tasks/$1"
[ -d "$dir" ] || { echo "Нет каталога $dir" >&2; exit 1; }
echo "# Релиз $1"; echo
# Задачи, уже убранные в журнал завершённых (scripts/archive-tasks.sh)
if [ -f tasks/DONE.md ]; then
  awk -F' [|] ' -v r="$1" '$2 == r && $3 == "done" { print "- " $5 " (" substr($1, 3) ")" }' tasks/DONE.md
fi
for f in "$dir"/[0-9][0-9]*-*.md; do
  [ -e "$f" ] || continue
  [ "$(task_field "$f" status)" = "done" ] || continue
  title=$(grep -m1 '^# ' "$f" | sed 's/^# //')
  echo "- $title ($(basename "$f"))"
done
