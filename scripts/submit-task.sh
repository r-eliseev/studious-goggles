#!/usr/bin/env bash
# The executor hands a finished task to the planner's review (ADR 0032): status in-progress -> review, the claim is released.
# Edits the task file in your working copy; commit it with the work and open the pull request.
# Usage: scripts/submit-task.sh <number>
# Exit codes: 0 — submitted, 3 — task not found, 4 — wrong status, 5 — the Report is empty.
set -u
cd "$(dirname "$0")/.."
. scripts/tasks-lib.sh
[ $# -eq 1 ] || { echo "Usage: $0 <number>" >&2; exit 1; }
case "$1" in ''|*[!0-9]*) echo "The task number is a number" >&2; exit 1 ;; esac
ID=$(task_norm_id "$1")
f=$(task_file . "$ID")
[ -n "$f" ] || { echo "Task $ID not found" >&2; exit 3; }
st=$(task_field "$f" status)
[ "$st" = "in-progress" ] || { echo "Only an 'in-progress' task can be submitted (status '$st')" >&2; exit 4; }
awk '($0=="## Отчёт" || $0=="## Report"){p=1; next} /^## /{p=0} p && NF {f=1} END{exit !f}' "$f" \
  || { echo "Fill in the Report of $f first: what changed, commands and results, differences from the plan" >&2; exit 5; }
task_set "$f" status review
task_set "$f" assignee ""
task_set "$f" claimed ""
echo "Submitted for review: $f. Next: commit, run scripts/check.sh, open the pull request; the planner reviews it."
