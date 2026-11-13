#!/usr/bin/env bash
# Release notes: tasks completed after the previous tag. Usage: scripts/release-notes.sh <tag> [<previous tag>]
set -eu
cd "$(dirname "$0")/.."
. scripts/tasks-lib.sh

[ $# -ge 1 ] && [ $# -le 2 ] || { echo "Usage: $0 <tag> [<previous tag>]" >&2; exit 1; }

# Stable tag: three numbers without leading zeroes or suffixes.
tag_re='^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'

for name in "$@"; do
  printf '%s' "$name" | grep -Eq "$tag_re" || { echo "Invalid tag format: $name" >&2; exit 1; }
done

# Tag commit. Only the full refs/tags/<name> ref is accepted; a short branch name is not.
TAG_SHA=""
resolve_tag() {
  local name="$1" ref
  ref=$(git rev-parse --verify --quiet "refs/tags/$name^{commit}" 2>/dev/null) || ref=""
  if [ -z "$ref" ]; then
    echo "Tag not found: $name" >&2
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
    echo "The previous tag must be older than the target" >&2
    exit 1
  fi
  resolve_tag "$prev"
  prev_sha="$TAG_SHA"
fi

# Newly completed tasks: tasks not completed at the previous tag.
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
  printf 'All completed tasks:\n\n'
  if [ -n "$new" ]; then
    printf '%s\n' "$new" | while IFS=$'\t' read -r id title; do
      printf -- '- %03d — %s\n' "$((10#$id))" "$title"
    done
  fi
elif [ -z "$new" ]; then
  printf 'No new completed tasks since %s.\n' "$prev"
else
  printf 'Tasks completed since %s:\n\n' "$prev"
  printf '%s\n' "$new" | while IFS=$'\t' read -r id title; do
    printf -- '- %03d — %s\n' "$((10#$id))" "$title"
  done
fi

# Commit comparison link, when the repository URL is known.
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
  printf '\nAll changes: %s/compare/%s...%s\n' "$url" "$prev" "$tag"
fi
