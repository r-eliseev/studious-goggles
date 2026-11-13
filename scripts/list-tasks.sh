#!/usr/bin/env bash
# List tasks: status, assignee, file.
# Usage: scripts/list-tasks.sh [--main] [status] [release]
# With --main, list tasks from the matching origin branch (TASKS_BRANCH=main means merged state), not the working copy.
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
