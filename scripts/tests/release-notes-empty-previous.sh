#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "$0")/../.." && pwd)
tmp_root="$repo_root/tmp/release-notes-test-$$"
trap 'rm -rf "$tmp_root"' EXIT
mkdir -p "$tmp_root/repo/scripts" "$tmp_root/repo/tasks/backlog" "$tmp_root/tmp"
cp "$repo_root/scripts/release-notes.sh" "$repo_root/scripts/tasks-lib.sh" "$tmp_root/repo/scripts/"
cd "$tmp_root/repo"
git init -q
git config user.name "Release Notes Test"
git config user.email "release-notes-test@example.invalid"
git remote add origin https://github.com/example/release-notes-test.git
export TMPDIR="$tmp_root/tmp"

fail() { echo "release-notes regression: $*" >&2; exit 1; }
assert_contains() { printf '%s\n' "$1" | grep -Fq -- "$2" || fail "expected output to contain: $2"; }
assert_not_contains() { printf '%s\n' "$1" | grep -Fq -- "$2" && fail "unexpected output: $2" || true; }
commit() {
  git add tasks
  if ! git diff --cached --quiet; then git commit -qm "$1"; fi
  git tag "$2"
}
run_notes() {
  env -u GITHUB_REPOSITORY -u GITHUB_SERVER_URL bash scripts/release-notes.sh "$@"
}

cat > tasks/README.md <<'TXT'
fixture
TXT
git add tasks/README.md
git commit -qm "empty baseline"
git tag v0.1.9

cat > tasks/backlog/001-first.md <<'TXT'
---
id: 001
status: done
---
# First task
TXT
cat > tasks/backlog/002-cancelled.md <<'TXT'
---
id: 002
status: cancelled
---
# Cancelled task
TXT
cat > tasks/DONE.md <<'TXT'
- 003 | 2026-10-30 | done | old | Archived task
TXT
commit "first completed work" v0.1.10

empty_previous=$(run_notes v0.1.10)
assert_contains "$empty_previous" "- 001 — First task"
assert_contains "$empty_previous" "- 003 — Archived task"
assert_not_contains "$empty_previous" "Cancelled task"
assert_contains "$empty_previous" "https://github.com/example/release-notes-test/compare/v0.1.9...v0.1.10"

cat > tasks/backlog/002-cancelled.md <<'TXT'
---
id: 002
status: done
---
# Newly done task
TXT
commit "another completed task" v0.1.11
new_only=$(run_notes v0.1.11)
assert_contains "$new_only" "- 002 — Newly done task"
assert_not_contains "$new_only" "- 001 — First task"
assert_not_contains "$new_only" "- 003 — Archived task"
assert_contains "$(run_notes v0.1.11 v0.1.10)" "- 002 — Newly done task"

cat > tasks/backlog/002-cancelled.md <<'TXT'
---
id: 002
status: done
---
# Newly done task
TXT
commit "no new completed work" v0.1.12
no_new=$(run_notes v0.1.12)
assert_contains "$no_new" "Новых завершённых задач после v0.1.11 нет."
assert_contains "$(run_notes v0.1.9)" "Все завершённые задачи:"

for invalid in HEAD v00.1.1 v0.1.10-rc.1; do
  if run_notes "$invalid" >/dev/null 2>&1; then fail "accepted invalid tag: $invalid"; fi
done
if run_notes v0.1.10 v0.1.10 >/dev/null 2>&1; then fail "accepted equal previous tag"; fi
if run_notes v0.1.10 v0.1.11 >/dev/null 2>&1; then fail "accepted newer previous tag"; fi

echo "release-notes regression scenarios passed"
