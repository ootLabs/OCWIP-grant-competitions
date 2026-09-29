#!/usr/bin/env python3
"""Talk to the running stack over HTTP, the way a browser would.

Unit tests prove that each piece works alone. This proves the three containers
actually see each other: the API answers, it reaches PostgreSQL, and the front
page renders. That is the failure this catches, and no unit test can.

Usage:
    docker compose up -d
    python scripts/smoke_test.py

Exits 1 on the first failed check. Standard library only.
"""

from __future__ import annotations

import json
import os
import socket
import ssl
import sys
import time
import urllib.error
import urllib.request

BACKEND = os.environ.get("SMOKE_BACKEND_URL") or f"http://localhost:{os.environ.get('BACKEND_PORT', '8080')}"
FRONTEND = os.environ.get("SMOKE_FRONTEND_URL") or f"http://localhost:{os.environ.get('FRONTEND_PORT', '3000')}"

# The production compose file behind Caddy (T-111): SMOKE_BACKEND_URL and
# SMOKE_FRONTEND_URL point at https://<domain>/api and https://<domain>,
# SMOKE_INSECURE=1 accepts Caddy's internal certificate on a test machine,
# SMOKE_RESOLVE=<name>:<address> reaches a test domain without /etc/hosts,
# and SMOKE_EXPECT_NO_OPENAPI=1 checks the API document is not served.
CONTEXT = ssl._create_unverified_context() if os.environ.get("SMOKE_INSECURE") == "1" else None

if resolve := os.environ.get("SMOKE_RESOLVE"):
    _name, _address = resolve.split(":", 1)
    _original = socket.getaddrinfo

    def _getaddrinfo(host, *args, **kwargs):
        return _original(_address if host == _name else host, *args, **kwargs)

    socket.getaddrinfo = _getaddrinfo

# The API and Next.js both compile on first request, so the first call is slow.
TIMEOUT_SECONDS = 120


def get(url: str, timeout: int = 10) -> tuple[int, str]:
    request = urllib.request.Request(url, headers={"Accept": "*/*"})
    with urllib.request.urlopen(request, timeout=timeout, context=CONTEXT) as response:
        return response.status, response.read().decode("utf-8", errors="replace")


def wait_for(url: str, label: str) -> None:
    deadline = time.time() + TIMEOUT_SECONDS
    last_error = "no attempt made"
    while time.time() < deadline:
        try:
            status, _ = get(url)
            if status == 200:
                print(f"  up: {label}")
                return
            last_error = f"HTTP {status}"
        except (urllib.error.URLError, TimeoutError, ConnectionError) as error:
            last_error = str(error)
        time.sleep(2)
    fail(f"{label} did not come up within {TIMEOUT_SECONDS}s ({last_error})")


def fail(message: str) -> None:
    print(f"FAIL: {message}")
    print("Container logs usually say why: docker compose logs --tail 50")
    sys.exit(1)


def main() -> int:
    print("Waiting for the stack.")
    wait_for(f"{BACKEND}/health", "backend")
    wait_for(FRONTEND, "frontend")

    print("Checking the database probe.")
    try:
        status, body = get(f"{BACKEND}/health/db", timeout=30)
    except urllib.error.HTTPError as error:
        fail(f"database probe returned HTTP {error.code}, so the API cannot reach Postgres")
        return 1

    if status != 200:
        fail(f"database probe returned HTTP {status}")

    payload = json.loads(body)
    if payload.get("database") != "reachable":
        fail(f"database probe answered but reported {payload!r}")
    print("  ok: backend reaches PostgreSQL")

    print("Checking the front page.")
    _, page = get(FRONTEND, timeout=60)
    if "OCWIP" not in page:
        fail("front page rendered without the expected content")
    print("  ok: front page renders")

    if os.environ.get("SMOKE_EXPECT_NO_OPENAPI") == "1":
        print("Checking that the API document is not served.")
        try:
            status, _ = get(f"{BACKEND}/openapi/v1.json")
        except urllib.error.HTTPError as error:
            status = error.code
        if status != 404:
            fail(f"/openapi/v1.json answered HTTP {status}; outside Development it must not exist")
        print("  ok: no /openapi outside Development")

    print("Smoke test passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
