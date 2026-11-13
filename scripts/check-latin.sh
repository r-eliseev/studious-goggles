#!/usr/bin/env bash
# "Latin only" check (owner's decision of 2026-11-09, docs/conventions.md, "Language").
# Fails if a checked text file contains a letter of a non-Latin script (Cyrillic, Greek, CJK and others).
# Symbols and letters of the Common/Inherited scripts (×, →, —, ≈, µ) are allowed. Binary files are skipped.
# Usage: scripts/check-latin.sh [path ...]   without arguments the paths of LATIN_PATHS below are checked.
cd "$(dirname "$0")/.."
export LC_ALL=C.UTF-8

# Checked paths. Task 268 and its subtasks add a path here once it is translated.
LATIN_PATHS=(AGENTS.md CLAUDE.md README.md docs .claude .github poc)

# Exceptions: a path (exact, relative to the repository root) and the reason. Only test data that
# verifies non-English input may be listed here.
LATIN_EXCEPTIONS=()

if [ $# -gt 0 ]; then paths=("$@"); else paths=("${LATIN_PATHS[@]}"); fi
fail=0
while IFS= read -r f; do
  [ -f "$f" ] || continue
  skip=0
  for e in "${LATIN_EXCEPTIONS[@]}"; do [ "$f" = "${e%% *}" ] && skip=1; done
  [ $skip -eq 1 ] && continue
  hits=$(grep -nIP '(?=\p{L})(?!\p{Latin}|\p{Common}|\p{Inherited})' "$f" | head -3)
  if [ -n "$hits" ]; then
    while IFS= read -r h; do echo "ERROR: $f:${h%%:*}: non-Latin letters (everything in the repository is in English, docs/conventions.md)"; done <<< "$hits"
    fail=1
  fi
done < <(for p in "${paths[@]}"; do
  if git ls-files --error-unmatch "$p" >/dev/null 2>&1 || [ -d "$p" ]; then
    git ls-files -co --exclude-standard -- "$p"
  else
    [ -e "$p" ] && echo "$p"
  fi
done | sort -u)
exit $fail
