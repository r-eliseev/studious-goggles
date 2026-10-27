#!/usr/bin/env bash
# Общие функции для скриптов задач. Подключается командой: . scripts/tasks-lib.sh

T_REMOTE="${TASKS_REMOTE:-origin}"
T_BRANCH="${TASKS_BRANCH:-main}"

# task_field <файл> <поле>: значение поля из заголовка файла задачи
task_field() {
  awk -v k="$2" '/^---$/{n++; next} n==1 && index($0,k":")==1 {sub("^"k":[ ]*",""); print; exit}' "$1"
}

# task_set <файл> <поле> <значение>: записывает поле в заголовок (добавляет, если его нет)
task_set() {
  awk -v k="$2" -v v="$3" '
    /^---$/ { n++; if (n==2 && !done) { print k ":" (v=="" ? "" : " " v); done=1 } print; next }
    n==1 && index($0,k":")==1 { print k ":" (v=="" ? "" : " " v); done=1; next }
    { print }' "$1" > "$1.tmp" && mv "$1.tmp" "$1"
}

# task_norm_id <число>: номер с ведущими нулями (1 -> 001)
task_norm_id() { printf '%03d' "$((10#$1))"; }

# task_next_id <корень>: следующий свободный номер задачи
task_next_id() {
  local last
  last=$(find "$1/tasks" -type f -name '[0-9][0-9]*-*.md' | sed -E 's|.*/([0-9]+)-[^/]*$|\1|' | sort -n | tail -1)
  printf '%03d' $(( 10#${last:-0} + 1 ))
}

# task_file <корень> <номер>: путь к файлу задачи
task_file() { find "$1/tasks" -type f -name "$2-*.md" 2>/dev/null | head -1; }

# task_render <шаблон> <номер> <имя>: файл задачи из шаблона (в stdout)
task_render() {
  sed -e "s/{{ID}}/$2/" -e "s/{{DATE}}/$(date +%F)/" -e "s/{{TITLE}}/$3/" "$1"
}

_task_drop() { git -C "$1" worktree remove --force "$2/wt" >/dev/null 2>&1; rm -rf "$2/wt"; }

# task_on_main <функция> <сообщение коммита>
# Применяет функцию к временной копии $T_REMOTE/$T_BRANCH, коммитит и пушит.
# Если кто-то успел запушить раньше, начинает заново на свежей версии (до 10 раз).
# Функция получает путь к копии и возвращает: 0 — закоммитить, 100 — менять нечего,
# любое другое число — прервать с этим кодом. Ваша рабочая копия не затрагивается.
task_on_main() {
  local fn="$1" msg="$2" repo tmp rc i err=""
  repo=$(git rev-parse --show-toplevel) || return 1
  tmp=$(mktemp -d) || return 1
  for i in 1 2 3 4 5 6 7 8 9 10; do
    # неудачный fetch тоже повторяем: параллельные процессы в одном клоне могут на миг заблокировать ссылку
    if ! err=$(git -C "$repo" fetch -q "$T_REMOTE" "$T_BRANCH" 2>&1); then sleep 1; continue; fi
    git -C "$repo" worktree add -q --detach "$tmp/wt" "$T_REMOTE/$T_BRANCH" || { rm -rf "$tmp"; return 1; }
    "$fn" "$tmp/wt"; rc=$?
    if [ $rc -ne 0 ]; then _task_drop "$repo" "$tmp"; rm -rf "$tmp"; return $rc; fi
    git -C "$tmp/wt" add -A tasks
    if ! git -C "$tmp/wt" commit -q -m "$msg"; then
      echo "Не удалось создать коммит (проверьте git config user.name и user.email)" >&2
      _task_drop "$repo" "$tmp"; rm -rf "$tmp"; return 1
    fi
    if err=$(git -C "$tmp/wt" push -q "$T_REMOTE" "HEAD:$T_BRANCH" 2>&1); then
      _task_drop "$repo" "$tmp"; rm -rf "$tmp"; return 0
    fi
    _task_drop "$repo" "$tmp"
  done
  echo "Не удалось обновить $T_REMOTE/$T_BRANCH за 10 попыток. Последняя ошибка: $err" >&2
  rm -rf "$tmp"; return 1
}
