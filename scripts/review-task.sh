#!/usr/bin/env bash
# The planner closes the review of a task (ADR 0032).
#   done      — the work matches the plan and the criteria; the Review section must be filled first;
#   approved  — sent back to the executor; write what to fix in Comments (the Review section may stay empty).
# Edits the task file in your working copy.
# Usage: scripts/review-task.sh <number> done|approved
# Exit codes: 0 — done, 3 — task not found, 4 — wrong status, 5 — the Review section is empty.
set -u
cd "$(dirname "$0")/.."
. scripts/tasks-lib.sh
[ $# -eq 2 ] || { echo "Usage: $0 <number> done|approved" >&2; exit 1; }
case "$1" in ''|*[!0-9]*) echo "The task number is a number" >&2; exit 1 ;; esac
case "$2" in done|approved) ;; *) echo "The verdict is 'done' or 'approved'" >&2; exit 1 ;; esac
ID=$(task_norm_id "$1")
f=$(task_file . "$ID")
[ -n "$f" ] || { echo "Task $ID not found" >&2; exit 3; }
st=$(task_field "$f" status)
[ "$st" = "review" ] || { echo "Only a task in 'review' can be reviewed (status '$st')" >&2; exit 4; }
if [ "$2" = "done" ]; then
  awk '$0=="## Review"{p=1; next} /^## /{p=0} p && NF {f=1} END{exit !f}' "$f" \
    || { echo "Fill in the Review section of $f first: what was checked against the plan and the criteria, the verdict" >&2; exit 5; }
fi
task_set "$f" status "$2"
echo "Task $ID: $2."
