"""The calendar lock of a deployment (T-115, T-48 "Termin naboru a wdrożenie").

A deployment in the last days of an intake risks the one moment the system
exists for: applicants submit in the last hours. So a deployment is refused
while any open competition closes within the next DAYS days, unless forced.
Reads the public API only, the same list the home page shows.

Usage:
    python scripts/deploy_guard.py https://konkursy.example.pl/api [--days 3] [--force]
    python scripts/deploy_guard.py --from-file competitions.json   (a saved answer, for testing)

Behind a site password (staging, T-117) SITE_BASIC_AUTH carries it as
user:password. Exits 1 when the deployment has to wait. Standard library only.
"""
from __future__ import annotations

import argparse
import base64
import json
import os
import sys
import urllib.request
from datetime import datetime, timedelta, timezone


def closing_soon(competitions: list[dict], now: datetime, days: int) -> list[str]:
    """The open competitions whose intake closes before now + days."""
    limit = now + timedelta(days=days)
    found = []
    for competition in competitions:
        intake = competition.get("intake") or {}
        closes = intake.get("closesAt")
        if intake.get("state") != "Open" or not closes:
            continue
        closes_at = datetime.fromisoformat(closes.replace("Z", "+00:00"))
        if closes_at <= limit:
            found.append(f"{competition.get('number')} {competition.get('title')}: closes {closes_at.isoformat()}")
    return found


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("api", nargs="?", help="the public API, for example https://konkursy.example.pl/api")
    parser.add_argument("--days", type=int, default=3)
    parser.add_argument("--force", action="store_true", help="deploy anyway, a decision somebody takes on purpose")
    parser.add_argument("--from-file", help="read the competitions from a saved answer instead of the API")
    parser.add_argument("--now", help="the moment to check against, ISO 8601 (testing)")
    args = parser.parse_args()

    if args.from_file:
        with open(args.from_file, encoding="utf-8") as file:
            competitions = json.load(file)
    elif args.api:
        request = urllib.request.Request(f"{args.api.rstrip('/')}/public/competitions")
        if credentials := os.environ.get("SITE_BASIC_AUTH"):
            request.add_header("Authorization", "Basic " + base64.b64encode(credentials.encode()).decode())
        with urllib.request.urlopen(request, timeout=30) as response:
            competitions = json.loads(response.read().decode("utf-8"))
    else:
        parser.error("give the API address or --from-file")

    now = datetime.fromisoformat(args.now) if args.now else datetime.now(timezone.utc)
    soon = closing_soon(competitions, now, args.days)

    if not soon:
        print(f"No open intake closes within {args.days} days. Deployment may go ahead.")
        return 0

    print(f"Intake closing within {args.days} days:")
    for line in soon:
        print(f"  {line}")
    if args.force:
        print("Forced: deploying anyway.")
        return 0

    print("Deployment refused. Wait until the intake closes, or deploy with force on purpose.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
