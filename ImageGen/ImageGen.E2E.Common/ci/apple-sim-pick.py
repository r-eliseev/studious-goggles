#!/usr/bin/env python3
"""Select the newest available iOS runtime and its first supported iPhone type."""

import json
import re
import sys
from pathlib import Path


def version_key(value: str) -> tuple[int, ...]:
    return tuple(int(part) for part in re.findall(r"\d+", value))


def read_json(path: str) -> dict:
    with Path(path).open(encoding="utf-8") as stream:
        return json.load(stream)


def main() -> int:
    if len(sys.argv) not in (3, 4):
        print(f"usage: {sys.argv[0]} <runtimes.json> <devicetypes.json> [runtime-version]", file=sys.stderr)
        return 2

    runtimes = read_json(sys.argv[1]).get("runtimes", [])
    device_types = read_json(sys.argv[2]).get("devicetypes", [])
    requested_version = sys.argv[3] if len(sys.argv) == 4 else None
    requested_prefix = f"{requested_version}." if requested_version else None
    known_ids = {item.get("identifier") for item in device_types}
    candidates = [
        runtime
        for runtime in runtimes
        if runtime.get("isAvailable")
        and runtime.get("platform") == "iOS"
        and (
            requested_version is None
            or runtime.get("version") == requested_version
            or runtime.get("version", "").startswith(requested_prefix)
        )
    ]
    if not candidates:
        if requested_version is None:
            raise SystemExit("no available iOS simulator runtime")
        raise SystemExit(f"no available iOS simulator runtime with version {requested_version}")

    runtime = max(candidates, key=lambda item: version_key(item.get("version", "0")))
    devices = runtime.get("supportedDeviceTypes", [])
    device = next(
        (
            item
            for item in devices
            if item.get("productFamily") == "iPhone"
            and item.get("identifier") in known_ids
        ),
        None,
    )
    if device is None:
        raise SystemExit(f"no available iPhone device type for runtime {runtime.get('name')}")

    print(json.dumps({"runtime": runtime["identifier"], "runtimeName": runtime.get("name"), "deviceType": device["identifier"], "deviceName": device.get("name")}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
