#!/usr/bin/env bash
# Занимает задачу: записывает assignee и claimed прямо в origin/main одним маленьким коммитом.
# Гонку двух агентов решает git: пушит первый, второй увидит занятость и получит код 2.
# Использование: scripts/claim-task.sh <номер> <имя-исполнителя>
# Коды возврата: 0 — занята вами, 2 — занята другим, 3 — задача не найдена, 4 — в этом статусе занять нельзя.
set -u
cd "$(dirname "$0")/.."
. scripts/tasks-lib.sh
[ $# -eq 2 ] || { echo "Использование: $0 <номер> <имя-исполнителя>" >&2; exit 1; }
case "$1" in ''|*[!0-9]*) echo "Номер задачи — число" >&2; exit 1 ;; esac
CL_ID=$(task_norm_id "$1"); CL_WHO="$2"
case "$CL_WHO" in ''|*[!a-zA-Z0-9._-]*) echo "Имя исполнителя: только буквы, цифры, точка, дефис, подчёркивание" >&2; exit 1 ;; esac

claim() {  # <корень копии origin/main>
  local f st cur
  f=$(task_file "$1" "$CL_ID")
  [ -n "$f" ] || { echo "Задача $CL_ID не найдена в $T_REMOTE/$T_BRANCH" >&2; return 3; }
  CL_REL="${f#"$1"/}"
  st=$(task_field "$f" status); cur=$(task_field "$f" assignee)
  if [ -n "$cur" ]; then
    [ "$cur" = "$CL_WHO" ] && return 100
    echo "Задача $CL_ID уже занята: $cur (с $(task_field "$f" claimed))" >&2; return 2
  fi
  case "$st" in todo|planned|approved) ;; *) echo "Задачу в статусе '$st' занять нельзя" >&2; return 4 ;; esac
  task_set "$f" assignee "$CL_WHO"
  task_set "$f" claimed "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  if [ "$st" = "approved" ]; then task_set "$f" status in-progress; fi
  return 0
}

task_on_main claim "task $CL_ID: занята $CL_WHO"; rc=$?
case $rc in
  0)   echo "Занята вами ($CL_WHO): $CL_REL" ;;
  100) echo "Уже занята вами ($CL_WHO)" ;;
  *)   exit $rc ;;
esac
CL_BASE=$(basename "${CL_REL:-$CL_ID}" .md)
echo "Дальше: git fetch $T_REMOTE && git switch -c task/$CL_BASE $T_REMOTE/$T_BRANCH"
