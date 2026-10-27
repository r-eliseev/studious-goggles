#!/usr/bin/env bash
# Проверка файлов задач. Код возврата 1, если есть ошибки.
cd "$(dirname "$0")/.."
. scripts/tasks-lib.sh
fail=0
allowed=" todo planned approved in-progress done blocked cancelled "
ids=" "
filled() { awk -v h="$2" '$0=="## " h {p=1; next} /^## /{p=0} p && NF {f=1} END{exit !f}' "$1"; }

while IFS= read -r f; do
  id=$(task_field "$f" id); st=$(task_field "$f" status)
  as=$(task_field "$f" assignee); cl=$(task_field "$f" claimed); base=$(basename "$f")
  [ -n "$id" ] || { echo "ОШИБКА: $f: нет поля id"; fail=1; }
  [ -n "$st" ] || { echo "ОШИБКА: $f: нет поля status"; fail=1; }
  if [ -n "$id" ]; then
    case "$base" in "$id"-*) ;; *) echo "ОШИБКА: $f: id '$id' не совпадает с именем файла"; fail=1 ;; esac
    case "$ids" in *" $id "*) echo "ОШИБКА: $f: повторяющийся id $id"; fail=1 ;; esac
    ids="$ids$id "
  fi
  if [ -n "$st" ]; then
    case "$allowed" in *" $st "*) ;; *) echo "ОШИБКА: $f: неизвестный статус '$st'"; fail=1 ;; esac
    case "$st" in planned|approved|in-progress|done)
      filled "$f" "План" || { echo "ОШИБКА: $f: статус '$st', но раздел 'План' пуст"; fail=1; } ;;
    esac
    if [ "$st" = "done" ]; then
      filled "$f" "Отчёт" || { echo "ОШИБКА: $f: статус 'done', но раздел 'Отчёт' пуст"; fail=1; }
    fi
    if [ "$st" = "in-progress" ] && [ -z "$as" ]; then
      echo "ОШИБКА: $f: статус 'in-progress', но не указан assignee"; fail=1
    fi
    case "$st" in done|cancelled)
      [ -z "$as" ] || { echo "ОШИБКА: $f: статус '$st', но задача всё ещё занята ($as): scripts/release-task.sh ${id:-N}"; fail=1; } ;;
    esac
  fi
  if [ -n "$as" ] && [ -z "$cl" ]; then
    echo "ОШИБКА: $f: указан assignee, но нет claimed"; fail=1
  fi
done < <(find tasks -type f -name '[0-9][0-9]*-*.md' | sort)
exit $fail
