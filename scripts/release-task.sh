#!/usr/bin/env bash
# Снимает занятость задачи (очищает assignee и claimed).
# Использование: scripts/release-task.sh <номер> [--publish]
#   без --publish правится файл в вашей рабочей копии (так делают при завершении задачи);
#   с --publish занятость снимается сразу в origin/main (так человек снимает «зависшую» занятость).
set -u
cd "$(dirname "$0")/.."
. scripts/tasks-lib.sh
[ $# -ge 1 ] || { echo "Использование: $0 <номер> [--publish]" >&2; exit 1; }
case "$1" in ''|*[!0-9]*) echo "Номер задачи — число" >&2; exit 1 ;; esac
ID=$(task_norm_id "$1")

release() {  # <корень>
  local f cur
  f=$(task_file "$1" "$ID")
  [ -n "$f" ] || { echo "Задача $ID не найдена" >&2; return 3; }
  cur=$(task_field "$f" assignee)
  [ -n "$cur" ] || return 100
  REL_WAS="$cur"
  task_set "$f" assignee ""
  task_set "$f" claimed ""
}

if [ "${2:-}" = "--publish" ]; then
  task_on_main release "task $ID: занятость снята"; rc=$?
else
  release .; rc=$?
fi
case $rc in
  0)   echo "Занятость снята (была: $REL_WAS)" ;;
  100) echo "Задача $ID не была занята" ;;
  *)   exit $rc ;;
esac
