#!/usr/bin/env bash
# Runs the probe with a local model served by Ollama on 127.0.0.1:11434.
# Usage: run-ollama.sh <ollama-model> <label> <runs>
set -euo pipefail
model=$1; label=$2; runs=$3
here=$(cd "$(dirname "$0")" && pwd)
out="$here/runs/$label"; mkdir -p "$out"
for f in "$here"/inputs/*.md; do
  n=$(basename "$f" .md)
  for r in $(seq 1 "$runs"); do
    dst="$out/$n-$r.md"; [ -s "$dst" ] && continue
    python3 -I - "$model" "$here/prompt.txt" "$f" "$r" > "$dst" <<'PY'
import json, sys, time, urllib.request
model, prompt_path, spec_path, seed = sys.argv[1:5]
body = {"model": model, "stream": False,
        "messages": [{"role": "system", "content": open(prompt_path, encoding="utf-8").read()},
                     {"role": "user", "content": open(spec_path, encoding="utf-8").read()}],
        "options": {"temperature": 0.8, "seed": int(seed), "num_ctx": 16384, "num_predict": 1200}}
t = time.time()
req = urllib.request.Request("http://127.0.0.1:11434/api/chat", json.dumps(body).encode(), {"Content-Type": "application/json"})
d = json.load(urllib.request.urlopen(req, timeout=3600))
print(d["message"]["content"])
print(f"\n<!-- seconds={time.time()-t:.0f} prompt_tokens={d.get('prompt_eval_count')} output_tokens={d.get('eval_count')} cost_usd=0 -->")
PY
    echo "$label $n-$r $(grep -o 'seconds=[0-9]*' "$dst")"
  done
done
