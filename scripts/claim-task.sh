#!/usr/bin/env bash
# Claims a task by recording assignee and claimed in the matching origin branch (TASKS_BRANCH overrides it) in one small commit.
# Git resolves races between agents: the first push wins, and the second agent sees the claim and gets exit code 2.
# Usage: scripts/claim-task.sh <number> <assignee-name>
# Exit codes: 0 claimed by you, 2 claimed by someone else, 3 task not found, 4 task cannot be claimed in this status.
set -u
cd "$(dirname "$0")/.."
. scripts/tasks-lib.sh
[ $# -eq 2 ] || { echo "Usage: $0 <number> <assignee-name>" >&2; exit 1; }
case "$1" in ''|*[!0-9]*) echo "Task number must be numeric" >&2; exit 1 ;; esac
CL_ID=$(task_norm_id "$1"); CL_WHO="$2"
case "$CL_WHO" in ''|*[!a-zA-Z0-9._-]*) echo "Assignee name may contain only letters, digits, period, hyphen, and underscore" >&2; exit 1 ;; esac

claim() {  # <copy-root of $T_REMOTE/$T_BRANCH>
  local f st cur
  f=$(task_file "$1" "$CL_ID")
  [ -n "$f" ] || { echo "Task $CL_ID not found in $T_REMOTE/$T_BRANCH" >&2; return 3; }
  CL_REL="${f#"$1"/}"
  st=$(task_field "$f" status); cur=$(task_field "$f" assignee)
  if [ -n "$cur" ]; then
    [ "$cur" = "$CL_WHO" ] && return 100
    echo "Task $CL_ID is already claimed: $cur (since $(task_field "$f" claimed))" >&2; return 2
  fi
  # Only the executor claims, and only an approved task (ADR 0032): planning is done without a claim.
  case "$st" in approved) ;; *) echo "Only an 'approved' task can be claimed (status '$st'); planning needs no claim" >&2; return 4 ;; esac
  task_set "$f" assignee "$CL_WHO"
  task_set "$f" claimed "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  task_set "$f" status in-progress
  return 0
}

task_on_main claim "task $CL_ID: claimed by $CL_WHO"; rc=$?
case $rc in
  0)   echo "Claimed by you ($CL_WHO): $CL_REL" ;;
  100) echo "Already claimed by you ($CL_WHO)" ;;
  *)   exit $rc ;;
esac
CL_BASE=$(basename "${CL_REL:-$CL_ID}" .md)
echo "Next: git pull --ff-only (the claim commit is already on $T_REMOTE/$T_BRANCH)"
