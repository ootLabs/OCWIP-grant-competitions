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
import sys
import time
import urllib.error
import urllib.request

BACKEND = f"http://localhost:{os.environ.get('BACKEND_PORT', '8080')}"
FRONTEND = f"http://localhost:{os.environ.get('FRONTEND_PORT', '3000')}"

# The API and Next.js both compile on first request, so the first call is slow.
TIMEOUT_SECONDS = 120


def get(url: str, timeout: int = 10) -> tuple[int, str]:
    request = urllib.request.Request(url, headers={"Accept": "*/*"})
    with urllib.request.urlopen(request, timeout=timeout) as response:
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

    print("Smoke test passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
