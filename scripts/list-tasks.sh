#!/usr/bin/env bash
# Список задач: статус, исполнитель, файл.
# Использование: scripts/list-tasks.sh [--main] [статус] [релиз]
# С --main список берётся из origin/main (актуальные занятости других агентов), а не из рабочей копии.
cd "$(dirname "$0")/.."
. scripts/tasks-lib.sh
if [ "${1:-}" = "--main" ]; then
  shift
  git fetch -q "$T_REMOTE" "$T_BRANCH" || exit 1
  tmp=$(mktemp -d); trap 'rm -rf "$tmp"' EXIT
  git archive "$T_REMOTE/$T_BRANCH" tasks | tar -x -C "$tmp" || exit 1
  cd "$tmp"
fi
want="${1:-}"; rel="${2:-}"
find tasks -type f -name '[0-9][0-9]*-*.md' | sort | while IFS= read -r f; do
  st=$(task_field "$f" status); as=$(task_field "$f" assignee)
  [ -z "$want" ] || [ "$st" = "$want" ] || continue
  [ -z "$rel" ] || [ "$(basename "$(dirname "$f")")" = "$rel" ] || continue
  printf '%-12s %-16s %s\n' "$st" "${as:--}" "$f"
done
