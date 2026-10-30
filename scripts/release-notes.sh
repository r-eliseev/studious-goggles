#!/usr/bin/env bash
# Заметки о релизе: задачи, завершённые после предыдущего тега. Использование: scripts/release-notes.sh <тег> [<предыдущий тег>]
set -eu
cd "$(dirname "$0")/.."
. scripts/tasks-lib.sh

[ $# -ge 1 ] && [ $# -le 2 ] || { echo "Использование: $0 <тег> [<предыдущий тег>]" >&2; exit 1; }

# Стабильный тег: три числа без ведущих нулей, без суффиксов.
tag_re='^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'

for name in "$@"; do
  printf '%s' "$name" | grep -Eq "$tag_re" || { echo "Неверный формат тега $name" >&2; exit 1; }
done

# Коммит тега. Только полное имя refs/tags/<имя>: сокращённое имя через ветку не разрешается.
TAG_SHA=""
resolve_tag() {
  local name="$1" ref
  ref=$(git rev-parse --verify --quiet "refs/tags/$name^{commit}" 2>/dev/null) || ref=""
  if [ -z "$ref" ]; then
    echo "Нет тега $name" >&2
    exit 1
  fi
  TAG_SHA="$ref"
}

tag="$1"
resolve_tag "$tag"
tag_sha="$TAG_SHA"

prev=""
prev_sha=""
if [ $# -eq 2 ]; then
  prev="$2"
else
  prev=$(git tag --sort=v:refname | grep -E "$tag_re" | awk -v t="$tag" '{ if ($0 == t) { print prev; exit } prev = $0 }')
fi

if [ -n "$prev" ]; then
  if [ "$prev" = "$tag" ] || [ "$(printf '%s\n%s\n' "$prev" "$tag" | sort -V | head -1)" != "$prev" ]; then
    echo "Предыдущий тег должен быть меньше целевого" >&2
    exit 1
  fi
  resolve_tag "$prev"
  prev_sha="$TAG_SHA"
fi

# Новые завершённые задачи: то, чего не было завершено на предыдущем теге.
if [ -n "$prev_sha" ]; then
  ids=$(mktemp)
  task_done_at "$prev_sha" | cut -f1 | sort -u > "$ids"
  new=$(awk -F'\t' 'FILENAME == ARGV[1] { seen[$1]=1; next } !seen[$1]' "$ids" <(task_done_at "$tag_sha"))
  rm -f "$ids"
else
  new=$(task_done_at "$tag_sha")
fi

printf '# ImageGen %s\n\n' "$tag"
if [ -z "$prev_sha" ]; then
  printf 'Все завершённые задачи:\n\n'
  if [ -n "$new" ]; then
    printf '%s\n' "$new" | while IFS=$'\t' read -r id title; do
      printf -- '- %03d — %s\n' "$((10#$id))" "$title"
    done
  fi
elif [ -z "$new" ]; then
  printf 'Новых завершённых задач после %s нет.\n' "$prev"
else
  printf 'Задачи, завершённые после %s:\n\n' "$prev"
  printf '%s\n' "$new" | while IFS=$'\t' read -r id title; do
    printf -- '- %03d — %s\n' "$((10#$id))" "$title"
  done
fi

# Ссылка на сравнение коммитов, если адрес репозитория известен.
url=""
if [ -n "${GITHUB_REPOSITORY:-}" ]; then
  url="${GITHUB_SERVER_URL:-https://github.com}/$GITHUB_REPOSITORY"
else
  remote=$(git remote get-url origin 2>/dev/null || true)
  case "$remote" in
    https://github.com/*) url="${remote%.git}" ;;
  esac
fi
if [ -n "$prev_sha" ] && [ -n "$url" ]; then
  printf '\nВсе изменения: %s/compare/%s...%s\n' "$url" "$prev" "$tag"
fi
