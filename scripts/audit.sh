#!/usr/bin/env bash
# Staleness signals (entropy control): findings that can be detected mechanically.
# Usage: scripts/audit.sh [claim-age-days] [doc-staleness-days]
#   claim-age-days: age after which a task claim is considered stale (default: 7);
#   doc-staleness-days: how far a document must lag behind ImageGen/ code to be listed (default: 30).
# An agent performs the semantic documentation/code review as described in docs/tasks.md, “Audit”.
# Exit code 1 only when scripts/check.sh fails.
cd "$(dirname "$0")/.."
. scripts/tasks-lib.sh
claim_days="${1:-7}"; doc_days="${2:-30}"
rc=0

echo "== 1. Harness checks (scripts/check.sh)"
bash scripts/check.sh || rc=1

echo; echo "== 2. Tasks claimed longer than $claim_days days (possibly stuck)"
cutoff=$(date -u -d "$claim_days days ago" +%Y-%m-%dT%H:%M:%SZ 2>/dev/null \
  || date -u -v-"${claim_days}"d +%Y-%m-%dT%H:%M:%SZ 2>/dev/null)
if [ -n "$cutoff" ]; then
  found=0
  while IFS= read -r f; do
    as=$(task_field "$f" assignee); cl=$(task_field "$f" claimed)
    [ -n "$as" ] && [ -n "$cl" ] || continue
    if [[ "$cl" < "$cutoff" ]]; then echo "  $f: $as, claimed since $cl"; found=1; fi
  done < <(find tasks -type f -name '[0-9][0-9]*-*.md' | sort)
  [ $found -eq 1 ] || echo "  none"
else
  echo "  skipped: could not calculate the date (GNU or BSD date is required)"
fi

echo; echo "== 3. Tasks waiting for the planner or the owner (todo, planned, blocked, review)"
waiting=$(bash scripts/list-tasks.sh todo; bash scripts/list-tasks.sh planned; bash scripts/list-tasks.sh blocked; bash scripts/list-tasks.sh review)
if [ -n "$waiting" ]; then echo "$waiting" | sed 's/^/  /'; else echo "  none"; fi

echo; echo "== 4. Documents that may be stale (lagging behind ImageGen/ code by at least $doc_days days)"
if git rev-parse --git-dir >/dev/null 2>&1; then
  code_t=$(git log -1 --format=%ct -- "ImageGen" 2>/dev/null)
  if [ -n "$code_t" ]; then
    found=0
    for f in docs/architecture.md docs/conventions.md docs/product.md docs/specs/*.md; do
      [ -e "$f" ] || continue
      case "$(basename "$f")" in _*) continue ;; esac
      doc_t=$(git log -1 --format=%ct -- "$f" 2>/dev/null)
      [ -n "$doc_t" ] || continue
      age=$(( (code_t - doc_t) / 86400 ))
      if [ "$age" -ge "$doc_days" ]; then echo "  $f: lags behind code by $age days"; found=1; fi
    done
    [ $found -eq 1 ] || echo "  none"
  else
    echo "  no commit history for ImageGen/: skipped"
  fi
else
  echo "  not a git repository: skipped"
fi

echo; echo "== 5. ADRs awaiting a decision (status ‘proposed’)"
found=0
for f in docs/decisions/*.md; do
  [ -e "$f" ] || continue
  case "$(basename "$f")" in _*) continue ;; esac
  if grep -q '^Status: proposed' "$f"; then echo "  $f"; found=1; fi
done
[ $found -eq 1 ] || echo "  none"

echo; echo "Semantic documentation/code review: docs/tasks.md, ‘Audit’ section"
exit $rc
