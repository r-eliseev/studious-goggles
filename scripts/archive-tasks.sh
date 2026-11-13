#!/usr/bin/env bash
# Archives completed and cancelled tasks from tasks/: appends a row to tasks/DONE.md and removes the file with git rm.
# The full task text remains in git history (the path is recorded in the log row).
# Usage: scripts/archive-tasks.sh [--dry-run]
set -eu
cd "$(dirname "$0")/.."
. scripts/tasks-lib.sh
dry=0
[ "${1:-}" = "--dry-run" ] && dry=1
journal=tasks/DONE.md
if [ ! -f "$journal" ] && [ $dry -eq 0 ]; then
  printf '# Completed tasks\n\nTasks with `done` or `cancelled` status removed from `tasks/` by `scripts/archive-tasks.sh`. Task text is in git history: `git log --all -- <path>`, then `git show <commit>^:<path>`. These numbers are never reused.\n\n' > "$journal"
fi
n=0
while IFS= read -r f; do
  st=$(task_field "$f" status)
  case "$st" in done|cancelled) ;; *) continue ;; esac
  id=$(task_field "$f" id); as=$(task_field "$f" assignee)
  if [ -n "$as" ]; then echo "Skipping $f: status is $st, but it is claimed by $as" >&2; continue; fi
  if ! git diff --quiet HEAD -- "$f" 2>/dev/null; then echo "Skipping $f: it has uncommitted changes; commit them first" >&2; continue; fi
  title=$(grep -m1 '^# ' "$f" | sed 's/^# //')
  rel=$(basename "$(dirname "$f")")
  date=$(git log -1 --format=%cs -- "$f" 2>/dev/null || true)
  line="- $id | $rel | $st | ${date:-unknown} | $title | \`$f\`"
  if [ $dry -eq 1 ]; then echo "$line"; else echo "$line" >> "$journal"; git rm -q "$f"; fi
  n=$((n + 1))
done < <(find tasks -type f -name '[0-9][0-9]*-*.md' | sort)
[ $dry -eq 0 ] && [ -f "$journal" ] && git add "$journal"
echo "Tasks archived: $n"
