#!/usr/bin/env bash
# Releases a task claim (clears assignee and claimed).
# Usage: scripts/release-task.sh <number> [--publish]
#   without --publish, edits the file in the working copy (used when completing a task);
#   with --publish, releases the claim directly on the matching origin branch (TASKS_BRANCH overrides it; useful for clearing a stale claim).
set -u
cd "$(dirname "$0")/.."
. scripts/tasks-lib.sh
[ $# -ge 1 ] || { echo "Usage: $0 <number> [--publish]" >&2; exit 1; }
case "$1" in ''|*[!0-9]*) echo "Task number must be numeric" >&2; exit 1 ;; esac
ID=$(task_norm_id "$1")

release() {  # <root>
  local f cur
  f=$(task_file "$1" "$ID")
  [ -n "$f" ] || { echo "Task $ID not found" >&2; return 3; }
  cur=$(task_field "$f" assignee)
  [ -n "$cur" ] || return 100
  REL_WAS="$cur"
  task_set "$f" assignee ""
  task_set "$f" claimed ""
}

if [ "${2:-}" = "--publish" ]; then
  task_on_main release "task $ID: claim released"; rc=$?
else
  release .; rc=$?
fi
case $rc in
  0)   echo "Claim released (was: $REL_WAS)" ;;
  100) echo "Task $ID was not claimed" ;;
  *)   exit $rc ;;
esac
