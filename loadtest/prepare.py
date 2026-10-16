#!/usr/bin/env python3
"""Prepare the data the load test (T-118) needs, through the public API.

Creates an operator, one open competition with the 2026 content, and N
applicant accounts that are verified, signed in once and have an entity card.
Writes loadtest/state.json for scenario.js. Nothing is written to the database
directly: accounts register through POST /register and follow the link from
the verification mail, which is read from Mailpit, as a person would.

Usage (the stack, Mailpit and a high RATE_LIMIT_PERMIT_LIMIT are described in
docs/wdrozenie.md, section "Test obciążenia"):

    python loadtest/prepare.py --applicants 150

Environment: LOADTEST_BASE_URL (default https://ocwip.test:8443),
LOADTEST_RESOLVE (name:address, default ocwip.test:127.0.0.1),
LOADTEST_MAILPIT_URL (default http://localhost:8026), LOADTEST_COMPOSE_ARGS
(arguments for `docker compose`, to run grant-role and import-content inside
the backend container). Standard library only.
"""

from __future__ import annotations

import argparse
import concurrent.futures
import http.cookiejar
import json
import os
import re
import socket
import ssl
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timedelta, timezone
from pathlib import Path

BASE = os.environ.get("LOADTEST_BASE_URL", "https://ocwip.test:8443").rstrip("/")
MAILPIT = os.environ.get("LOADTEST_MAILPIT_URL", "http://localhost:8026").rstrip("/")
COMPOSE_ARGS = os.environ.get("LOADTEST_COMPOSE_ARGS", "").split()
PASSWORD = "Load!Mocne-Haslo1"
TURNSTILE_TEST_TOKEN = "XXXX.DUMMY.TOKEN.XXXX"
ROOT = Path(__file__).resolve().parent.parent

if resolve := os.environ.get("LOADTEST_RESOLVE", "ocwip.test:127.0.0.1"):
    _name, _address = resolve.split(":", 1)
    _original = socket.getaddrinfo

    def _getaddrinfo(host, *args, **kwargs):
        return _original(_address if host == _name else host, *args, **kwargs)

    socket.getaddrinfo = _getaddrinfo

_TLS = ssl._create_unverified_context()


class Session:
    """One person's cookie jar."""

    def __init__(self) -> None:
        self.jar = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(self.jar),
            urllib.request.HTTPSHandler(context=_TLS),
        )

    def call(self, method: str, path: str, body: object | None = None) -> tuple[int, object]:
        data = json.dumps(body).encode() if body is not None else None
        headers = {"Accept": "application/json", "X-Turnstile-Token": TURNSTILE_TEST_TOKEN}
        if data is not None:
            headers["Content-Type"] = "application/json"
        request = urllib.request.Request(f"{BASE}/api{path}", data=data, method=method, headers=headers)
        try:
            with self.opener.open(request, timeout=60) as response:
                raw = response.read()
                return response.status, json.loads(raw) if raw else None
        except urllib.error.HTTPError as error:
            raw = error.read()
            try:
                return error.code, json.loads(raw) if raw else None
            except ValueError:
                return error.code, raw.decode("utf-8", errors="replace")[:300]


def expect(result: tuple[int, object], what: str) -> object:
    status, body = result
    if not 200 <= status < 300:
        sys.exit(f"{what}: HTTP {status}: {body}")
    return body


def mailpit_get(path: str) -> object:
    with urllib.request.urlopen(f"{MAILPIT}{path}", timeout=30) as response:
        return json.loads(response.read())


def verification_link(email: str, timeout: float = 60) -> str:
    query = urllib.parse.quote(f'to:"{email}" subject:"Potwierdzenie adresu e-mail"')
    until = time.time() + timeout
    while time.time() < until:
        found = mailpit_get(f"/api/v1/search?query={query}")["messages"]
        if found:
            text = mailpit_get(f"/api/v1/message/{found[0]['ID']}")["Text"]
            match = re.search(r"https?://\S+/verify-email\S*", text)
            if match:
                return match.group(0)
        time.sleep(0.5)
    sys.exit(f"No verification mail to {email} within {timeout:.0f} s")


def register_and_verify(email: str, first: str, last: str) -> Session:
    session = Session()
    consents = [c["version"] for c in session.call("GET", "/public/consents")[1]]
    expect(
        session.call("POST", "/register", {
            "email": email, "password": PASSWORD, "firstName": first, "lastName": last,
            "acceptedConsents": consents,
        }),
        f"register {email}",
    )
    query = urllib.parse.parse_qs(urllib.parse.urlparse(verification_link(email)).query)
    expect(
        session.call("POST", "/verify-email", {"userId": query["userId"][0], "token": query["token"][0]}),
        f"verify {email}",
    )
    return session


def sign_in(session: Session, email: str) -> None:
    expect(session.call("POST", "/login", {"email": email, "password": PASSWORD}), f"login {email}")


def admin(*args: str) -> str:
    dll = os.environ.get("LOADTEST_ADMIN_DLL", "Ocwip.Api.dll")
    command = ["docker", "compose", *COMPOSE_ARGS, "exec", "-T", "backend", "dotnet", dll, *args]
    return subprocess.run(command, cwd=ROOT, check=True, capture_output=True, text=True).stdout


def fresh_nip(index: int) -> str:
    """A NIP with a valid checksum, distinct per applicant (an active card's NIP is unique)."""
    weights = [6, 5, 7, 2, 3, 4, 5, 6, 7]
    seed = 100_000_000 + index * 7919 + int(time.time()) % 1_000_000
    while True:
        digits = [int(c) for c in str(seed)[:9]]
        check = sum(d * w for d, w in zip(digits, weights)) % 11
        if check != 10:
            return "".join(map(str, digits)) + str(check)
        seed += 1


def iso(offset: timedelta) -> str:
    moment = datetime.now(timezone.utc) + offset
    return moment.replace(second=0, microsecond=0).strftime("%Y-%m-%dT%H:%M:%SZ")


def prepare_applicant(run: str, index: int) -> dict[str, str]:
    email = f"load-{run}-{index:03d}@example.org"
    session = register_and_verify(email, "Test", f"Wnioskodawca{index:03d}")
    sign_in(session, email)
    expect(session.call("POST", "/me/entities", {
        "type": "Organisation",
        "name": f"Stowarzyszenie Load {run} {index:03d}",
        "legalForm": "Association",
        "register": "Krs",
        "registerNumber": "0000000001",
        "nip": fresh_nip(index),
        "address": "ul. Testowa 1, 45-000 Opole",
        "phone": "+48 700 100 200",
        "email": "biuro@example.org",
        "bankAccount": "PL73 1111 1111 1111 1111 1111 1111",
        "representatives": [{"firstName": "Anna", "lastName": "Testowa", "function": "Prezeska"}],
    }), f"entity card {email}")
    return {"email": email, "password": PASSWORD}


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--applicants", type=int, default=150)
    parser.add_argument("--window-minutes", type=int, default=120, help="how long the intake stays open from now")
    parser.add_argument("--workers", type=int, default=8)
    parser.add_argument("--out", default=str(Path(__file__).with_name("state.json")))
    args = parser.parse_args()

    run = format(int(time.time()), "x")
    operator = f"load-operator-{run}@example.org"
    session = register_and_verify(operator, "Test", "Operator")
    admin("grant-role", "--email", operator, "--role", "Operator")
    sign_in(session, operator)

    # Open now, closing after the test window and a margin, so the run never waits for a clock.
    competition = expect(session.call("POST", "/competitions", {
        "number": f"LOAD/{run}",
        "title": f"Konkurs testu obciążenia {run}",
        "description": "Konkurs testu obciążenia (T-118).",
        "startDate": iso(timedelta(hours=-1)),
        "endDate": iso(timedelta(minutes=args.window_minutes + 30)),
        "isContinuousIntake": False,
        "maxGrantAmount": 7000,
        "totalPoolAmount": 7000 * args.applicants,
        "formDefinitionId": None,
        "requiresPaperSubmission": False,
        "maxIndirectCostPercent": 10,
        "maxAverageAnnualRevenue": 50000,
        "attachments": [{"title": "Statut", "description": None, "requirement": "Required", "allowedFormats": ["Pdf"]}],
    }), "create competition")
    competition_id = competition["id"]
    admin(
        "import-content", "--competition", competition_id,
        "--application", "seed/forms/application-2026.json",
        "--formal", "seed/evaluation-cards/formal-2026.json",
        "--merit", "seed/evaluation-cards/merit-2026.json",
        "--report", "seed/forms/report-2026.json",
        "--contract", "seed/templates/contract-2026.txt",
    )
    expect(session.call("POST", f"/competitions/{competition_id}/status", {"status": "Published"}), "publish")
    published = expect(session.call("GET", f"/public/competitions/{competition_id}"), "read competition")
    requirement_id = published["attachments"][0]["id"]

    print(f"Competition {competition_id}; preparing {args.applicants} applicants.", flush=True)
    started = time.time()
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
        accounts = list(pool.map(lambda i: prepare_applicant(run, i), range(args.applicants)))
    print(f"Applicants ready in {time.time() - started:.0f} s.", flush=True)

    Path(args.out).write_text(json.dumps({
        "run": run,
        "competitionId": competition_id,
        "requirementId": requirement_id,
        "accounts": accounts,
    }, indent=2), encoding="utf-8")
    print(f"Wrote {args.out}")


if __name__ == "__main__":
    main()
