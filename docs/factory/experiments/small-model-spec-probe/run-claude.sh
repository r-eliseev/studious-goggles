#!/usr/bin/env bash
# Runs the probe with a Claude model through `claude -p`, no tools, in an empty directory.
# Usage: run-claude.sh <model-alias> <runs> <isolated-dir>
set -euo pipefail
model=$1; runs=$2; iso=$3
here=$(cd "$(dirname "$0")" && pwd)
out="$here/runs/$model"; mkdir -p "$out" "$iso"
prompt=$(cat "$here/prompt.txt")
for f in "$here"/inputs/*.md; do
  n=$(basename "$f" .md)
  for r in $(seq 1 "$runs"); do
    dst="$out/$n-$r.md"; [ -s "$dst" ] && continue
    start=$(date +%s)
    json=$(cd "$iso" && claude -p --model "$model" --tools "" --system-prompt "$prompt" \
      --no-session-persistence --output-format json < "$f")
    end=$(date +%s)
    printf '%s' "$json" | python3 -I -c '
import json,sys
d=json.load(sys.stdin); u=d.get("modelUsage") or {}
cost=sum(v.get("costUSD",0) for v in u.values()); outt=sum(v.get("outputTokens",0) for v in u.values())
print(d.get("result",""))
print(f"\n<!-- seconds={sys.argv[1]} cost_usd={cost:.5f} output_tokens={outt} -->")' "$((end-start))" > "$dst"
    echo "$model $n-$r $((end-start))s"
  done
done
