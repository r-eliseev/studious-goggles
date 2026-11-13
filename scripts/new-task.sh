#!/usr/bin/env bash
# Creates a task file.
# Usage: scripts/new-task.sh [--publish] <release|backlog> <short-name-in-latin-letters>
# With --publish, creates the task directly on the matching origin branch (TASKS_BRANCH overrides it). Use it when working in parallel:
# this prevents task-number collisions on the same branch (for different branches, see “New tasks” in docs/tasks.md).
set -u
cd "$(dirname "$0")/.."
. scripts/tasks-lib.sh
publish=0
if [ "${1:-}" = "--publish" ]; then publish=1; shift; fi
[ $# -eq 2 ] || { echo "Usage: $0 [--publish] <release|backlog> <short-name>" >&2; exit 1; }
release="$1"; slug="$2"
case "$release" in .*|*[!a-zA-Z0-9._-]*|"") echo "Invalid release name: $release" >&2; exit 1 ;; esac
case "$slug" in *[!a-z0-9-]*|"") echo "Task name may contain only a-z, 0-9, and hyphens" >&2; exit 1 ;; esac

create() {  # <root>
  local id
  [ -f "$1/tasks/_template.md" ] || { echo "tasks/_template.md not found" >&2; return 3; }
  id=$(task_next_id "$1")
  NEW_REL="tasks/$release/$id-$slug.md"
  mkdir -p "$1/tasks/$release"
  task_render "$1/tasks/_template.md" "$id" "$slug" > "$1/$NEW_REL"
}

if [ $publish -eq 1 ]; then
  task_on_main create "task: new task $release/$slug" || exit $?
  echo "$NEW_REL (created on $T_REMOTE/$T_BRANCH)"
else
  create . || exit $?
  echo "$NEW_REL"
fi
