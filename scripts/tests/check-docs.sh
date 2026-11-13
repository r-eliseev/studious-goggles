#!/usr/bin/env bash
# Regression for scripts/check-docs.sh: role skills needed to resume work must exist.
set -euo pipefail
repo_root=$(cd "$(dirname "$0")/../.." && pwd)
tmp_root="$repo_root/tmp/check-docs-test-$$"
trap 'rm -rf "$tmp_root"' EXIT
mkdir -p "$tmp_root/repo/scripts" "$tmp_root/repo/docs" "$tmp_root/repo/.claude/skills/planner" "$tmp_root/repo/.claude/skills/executor"
cp "$repo_root/scripts/check-docs.sh" "$repo_root/scripts/check-latin.sh" "$tmp_root/repo/scripts/"
cd "$tmp_root/repo"
git init -q
for f in AGENTS.md docs/status.md docs/architecture.md docs/conventions.md docs/tasks.md docs/executor.md .claude/skills/planner/SKILL.md; do
  mkdir -p "$(dirname "$f")"
  : > "$f"
done
cat > docs/index.md <<'EOF'
architecture.md status.md conventions.md tasks.md executor.md
EOF
printf 'executor skill\n' > .claude/skills/executor/SKILL.md

bash scripts/check-docs.sh >/dev/null || { echo 'check-docs regression: valid executor skill should pass' >&2; exit 1; }
rm .claude/skills/executor/SKILL.md
if out=$(bash scripts/check-docs.sh 2>&1); then
  echo 'check-docs regression: missing executor skill must fail' >&2
  exit 1
fi
printf '%s\n' "$out" | grep -Fq 'ERROR: missing file .claude/skills/executor/SKILL.md' || {
  echo "check-docs regression: expected executor skill error, got: $out" >&2
  exit 1
}
echo 'check-docs regression: ok'
