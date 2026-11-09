#!/usr/bin/env bash
# Regression of scripts/check-latin.sh: Latin text with symbols passes, Cyrillic, Greek and CJK fail.
set -euo pipefail
repo_root=$(cd "$(dirname "$0")/../.." && pwd)
tmp_root="$repo_root/tmp/check-latin-test-$$"
trap 'rm -rf "$tmp_root"' EXIT
mkdir -p "$tmp_root"
fail() { echo "check-latin regression: $*" >&2; exit 1; }

printf 'Plain text with symbols: × → — ≈ µ é ñ ß ø.\n' > "$tmp_root/ok.md"
bash "$repo_root/scripts/check-latin.sh" "$tmp_root/ok.md" >/dev/null || fail "Latin text with symbols must pass"

for sample in 'Привет' 'Δelta' '中文'; do
  printf 'line one\n%s\n' "$sample" > "$tmp_root/bad.md"
  if out=$(bash "$repo_root/scripts/check-latin.sh" "$tmp_root/bad.md"); then fail "'$sample' must fail"; fi
  printf '%s\n' "$out" | grep -Fq 'bad.md:2:' || fail "the error must name line 2 for '$sample': $out"
done

printf '\x89PNG\r\n\x1a\n\x00\x00\xd0\x9f\xd1\x80\n' > "$tmp_root/image.png"
bash "$repo_root/scripts/check-latin.sh" "$tmp_root/image.png" >/dev/null || fail "binary files must be skipped"
echo "check-latin regression: ok"
