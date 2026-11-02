#!/usr/bin/env python3
"""HTTP client used by the Docker restart smoke test; it never contacts real providers."""

from __future__ import annotations

import argparse
import http.cookiejar
import json
import os
import pathlib
import time
import urllib.error
import urllib.request


def open_request(opener: urllib.request.OpenerDirector, base: str, path: str, method: str = "GET", body: bytes | None = None, headers: dict[str, str] | None = None) -> tuple[int, bytes]:
    request = urllib.request.Request(base + path, data=body, headers=headers or {}, method=method)
    try:
        with opener.open(request, timeout=10) as response:
            return response.status, response.read()
    except urllib.error.HTTPError as error:
        return error.code, error.read()


def json_request(opener: urllib.request.OpenerDirector, base: str, path: str, method: str = "GET", payload: object | None = None, csrf: str | None = None) -> object:
    headers = {"Accept": "application/json"}
    body = None
    if payload is not None:
        body = json.dumps(payload).encode()
        headers["Content-Type"] = "application/json"
    if csrf is not None:
        headers["X-CSRF-TOKEN"] = csrf
    status, response = open_request(opener, base, path, method, body, headers)
    if status < 200 or status >= 300:
        raise RuntimeError(f"{method} {path} returned HTTP {status}: {response[:300]!r}")
    return json.loads(response) if response else None


def multipart_text(text: str, csrf: str) -> bytes:
    boundary = "ImageGenSmokeBoundary"
    body = (
        f"--{boundary}\r\n"
        'Content-Disposition: form-data; name="text"\r\n\r\n'
        f"{text}\r\n"
        f"--{boundary}--\r\n"
    ).encode()
    return body


def authenticated_opener(cookie_path: pathlib.Path) -> tuple[urllib.request.OpenerDirector, http.cookiejar.MozillaCookieJar]:
    jar = http.cookiejar.MozillaCookieJar(str(cookie_path))
    if cookie_path.exists():
        jar.load(ignore_discard=True, ignore_expires=True)
    opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
    return opener, jar


def login(opener: urllib.request.OpenerDirector, base: str, password: str) -> str:
    json_request(opener, base, "/api/auth/login", "POST", {"password": password})
    csrf = json_request(opener, base, "/api/auth/csrf")
    return str(csrf["token"])


def send_message(opener: urllib.request.OpenerDirector, base: str, session_id: str, csrf: str) -> None:
    boundary = "ImageGenSmokeBoundary"
    body = multipart_text("Create the smoke-test image", csrf)
    headers = {
        "Content-Type": f"multipart/form-data; boundary={boundary}",
        "X-CSRF-TOKEN": csrf,
    }
    status, response = open_request(opener, base, f"/api/sessions/{session_id}/messages", "POST", body, headers)
    if status != 200:
        raise RuntimeError(f"message submission returned HTTP {status}: {response[:300]!r}")


def initial_run(base: str, password: str, cookie_path: pathlib.Path, state_path: pathlib.Path, fake_provider: str) -> None:
    opener, jar = authenticated_opener(cookie_path)
    csrf = login(opener, base, password)
    json_request(opener, base, "/api/providers/openrouter/key", "PUT", {"apiKey": "docker-smoke-secret-1234"}, csrf)

    catalog = json_request(opener, base, "/api/models/image")
    assistant_models = json_request(opener, base, "/api/models/assistant")
    if not any(model["providerId"] == "openrouter" and model["id"] == "smoke-image" for model in catalog):
        raise RuntimeError("local smoke image model is missing from the catalog")
    if not any(model["providerId"] == "openrouter" and model["id"] == "smoke-chat" for model in assistant_models):
        raise RuntimeError("local smoke assistant model is missing from the catalog")

    session = json_request(opener, base, "/api/sessions", "POST", {}, csrf)
    session_id = str(session["id"])
    settings = {
        "imageModelIds": ["openrouter:smoke-image"],
        "assistantModelId": "openrouter:smoke-chat",
        "runsPerModel": 1,
        "parameters": {"aspectRatio": None, "resolution": None, "outputFormat": None, "quality": None},
    }
    json_request(opener, base, f"/api/sessions/{session_id}/settings", "PUT", settings, csrf)
    send_message(opener, base, session_id, csrf)

    timeline = None
    generation = None
    for _ in range(60):
        timeline = json_request(opener, base, f"/api/sessions/{session_id}/timeline")
        if timeline["generations"] and timeline["generations"][0]["status"] == "Succeeded":
            generation = timeline["generations"][0]
            break
        time.sleep(1)
    if generation is None:
        raise RuntimeError("smoke generation did not finish")

    status, image = open_request(opener, base, f"/api/generations/{generation['id']}/image")
    if status != 200 or not image.startswith(b"\x89PNG\r\n\x1a\n"):
        raise RuntimeError("generated image could not be read through the protected API")
    status, stats = open_request(opener, fake_provider, "/__stats")
    if status != 200 or json.loads(stats)["imageSubmits"] != 1:
        raise RuntimeError("the fake image provider did not receive exactly one submission")

    jar.save(ignore_discard=True, ignore_expires=True)
    os.chmod(cookie_path, 0o600)
    state_path.write_text(json.dumps({"sessionId": session_id, "generationId": generation["id"], "runs": len(timeline["runs"]), "generations": len(timeline["generations"])}))


def verify_restart(base: str, password: str, cookie_path: pathlib.Path, state_path: pathlib.Path, fake_provider: str) -> None:
    state = json.loads(state_path.read_text())
    opener, _jar = authenticated_opener(cookie_path)
    identity = json_request(opener, base, "/api/auth/me")
    if not identity["authenticated"]:
        raise RuntimeError("the existing auth cookie did not survive container restart")

    providers = json_request(opener, base, "/api/providers")
    openrouter = next(provider for provider in providers if provider["id"] == "openrouter")
    if not openrouter["maskedKey"] or not openrouter["maskedKey"].endswith("1234"):
        raise RuntimeError("the encrypted provider key was not readable after restart")

    timeline = json_request(opener, base, f"/api/sessions/{state['sessionId']}/timeline")
    if len(timeline["runs"]) != state["runs"] or len(timeline["generations"]) != state["generations"]:
        raise RuntimeError("the persisted timeline changed after restart")
    status, image = open_request(opener, base, f"/api/generations/{state['generationId']}/image")
    if status != 200 or not image.startswith(b"\x89PNG\r\n\x1a\n"):
        raise RuntimeError("the generated image did not survive restart")
    status, stats = open_request(opener, fake_provider, "/__stats")
    if status != 200 or json.loads(stats)["imageSubmits"] != 1:
        raise RuntimeError("a provider job was submitted again after restart")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=["initial", "verify"])
    parser.add_argument("--base", default="http://localhost:8080")
    parser.add_argument("--fake-provider", required=True)
    parser.add_argument("--cookie-file", required=True, type=pathlib.Path)
    parser.add_argument("--state-file", required=True, type=pathlib.Path)
    args = parser.parse_args()
    password = os.environ["IMAGEGEN_WEB_PASSWORD"]
    if args.mode == "initial":
        initial_run(args.base, password, args.cookie_file, args.state_file, args.fake_provider)
    else:
        verify_restart(args.base, password, args.cookie_file, args.state_file, args.fake_provider)
    print(f"Docker smoke {args.mode}: passed")


if __name__ == "__main__":
    main()
