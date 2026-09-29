"""An availability monitor for a machine other than the server (T-116).

Asks /health (is the API up) and /health/db (does it reach PostgreSQL)
separately, because they fail for different reasons and call for different
people. Mails when a probe goes down and again when it comes back, never in
between: the last state of each probe is kept in a file, so a cron line
every few minutes does not flood the inbox.

    */5 * * * * python3 monitor.py https://konkursy.example.pl/api

With --backup-max-age HOURS it also asks the restic repository for the
newest nightly snapshot (T-114) and treats an older one, or no answer, as a
probe that is down: a backup that fails every night says nothing on the
server. RESTIC_REPOSITORY, RESTIC_PASSWORD and the storage's keys come from
the environment (a key that may only read is enough); MONITOR_RESTIC names
the command, "restic" by default.

Mail settings from the environment: MONITOR_SMTP_HOST, MONITOR_SMTP_PORT
(587), MONITOR_SMTP_STARTTLS (1), MONITOR_SMTP_USER, MONITOR_SMTP_PASSWORD,
MONITOR_FROM, MONITOR_TO (comma separated). Behind a site password
(staging) MONITOR_BASIC_AUTH is user:password. Exits 1 while a probe is
down. Standard library only; Uptime Kuma or any hosted monitor does the
same job for the two HTTP probes.
"""
from __future__ import annotations

import argparse
import base64
import json
import os
import re
import shlex
import smtplib
import ssl
import subprocess
import sys
import urllib.error
import urllib.request
from datetime import datetime, timedelta, timezone
from email.message import EmailMessage
from pathlib import Path

PROBES = {"api": "/health", "database": "/health/db"}


def probe(url: str, context: ssl.SSLContext | None) -> str | None:
    """None when the probe answers 200, otherwise what went wrong."""
    request = urllib.request.Request(url)
    if credentials := os.environ.get("MONITOR_BASIC_AUTH"):
        request.add_header("Authorization", "Basic " + base64.b64encode(credentials.encode()).decode())
    try:
        with urllib.request.urlopen(request, timeout=15, context=context) as response:
            return None if response.status == 200 else f"HTTP {response.status}"
    except urllib.error.HTTPError as error:
        return f"HTTP {error.code}"
    except (urllib.error.URLError, TimeoutError, ConnectionError, OSError) as error:
        return str(getattr(error, "reason", error))


def snapshot_time(value: str) -> datetime:
    """restic writes nanoseconds; datetime takes six digits at most."""
    return datetime.fromisoformat(re.sub(r"(\.\d{6})\d+", r"\1", value.replace("Z", "+00:00")))


def backup_problem(snapshots: list[dict], now: datetime, max_age: timedelta) -> str | None:
    """None when the newest snapshot is recent enough, otherwise what is wrong."""
    if not snapshots:
        return "brak jakiejkolwiek kopii"
    newest = max(snapshot_time(snapshot["time"]) for snapshot in snapshots)
    if now - newest > max_age:
        return f"najnowsza kopia z {newest.isoformat()}"
    return None


def backup_probe(max_age: timedelta) -> str | None:
    command = shlex.split(os.environ.get("MONITOR_RESTIC", "restic")) + [
        "snapshots", "--host", "ocwip", "--tag", "nightly", "--latest", "1", "--json"]
    try:
        result = subprocess.run(command, capture_output=True, text=True, timeout=300, check=False)
    except (OSError, subprocess.TimeoutExpired) as error:
        return f"restic nie odpowiada: {type(error).__name__}"
    if result.returncode != 0:
        # Only the code: restic's own message can carry the repository address.
        return f"restic zakończył się kodem {result.returncode}"
    return backup_problem(json.loads(result.stdout or "[]"), datetime.now(timezone.utc), max_age)


def send(subject: str, body: str) -> None:
    message = EmailMessage()
    message["Subject"] = subject
    message["From"] = os.environ["MONITOR_FROM"]
    message["To"] = os.environ["MONITOR_TO"]
    message.set_content(body)
    host = os.environ["MONITOR_SMTP_HOST"]
    port = int(os.environ.get("MONITOR_SMTP_PORT", "587"))
    with smtplib.SMTP(host, port, timeout=30) as smtp:
        if os.environ.get("MONITOR_SMTP_STARTTLS", "1") == "1":
            smtp.starttls(context=ssl.create_default_context())
        if os.environ.get("MONITOR_SMTP_USER"):
            smtp.login(os.environ["MONITOR_SMTP_USER"], os.environ.get("MONITOR_SMTP_PASSWORD", ""))
        smtp.send_message(message)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("api", help="the public API, for example https://konkursy.example.pl/api")
    parser.add_argument("--state", default=str(Path.home() / ".ocwip-monitor.json"))
    parser.add_argument("--insecure", action="store_true", help="accept a test certificate (never in production)")
    parser.add_argument("--backup-max-age", type=float, metavar="HOURS",
                        help="also check that the newest nightly backup is at most this old, for example 26")
    args = parser.parse_args()

    context = ssl._create_unverified_context() if args.insecure else None
    state_file = Path(args.state)
    previous = json.loads(state_file.read_text()) if state_file.exists() else {}
    current = {}

    for name, path in PROBES.items():
        failure = probe(f"{args.api.rstrip('/')}{path}", context)
        current[name] = "down" if failure else "up"
        print(f"{name} ({path}): {failure or 'ok'}")
        if current[name] != previous.get(name, "up"):
            if failure:
                send(f"ALARM OCWIP: {path} nie odpowiada",
                     f"Sonda {args.api}{path} zwraca: {failure}.\n\n"
                     "/health to samo API, /health/db jego połączenie z bazą. Procedura: docs/wdrozenie.md.")
            else:
                send(f"OCWIP: {path} znowu odpowiada", f"Sonda {args.api}{path} odpowiada poprawnie.")

    if args.backup_max_age is not None:
        failure = backup_probe(timedelta(hours=args.backup_max_age))
        current["backup"] = "down" if failure else "up"
        print(f"backup: {failure or 'ok'}")
        if current["backup"] != previous.get("backup", "up"):
            if failure:
                send("ALARM OCWIP: kopia zapasowa nie powstaje",
                     f"Kopia nocna: {failure} (dopuszczalny wiek {args.backup_max_age:g} h).\n\n"
                     "Log usługi backup na serwerze i procedura: docs/wdrozenie.md, \"Kopie zapasowe\".")
            else:
                send("OCWIP: kopia zapasowa znowu powstaje", "Najnowsza kopia nocna jest znowu świeża.")

    state_file.write_text(json.dumps(current))
    return 1 if "down" in current.values() else 0


if __name__ == "__main__":
    sys.exit(main())
