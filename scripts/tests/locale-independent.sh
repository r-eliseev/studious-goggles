#!/usr/bin/env bash
# Regression: legacy Russian task headings remain readable under the POSIX locale.
set -euo pipefail
repo_root=$(cd "$(dirname "$0")/../.." && pwd)
tmp_root="$repo_root/tmp/locale-independent-test-$$"
trap 'rm -rf "$tmp_root"' EXIT
mkdir -p "$tmp_root/repo/scripts" "$tmp_root/repo/tasks/backlog"
cp "$repo_root/scripts/check-tasks.sh" "$repo_root/scripts/tasks-lib.sh" \
  "$repo_root/scripts/submit-task.sh" "$tmp_root/repo/scripts/"
cd "$tmp_root/repo"
git init -q

plan=$(printf '\xD0\x9F\xD0\xBB\xD0\xB0\xD0\xBD')
report=$(printf '\xD0\x9E\xD1\x82\xD1\x87\xD1\x91\xD1\x82')
description=$(printf '\xD0\x9E\xD0\xBF\xD0\xB8\xD1\x81\xD0\xB0\xD0\xBD\xD0\xB8\xD0\xB5')
acceptance=$(printf '\xD0\x9A\xD1\x80\xD0\xB8\xD1\x82\xD0\xB5\xD1\x80\xD0\xB8\xD0\xB8\x20\xD0\xB3\xD0\xBE\xD1\x82\xD0\xBE\xD0\xB2\xD0\xBD\xD0\xBE\xD1\x81\xD1\x82\xD0\xB8')
analysis=$(printf '\xD0\x90\xD0\xBD\xD0\xB0\xD0\xBB\xD0\xB8\xD0\xB7')
comments=$(printf '\xD0\x9A\xD0\xBE\xD0\xBC\xD0\xBC\xD0\xB5\xD0\xBD\xD1\x82\xD0\xB0\xD1\x80\xD0\xB8\xD0\xB8')

make_task() { # <status> <assignee>
  cat > tasks/backlog/001-legacy.md <<EOF
---
id: 001
status: $1
assignee: $2
claimed: $([ -n "$2" ] && echo 2026-11-13T00:00:00Z)
created: 2026-11-13
---

# Legacy task

## $description
Example.

## $acceptance
- It works.

## $analysis
No analysis needed.

## $plan
Step one.

## $report
Completed step one.

## $comments
EOF
}

make_task review ""
env LC_ALL=C bash scripts/check-tasks.sh >/dev/null || {
  echo 'locale-independent regression: legacy Russian Plan and Report should pass check-tasks.sh' >&2
  exit 1
}

make_task in-progress codex
env LC_ALL=C bash scripts/submit-task.sh 1 >/dev/null || {
  echo 'locale-independent regression: submit-task.sh should accept a filled legacy Report' >&2
  exit 1
}
echo 'locale-independent regression: ok'
