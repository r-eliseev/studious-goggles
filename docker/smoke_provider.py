#!/usr/bin/env python3
"""Local OpenAI-compatible stub for the Docker persistence smoke test."""

from __future__ import annotations

import argparse
import base64
import json
import pathlib
import struct
import zlib
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer


def png_pixel() -> bytes:
    def chunk(kind: bytes, data: bytes) -> bytes:
        body = kind + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)

    header = struct.pack(">IIBBBBB", 1, 1, 8, 6, 0, 0, 0)
    image_data = zlib.compress(b"\x00\xff\x00\x00\xff")
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", image_data) + chunk(b"IEND", b"")


class Handler(BaseHTTPRequestHandler):
    image_submits = 0
    stats_file: pathlib.Path
    image = base64.b64encode(png_pixel()).decode("ascii")

    def log_message(self, _format: str, *_args: object) -> None:
        return

    def send_json(self, payload: object, status: int = 200) -> None:
        body = json.dumps(payload, separators=(",", ":")).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self) -> None:
        if self.path == "/__stats":
            self.send_json({"imageSubmits": type(self).image_submits})
        elif self.path.endswith("/models"):
            self.send_json({"data": [{"id": "smoke-chat", "name": "Smoke Chat"}]})
        else:
            self.send_json({"error": {"message": "not found"}}, 404)

    def do_POST(self) -> None:
        length = int(self.headers.get("Content-Length", "0"))
        request = self.rfile.read(length)
        if self.path.endswith("/chat/completions"):
            self.send_json({
                "choices": [{
                    "message": {
                        "role": "assistant",
                        "content": "",
                        "tool_calls": [{
                            "id": "smoke-call",
                            "type": "function",
                            "function": {
                                "name": "generate_images",
                                "arguments": json.dumps({"prompt": "A small red square for a container smoke test"}),
                            },
                        }],
                    },
                    "finish_reason": "tool_calls",
                }],
                "usage": {"prompt_tokens": 1, "completion_tokens": 1},
            })
        elif self.path.endswith("/images/generations"):
            type(self).image_submits += 1
            type(self).stats_file.write_text(json.dumps({"imageSubmits": type(self).image_submits}))
            json.loads(request)
            self.send_json({"data": [{"b64_json": type(self).image}]})
        else:
            self.send_json({"error": {"message": "not found"}}, 404)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, required=True)
    parser.add_argument("--stats-file", type=pathlib.Path, required=True)
    args = parser.parse_args()
    Handler.stats_file = args.stats_file
    Handler.image_submits = json.loads(args.stats_file.read_text())["imageSubmits"]
    ThreadingHTTPServer(("0.0.0.0", args.port), Handler).serve_forever()


if __name__ == "__main__":
    main()
