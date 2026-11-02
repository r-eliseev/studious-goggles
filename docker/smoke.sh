#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"
mkdir -p tmp
smoke_dir="$(mktemp -d "$repo_root/tmp/docker-smoke.XXXXXX")"
project="imagegen-smoke-$$"
provider_port="$(python3 -c 'import socket; s=socket.socket(); s.bind(("127.0.0.1", 0)); print(s.getsockname()[1]); s.close()')"
provider_url="http://localhost:18080/v1/"
fake_provider="http://127.0.0.1:${provider_port}"
export IMAGEGEN_WEB_PASSWORD="docker-smoke-temporary-password"
export SMOKE_PROVIDER_CONFIG="$smoke_dir/providers.json"
export SMOKE_PROVIDER_PORT="$provider_port"
export SMOKE_PROVIDER_STATS="$smoke_dir/provider-stats.json"
printf '{"imageSubmits":0}' > "$SMOKE_PROVIDER_STATS"

cleanup() {
    docker compose -p "$project" -f docker-compose.yml -f "$smoke_dir/compose.yml" down --volumes --remove-orphans >/dev/null 2>&1 || true
    rm -rf "$smoke_dir"
}
trap cleanup EXIT

python3 - "$SMOKE_PROVIDER_CONFIG" "$provider_url" "$smoke_dir/compose.yml" "$repo_root" "$SMOKE_PROVIDER_STATS" <<'PY'
import json
import os
import pathlib
import sys

config_path = pathlib.Path(sys.argv[1])
provider_url = sys.argv[2]
compose_path = pathlib.Path(sys.argv[3])
repo_root = pathlib.Path(sys.argv[4])
stats_path = pathlib.Path(sys.argv[5])
providers = {
    "providers": [
        {
            "id": "openrouter",
            "kind": "openAiCompatible",
            "displayName": "Local smoke provider",
            "baseAddress": provider_url,
            "timeoutSeconds": 30,
            "imageResponseFormat": "base64",
            "imageOutputFormat": "png",
            "imageModels": [{"id": "smoke-image", "name": "Smoke Image", "supportsSeed": False, "maxImagesPerRequest": 1}],
            "chatModels": [{"id": "smoke-chat", "name": "Smoke Chat", "contextLength": 4096}],
        },
        {"id": "fal", "kind": "openAiCompatible", "displayName": "Local smoke provider", "baseAddress": provider_url},
        {"id": "together", "kind": "openAiCompatible", "displayName": "Local smoke provider", "baseAddress": provider_url},
    ]
}
config_path.write_text(json.dumps(providers))
compose = {
    "services": {
        "imagegen": {
            "ports": [f"127.0.0.1:{os.environ['SMOKE_PROVIDER_PORT']}:18080"],
            "volumes": [f"{config_path}:/data/providers.json:ro"],
        },
        "smoke-provider": {
            "image": "python:3.13-slim",
            "network_mode": "service:imagegen",
            "depends_on": ["imagegen"],
            "volumes": [
                f"{repo_root / 'docker' / 'smoke_provider.py'}:/smoke_provider.py:ro",
                f"{stats_path}:/stats.json",
            ],
            "command": ["python", "/smoke_provider.py", "--port", "18080", "--stats-file", "/stats.json"],
        },
    }
}
compose_path.write_text(json.dumps(compose))
PY

compose=(docker compose -p "$project" -f docker-compose.yml -f "$smoke_dir/compose.yml")
"${compose[@]}" build
"${compose[@]}" up -d

for _ in $(seq 1 60); do
    if curl --fail --silent http://127.0.0.1:8080/api/health >/dev/null; then
        break
    fi
    sleep 1
done
curl --fail --silent http://127.0.0.1:8080/api/health >/dev/null

python3 docker/smoke_client.py initial \
    --fake-provider "$fake_provider" \
    --cookie-file "$smoke_dir/cookies.txt" \
    --state-file "$smoke_dir/state.json"
"${compose[@]}" down
"${compose[@]}" up -d

for _ in $(seq 1 60); do
    if curl --fail --silent http://127.0.0.1:8080/api/health >/dev/null; then
        break
    fi
    sleep 1
done
curl --fail --silent http://127.0.0.1:8080/api/health >/dev/null

python3 docker/smoke_client.py verify \
    --fake-provider "$fake_provider" \
    --cookie-file "$smoke_dir/cookies.txt" \
    --state-file "$smoke_dir/state.json"
