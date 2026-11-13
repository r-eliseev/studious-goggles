#!/usr/bin/env bash
# Documentation checks. Exit code 1 if any check fails.
cd "$(dirname "$0")/.."
fail=0

# 1. Required files
for f in AGENTS.md docs/index.md docs/status.md docs/architecture.md docs/conventions.md docs/tasks.md docs/executor.md .claude/skills/planner/SKILL.md .claude/skills/executor/SKILL.md; do
  [ -f "$f" ] || { echo "ERROR: missing file $f"; fail=1; }
done

# 2. Every file in docs/ is listed in index.md: top-level files by name, nested files by directory name.
#    Excluded: index.md, log/ entries, and _*.md templates.
if [ -f docs/index.md ]; then
  while IFS= read -r f; do
    rel="${f#docs/}"; base="$(basename "$rel")"
    case "$rel" in index.md|log/*) continue ;; esac
    case "$base" in _*) continue ;; esac
    if [ "$rel" = "$base" ]; then key="$base"; else key="${rel%%/*}/"; fi
    grep -qF "$key" docs/index.md || { echo "ERROR: $rel is not listed in docs/index.md"; fail=1; }
  done < <(find docs -type f -name '*.md' | sort)
fi

# 3. Every ADR has a “Status:” line with an allowed value
for f in docs/decisions/*.md; do
  [ -e "$f" ] || continue
  case "$(basename "$f")" in _*) continue ;; esac
  grep -qE '^Status: (proposed|accepted|superseded|rejected)' "$f" \
    || { echo "ERROR: $f has no 'Status:' line with an allowed value"; fail=1; }
done

# 4. Broken relative links to .md files
while IFS= read -r f; do
  dir="$(dirname "$f")"
  while IFS= read -r link; do
    [ -n "$link" ] || continue
    case "$link" in http*|mailto:*) continue ;; esac
    [ -e "$dir/$link" ] || { echo "ERROR: broken link in $f -> $link"; fail=1; }
  done < <(grep -oE '\]\([^)#[:space:]]+\.md(#[^)]*)?\)' "$f" | sed -E 's/^\]\(([^)#]+).*/\1/')
done < <(find . -name '*.md' -not -path './.git/*' -not -path './node_modules/*' -not -path './tmp/*' | sort)

# 5. architecture.md describes architecture, not work progress: no task numbers, tasks/ paths, or status section
if [ -f docs/architecture.md ]; then
  grep -nE '[Зз]адач[^ ]* +[0-9]{3}|[Tt]asks? +[0-9]{3}|tasks/|^#+ +(Состояние|Status|State)' docs/architecture.md | while IFS= read -r line; do
    echo "ERROR: docs/architecture.md:${line%%:*}: work progress (task number, tasks/ path, or status section) belongs in a task report"
  done | grep . && fail=1
fi

# 7. Latin only: everything in the repository is in English (docs/conventions.md, "Language").
#    The checked paths and the exceptions are in scripts/check-latin.sh.
bash scripts/check-latin.sh || fail=1

# 6. Warning: code changed without docs/ or tasks/ changes (for pull-request CI)
base="${BASE_REF:-origin/main}"
if git rev-parse --verify -q "$base" >/dev/null 2>&1; then
  changed="$(git diff --name-only "$base"...HEAD 2>/dev/null)"
  if echo "$changed" | grep -qE '^ImageGen/' && ! echo "$changed" | grep -qE '^(docs|tasks)/'; then
    echo "WARNING: ImageGen/ code changed, but neither docs/ nor tasks/ were updated"
  fi
fi

exit $fail
