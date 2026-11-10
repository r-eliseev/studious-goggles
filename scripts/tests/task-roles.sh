#!/usr/bin/env bash
# Regression of the planner/executor rules in scripts/check-tasks.sh (ADR 0032):
# the executor does not change the plan, review needs a report, done needs a review, approval needs a record.
set -euo pipefail
repo_root=$(cd "$(dirname "$0")/../.." && pwd)
tmp_root="$repo_root/tmp/task-roles-test-$$"
trap 'rm -rf "$tmp_root"' EXIT
mkdir -p "$tmp_root/repo/scripts" "$tmp_root/repo/tasks/backlog"
cp "$repo_root/scripts/check-tasks.sh" "$repo_root/scripts/tasks-lib.sh" "$repo_root/scripts/submit-task.sh" "$repo_root/scripts/review-task.sh" "$tmp_root/repo/scripts/"
cd "$tmp_root/repo"
git init -q
git config user.name "Task Roles Test"
git config user.email "task-roles-test@example.invalid"

fail() { echo "task-roles regression: $*" >&2; exit 1; }
task() {  # <file> <status> <assignee> <plan> <report> <review> <comments>
  cat > "$1" <<T
---
id: ${1##*/}
status: $2
assignee: $3
claimed: $([ -n "$3" ] && echo 2026-11-10T00:00:00Z)
created: 2026-11-10
---

# A task

## Description
Something.

## Acceptance criteria
- It works.

## Analysis
Files.

## Plan
$4

## Report
$5

## Review
$6

## Comments
$7
T
  sed -i "s/^id: .*/id: $(basename "$1" | cut -d- -f1)/" "$1"
}
task_status() { awk '/^status:/{print $2; exit}' "$1"; }
expect_pass() { bash scripts/check-tasks.sh >/dev/null 2>&1 || { bash scripts/check-tasks.sh; fail "expected pass: $1"; }; }
expect_fail() { if out=$(bash scripts/check-tasks.sh 2>&1); then fail "expected failure: $1"; fi
  printf '%s\n' "$out" | grep -Fq -- "$2" || fail "expected '$2' for: $1, got: $out"; }

A=tasks/backlog/001-a.md; B=tasks/backlog/002-b.md
task "$A" approved "" "Step one." "" "" "### 2026-11-10, owner
Plan approved."
task "$B" planned "" "Step one." "" "" ""
git add -A && git commit -qm base
export BASE_REF=$(git rev-parse HEAD)
expect_pass "unchanged base"

task "$A" in-progress luna "Step one, changed." "" "" "### 2026-11-10, owner
Plan approved."
expect_fail "the executor changes the plan" "changed while executing"

task "$A" in-progress luna "Step one." "Did step one." "" "### 2026-11-10, owner
Plan approved."
expect_pass "the executor writes only the report"

task "$A" review "" "Step one." "" "" "### 2026-11-10, owner
Plan approved."
expect_fail "review without a report" "'review'"

task "$A" review luna "Step one." "Did step one." "" "### 2026-11-10, owner
Plan approved."
expect_fail "review while still claimed" "still claimed"

task "$A" review "" "Step one." "Did step one." "" "### 2026-11-10, owner
Plan approved."
expect_pass "review with a report"

task "$A" done "" "Step one." "Did step one." "" "### 2026-11-10, owner
Plan approved."
expect_fail "done without a review" "'Review'"

task "$A" done "" "Step one." "Did step one." "### 2026-11-10, planner
Matches the plan." "### 2026-11-10, owner
Plan approved."
expect_pass "done with a review"

task "$B" approved "" "Step one." "" "" ""
expect_fail "approved without an approval record" "approval record"

task "$B" approved "" "Step one." "" "" "### 2026-11-10, owner (recorded by planner)
Plan approved."
expect_pass "approved with an approval record"

git add -A && git commit -qm "B approved"
task "$B" review "" "Step one." "Did step one." "" "### 2026-11-10, owner (recorded by planner)
Plan approved."
git add -A && git commit -qm "B in review"; export BASE_REF=$(git rev-parse HEAD)
task "$B" approved "" "Step one." "Did step one." "" "### 2026-11-10, owner (recorded by planner)
Plan approved.
### 2026-11-10, planner
Fix the second step."
expect_pass "sending a reviewed task back needs no new approval"

task tasks/backlog/003-c.md approved "" "Step one." "" "" ""
expect_fail "a new approved task without an approval record" "approval record"
rm tasks/backlog/003-c.md

# The scripts of the two roles.
task "$A" in-progress luna "Step one." "" "" "### 2026-11-10, owner
Plan approved."
if bash scripts/submit-task.sh 1 >/dev/null 2>&1; then fail "submit without a report must fail"; fi
task "$A" in-progress luna "Step one." "Did step one." "" "### 2026-11-10, owner
Plan approved."
bash scripts/submit-task.sh 1 >/dev/null || fail "submit with a report must pass"
[ "$(task_status "$A")" = review ] || fail "submit must set review"
grep -q '^assignee:$' "$A" || fail "submit must release the claim"
if bash scripts/review-task.sh 1 done >/dev/null 2>&1; then fail "done without a review must fail"; fi
bash scripts/review-task.sh 1 approved >/dev/null || fail "sending back must pass"
[ "$(task_status "$A")" = approved ] || fail "sending back must set approved"

echo "task-roles regression: ok"
