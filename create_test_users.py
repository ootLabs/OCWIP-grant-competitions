#!/usr/bin/env python3
"""LOCAL-ONLY helper. Nie czesc repo, nigdy nie trafia do commita.

Zaklada po jednym koncie testowym na kazda z trzech ról systemu (Operator,
Wnioskodawca, Recenzent), przechodzac PRAWDZIWY flow: POST /register, odczyt
linku weryfikacyjnego z logow backendu (Development loguje cala tresc maila),
POST /verify-email, a na koniec `grant-role` dla ról innych niz domyslny
Applicant.

Wymaga odpalonego stacku (docker compose up -d) i uruchomienia TEGO skryptu z
katalogu glownego repo (albo dowolnego jego podkatalogu) - docker compose i tak
szuka docker-compose.yml, wchodzac w gore od biezacego katalogu.

Usage:
    python3 create_test_users.py
"""

from __future__ import annotations

import json
import re
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

REPO_ROOT = Path.cwd()
API_BASE = "http://localhost:8080"
PASSWORD = "TestHaslo123!"

# (email, first name, last name, role) - Applicant is what /register grants by
# default, so it needs no grant-role call afterwards.
USERS = [
    ("operator@example.org", "Anna", "Operator", "Operator"),
    ("wnioskodawca@example.org", "Marek", "Wnioskodawca", "Applicant"),
    ("recenzent@example.org", "Ewa", "Recenzent", "Reviewer"),
]

LINK_RE = re.compile(r"http://localhost:3000/verify-email\?[^\s]+")


def current_consent_versions() -> list[str]:
    with urllib.request.urlopen(f"{API_BASE}/public/consents") as response:
        documents = json.loads(response.read().decode("utf-8"))
    return [document["version"] for document in documents]


def register(email: str, first_name: str, last_name: str, consents: list[str]) -> None:
    body = json.dumps({
        "email": email,
        "password": PASSWORD,
        "firstName": first_name,
        "lastName": last_name,
        "acceptedConsents": consents,
    }).encode("utf-8")
    request = urllib.request.Request(
        f"{API_BASE}/register",
        data=body,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(request) as response:
            print(f"  register {email}: {response.status}")
    except urllib.error.HTTPError as error:
        print(f"  register {email}: {error.status} {error.read().decode('utf-8')}")


def backend_logs() -> str:
    completed = subprocess.run(
        ["docker", "compose", "logs", "backend"],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=False,
    )
    return completed.stdout


def find_verification_link(logs: str, email: str) -> str | None:
    # Find the LAST "To: <email>" occurrence, then the first verify-email link
    # after it - matches the DEV EMAIL block layout in EmailSenderService.cs.
    marker = f"To: {email}"
    index = logs.rfind(marker)
    if index == -1:
        return None
    tail = logs[index:index + 2000]
    match = LINK_RE.search(tail)
    return match.group(0) if match else None


def verify(email: str) -> bool:
    logs = backend_logs()
    link = find_verification_link(logs, email)
    if link is None:
        print(f"  verify {email}: link not found in backend logs yet")
        return False

    parsed = urllib.parse.urlparse(link)
    params = urllib.parse.parse_qs(parsed.query)
    user_id = params["userId"][0]
    token = params["token"][0]

    body = json.dumps({"userId": user_id, "token": token}).encode("utf-8")
    request = urllib.request.Request(
        f"{API_BASE}/verify-email",
        data=body,
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(request) as response:
            print(f"  verify {email}: {response.status}")
            return True
    except urllib.error.HTTPError as error:
        print(f"  verify {email}: {error.status} {error.read().decode('utf-8')}")
        return False


def grant_role(email: str, role: str) -> None:
    completed = subprocess.run(
        [
            "docker", "compose", "exec", "-T", "backend",
            "dotnet", "run", "--project", "src/Ocwip.Api/Ocwip.Api.csproj",
            "--no-launch-profile", "--",
            "grant-role", "--email", email, "--role", role,
        ],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        check=False,
    )
    output = (completed.stdout + completed.stderr).strip()
    print(f"  grant-role {email} -> {role}: {output or completed.returncode}")


def main() -> None:
    print("Rejestracja kont testowych.")
    consents = current_consent_versions()
    for email, first_name, last_name, _role in USERS:
        register(email, first_name, last_name, consents)

    print("Czekam chwile, zeby logi backendu sie zapisaly.")
    time.sleep(2)

    print("Weryfikacja adresow e-mail.")
    for email, _first_name, _last_name, _role in USERS:
        ok = verify(email)
        if not ok:
            print(f"  ponawiam za 2s dla {email}")
            time.sleep(2)
            verify(email)

    print("Nadawanie rol (Applicant to juz domyslna rola po rejestracji).")
    for email, _first_name, _last_name, role in USERS:
        if role != "Applicant":
            grant_role(email, role)

    print()
    print("Gotowe. Dane logowania (LOKALNE, TYLKO DEV, haslo takie samo dla wszystkich):")
    for email, _first_name, _last_name, role in USERS:
        print(f"  {role:10s} {email}  /  {PASSWORD}")


if __name__ == "__main__":
    main()

