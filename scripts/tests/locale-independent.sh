#!/usr/bin/env bash
# Regression: task parsing and submission use English headings under the POSIX locale.
set -euo pipefail
repo_root=$(cd "$(dirname "$0")/../.." && pwd)
tmp_root="$repo_root/tmp/locale-independent-test-$$"
trap 'rm -rf "$tmp_root"' EXIT
mkdir -p "$tmp_root/repo/scripts" "$tmp_root/repo/tasks/backlog"
cp "$repo_root/scripts/check-tasks.sh" "$repo_root/scripts/tasks-lib.sh" \
  "$repo_root/scripts/submit-task.sh" "$tmp_root/repo/scripts/"
cd "$tmp_root/repo"
git init -q

make_task() { # <status> <plan heading> <report heading>
  cat > tasks/backlog/001-locale.md <<EOF
---
id: 001
status: $1
assignee: $([ "$1" = in-progress ] && echo codex)
claimed: $([ "$1" = in-progress ] && echo 2026-11-13T00:00:00Z)
created: 2026-11-13
---

# Locale test

## Description
Example.

## Acceptance criteria
- The English headings are accepted.

## Analysis
No analysis needed.

## Plan
Step one.

## Report
Completed step one.

## Comments
EOF
  if [ "$2" != Plan ] || [ "$3" != Report ]; then
    python3 - "$2" "$3" <<'PY'
from pathlib import Path
import sys
p = Path("tasks/backlog/001-locale.md")
s = p.read_text()
s = s.replace("## Plan", "## " + sys.argv[1]).replace("## Report", "## " + sys.argv[2])
p.write_text(s)
PY
  fi
}

make_task review Plan Report
env LC_ALL=C bash scripts/check-tasks.sh >/dev/null || {
  echo 'locale regression: English task sections should pass check-tasks.sh' >&2
  exit 1
}

make_task in-progress Plan Report
env LC_ALL=C bash scripts/submit-task.sh 1 >/dev/null || {
  echo 'locale regression: submit-task.sh should accept a filled English Report' >&2
  exit 1
}

legacy_plan=$(printf '\xD0\x9F\xD0\xBB\xD0\xB0\xD0\xBD')
legacy_report=$(printf '\xD0\x9E\xD1\x82\xD1\x87\xD1\x91\xD1\x82')
make_task review "$legacy_plan" "$legacy_report"
if env LC_ALL=C bash scripts/check-tasks.sh >/dev/null 2>&1; then
  echo 'locale regression: check-tasks.sh must reject legacy task headings' >&2
  exit 1
fi

echo 'locale regression: ok'
