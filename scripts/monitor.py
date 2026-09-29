"""An availability monitor for a machine other than the server (T-116).

Asks /health (is the API up) and /health/db (does it reach PostgreSQL)
separately, because they fail for different reasons and call for different
people. Mails when a probe goes down and again when it comes back, never in
between: the last state of each probe is kept in a file, so a cron line
every few minutes does not flood the inbox.

    */5 * * * * python3 monitor.py https://konkursy.example.pl/api

Mail settings from the environment: MONITOR_SMTP_HOST, MONITOR_SMTP_PORT
(587), MONITOR_SMTP_STARTTLS (1), MONITOR_SMTP_USER, MONITOR_SMTP_PASSWORD,
MONITOR_FROM, MONITOR_TO (comma separated). Exits 1 while a probe is down.
Standard library only; Uptime Kuma or any hosted monitor does the same job.
"""
from __future__ import annotations

import argparse
import json
import os
import smtplib
import ssl
import sys
import urllib.error
import urllib.request
from email.message import EmailMessage
from pathlib import Path

PROBES = {"api": "/health", "database": "/health/db"}


def probe(url: str, context: ssl.SSLContext | None) -> str | None:
    """None when the probe answers 200, otherwise what went wrong."""
    try:
        with urllib.request.urlopen(url, timeout=15, context=context) as response:
            return None if response.status == 200 else f"HTTP {response.status}"
    except urllib.error.HTTPError as error:
        return f"HTTP {error.code}"
    except (urllib.error.URLError, TimeoutError, ConnectionError, OSError) as error:
        return str(getattr(error, "reason", error))


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

    state_file.write_text(json.dumps(current))
    return 1 if "down" in current.values() else 0


if __name__ == "__main__":
    sys.exit(main())
