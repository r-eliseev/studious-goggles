#!/usr/bin/env bash
# Создаёт файл задачи.
# Использование: scripts/new-task.sh [--publish] <релиз|backlog> <короткое-имя-латиницей>
# С --publish задача сразу создаётся в origin/main. При параллельной работе используйте его:
# тогда номера задач, созданных разными агентами, не пересекутся.
set -u
cd "$(dirname "$0")/.."
. scripts/tasks-lib.sh
publish=0
if [ "${1:-}" = "--publish" ]; then publish=1; shift; fi
[ $# -eq 2 ] || { echo "Использование: $0 [--publish] <релиз|backlog> <короткое-имя>" >&2; exit 1; }
release="$1"; slug="$2"
case "$release" in .*|*[!a-zA-Z0-9._-]*|"") echo "Недопустимое имя релиза: $release" >&2; exit 1 ;; esac
case "$slug" in *[!a-z0-9-]*|"") echo "Имя задачи: только a-z, 0-9 и дефис" >&2; exit 1 ;; esac

create() {  # <корень>
  local id
  [ -f "$1/tasks/_template.md" ] || { echo "Нет tasks/_template.md" >&2; return 3; }
  id=$(task_next_id "$1")
  NEW_REL="tasks/$release/$id-$slug.md"
  mkdir -p "$1/tasks/$release"
  task_render "$1/tasks/_template.md" "$id" "$slug" > "$1/$NEW_REL"
}

if [ $publish -eq 1 ]; then
  task_on_main create "task: новая задача $release/$slug" || exit $?
  echo "$NEW_REL (создана в $T_REMOTE/$T_BRANCH)"
else
  create . || exit $?
  echo "$NEW_REL"
fi
