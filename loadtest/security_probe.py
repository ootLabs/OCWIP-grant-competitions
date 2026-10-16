#!/usr/bin/env python3
"""Live probes of the rules in AGENTS.md "Bezpieczeństwo", against a running stack (T-119).

Uses the accounts and applications left by prepare.py and scenario.js, so
run those first. Every probe states the rule it protects; the exit code is 1
when any rule is broken. It sends nothing destructive: refused writes are the
point, and an accepted one is reported as a failure.

    python loadtest/security_probe.py            # all probes
    python loadtest/security_probe.py --rate     # also the login rate limit (needs RATE_LIMIT_PERMIT_LIMIT=10)

Environment: the same LOADTEST_* variables as prepare.py. Standard library only.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from prepare import BASE, PASSWORD, Session, sign_in  # noqa: E402

failures: list[str] = []


def check(rule: str, ok: bool, detail: str) -> None:
    print(f"  {'ok  ' if ok else 'FAIL'} {rule}: {detail}")
    if not ok:
        failures.append(f"{rule}: {detail}")


def refused(status: int) -> bool:
    return status in (401, 403, 404)


def raw(method: str, path: str, data: bytes, content_type: str = "application/json") -> int:
    """A request with a body the API cannot read, from a person who is not signed in."""
    import urllib.request
    import urllib.error

    session = Session()
    request = urllib.request.Request(
        f"{BASE}/api{path}", data=data, method=method,
        headers={"Content-Type": content_type, "X-Turnstile-Token": "XXXX.DUMMY.TOKEN.XXXX"},
    )
    try:
        with session.opener.open(request, timeout=30) as response:
            return response.status
    except urllib.error.HTTPError as error:
        return error.code


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--state", default=str(Path(__file__).with_name("state.json")))
    parser.add_argument("--rate", action="store_true")
    args = parser.parse_args()
    state = json.loads(Path(args.state).read_text(encoding="utf-8"))
    competition = state["competitionId"]

    a, b = Session(), Session()
    sign_in(a, state["accounts"][0]["email"])
    sign_in(b, state["accounts"][1]["email"])
    mine_a = a.call("GET", "/applications")[1]
    mine_b = b.call("GET", "/applications")[1]
    app_a, app_b = mine_a[0]["id"], mine_b[0]["id"]
    check("applicant sees only own applications", all(x["id"] != app_a for x in mine_b) and len(mine_b) >= 1,
          f"B lists {len(mine_b)} application(s), none of them A's")

    print("Rule 2: an applicant never sees somebody else's application")
    foreign_reads = [
        ("GET", f"/applications/{app_a}"),
        ("GET", f"/applications/{app_a}/pdf"),
        ("GET", f"/applications/{app_a}/confirmation"),
        ("GET", f"/applications/{app_a}/attachments"),
        ("GET", f"/applications/{app_a}/corrections"),
        ("GET", f"/applications/{app_a}/form-definition"),
        ("GET", f"/applications/{app_a}/evaluations"),
        ("GET", f"/applications/{app_a}/evaluation-cards"),
        ("GET", f"/applications/{app_a}/contract"),
    ]
    for method, path in foreign_reads:
        status, _ = b.call(method, path)
        check("foreign read", refused(status), f"{method} {path.replace(app_a, '<A>')} -> {status}")
    attachment = a.call("GET", f"/applications/{app_a}/attachments")[1][0]["id"]
    status, _ = b.call("GET", f"/attachments/{attachment}")
    check("foreign attachment download", refused(status), f"GET /attachments/<A's> -> {status}")

    foreign_writes = [
        ("PUT", f"/applications/{app_a}", {"answers": {"tytul_projektu": "przejęty"}}),
        ("POST", f"/applications/{app_a}/submit", None),
        ("DELETE", f"/applications/{app_a}", None),
        ("POST", f"/attachments/{attachment}/withdraw", None),
        ("POST", f"/applications/{app_a}/resignation", {}),
        ("POST", f"/applications/{app_a}/report", {}),
    ]
    for method, path, body in foreign_writes:
        status, _ = b.call(method, path, body)
        check("foreign write", refused(status), f"{method} {path.replace(app_a, '<A>')} -> {status}")
    after = a.call("GET", f"/applications/{app_a}")[1]
    check("foreign write left no trace", after.get("answers", {}).get("tytul_projektu") != "przejęty", "A's title unchanged")

    print("Rule 1: no rule means no access (an applicant on operator and expert routes)")
    operator_routes = [
        ("GET", f"/competitions/{competition}/applications"),
        ("GET", f"/competitions/{competition}/applications/{app_a}"),
        ("GET", f"/competitions/{competition}/applications/export/csv"),
        ("GET", f"/competitions/{competition}/ranking"),
        ("GET", f"/competitions/{competition}/reports"),
        ("GET", f"/competitions/{competition}/experts"),
        ("GET", "/accounts/team"),
        ("GET", "/accounts/operators"),
        ("GET", "/access-requests/escalated"),
        ("GET", "/reviewers"),
        ("GET", "/reviewer/applications"),
        ("POST", f"/competitions/{competition}/status", {"status": "Closed"}),
        ("POST", f"/competitions/{competition}/results/approve", None),
        ("POST", f"/applications/{app_b}/return", {"note": "x"}),
        ("POST", f"/applications/{app_b}/assignments", {}),
        ("POST", "/competitions", {}),
        ("PUT", f"/competitions/{competition}", {}),
        ("POST", f"/competitions/{competition}/contracts/bundle", None),
    ]
    for method, path, *body in operator_routes:
        status, _ = b.call(method, path, body[0] if body else None)
        check("applicant on staff route", refused(status), f"{method} {path.replace(competition, '<C>').replace(app_a, '<A>').replace(app_b, '<B>')} -> {status}")
    state_after = b.call("GET", f"/public/competitions/{competition}")
    check("competition untouched", state_after[0] == 200, f"public page still answers (GET -> {state_after[0]})")

    print("Rule 1: anonymous visitors")
    anonymous = Session()
    for method, path in [
        ("GET", "/applications"), ("GET", f"/applications/{app_a}"), ("GET", f"/attachments/{attachment}"),
        ("GET", f"/competitions/{competition}/applications"), ("GET", "/me"), ("GET", "/me/entities"),
        ("GET", "/accounts/team"), ("POST", f"/applications/{app_a}/submit"), ("PUT", f"/applications/{app_a}"),
    ]:
        status, _ = anonymous.call(method, path, {} if method in ("PUT", "POST") else None)
        check("anonymous", status == 401, f"{method} {path.replace(app_a, '<A>').replace(attachment, '<att>')} -> {status}")

    print("Rule 3: the same answer for a taken and a free address")
    taken = Session().call("POST", "/forgot-password", {"email": state["accounts"][2]["email"]})
    free = Session().call("POST", "/forgot-password", {"email": "nikt-takiego@example.org"})
    check("forgot-password does not reveal the account", taken[0] == free[0] and taken[1] == free[1], f"taken {taken[0]}, free {free[0]}")

    print("Malformed bodies are a 400, not a 500 (monitoring counts every 5xx as an outage)")
    for label, body, content_type in [
        ("truncated JSON", b'{"email": ', "application/json"),
        ("wrong type", b'{"email": 5}', "application/json"),
        ("bytes outside UTF-8", b'{"email": "\xff\xfe"}', "application/json"),
        ("not JSON at all", b"<xml/>", "text/xml"),
    ]:
        status = raw("POST", "/login", body, content_type)
        check("malformed body", 400 <= status < 500, f"POST /login {label} -> {status}")

    print("Server technology is not named in the headers (P5-02)")
    import urllib.request
    request = urllib.request.Request(f"{BASE}/api/public/consents")
    with a.opener.open(request, timeout=30) as response:
        headers = {k.lower(): v for k, v in response.headers.items()}
    leaked = [h for h in ("server", "x-powered-by", "x-aspnet-version") if h in headers]
    check("no technology headers", not leaked, f"present: {leaked or 'none'}")
    check("API answers are not cached", "no-store" in headers.get("cache-control", ""), headers.get("cache-control", "missing"))

    if args.rate:
        print("Brute force: the login limit (needs RATE_LIMIT_PERMIT_LIMIT=10)")
        codes = [Session().call("POST", "/login", {"email": "nikt-takiego@example.org", "password": f"zle-{i}"})[0] for i in range(25)]
        check("login is rate limited", 429 in codes, f"first 429 at attempt {codes.index(429) + 1 if 429 in codes else 'never'}; codes {sorted(set(codes))}")

    print()
    if failures:
        print(f"{len(failures)} rule(s) broken:")
        for failure in failures:
            print(f"  - {failure}")
        sys.exit(1)
    print("All probes passed.")


if __name__ == "__main__":
    main()
