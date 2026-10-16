#!/usr/bin/env python3
"""Every operation of the OpenAPI document, as a visitor and as an applicant (T-119).

For each of the operations it calls the route with real but foreign
identifiers (a competition that exists, another applicant's application and
attachment) and reports:

- a 5xx anywhere: monitoring counts every one as an outage, and a stack trace
  is not an answer to a stranger;
- a 2xx for a visitor outside the public allowlist: a route nobody locked;
- a 2xx for an applicant on a route that names somebody else's resource
  (rule 2), or on a staff route.

Routes an applicant may legitimately open without naming a resource (their
own profile, the public competitions) are listed for a human to read, not
failed. Needs the stack from prepare.py and an OpenAPI document (Development
serves /openapi/v1.json; the production compose file does not).

    python loadtest/route_sweep.py --openapi openapi.json --state loadtest/state.json
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import uuid
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from prepare import Session, sign_in  # noqa: E402

# A visitor may do these; everything else needs a session.
PUBLIC = re.compile(
    r"^(/public/|/health|/register$|/login$|/logout$|/forgot-password$|/reset-password$|"
    r"/verify-email$|/resend-verification$|/confirm-email-change$)"
)
# An applicant may start a draft in a competition (the resource is the competition, theirs to apply to).
APPLICANT_OK = {"POST /competitions/{competitionId}/applications"}


def fill(path: str, ids: dict[str, str]) -> str:
    def value(match: re.Match[str]) -> str:
        name = match.group(1)
        if name in ids:
            return ids[name]
        if name in ("version", "stage"):
            return "1" if name == "version" else "formal"
        return str(uuid.uuid4())

    return re.sub(r"\{(\w+)(?::\w+)?\}", value, path)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--openapi", required=True)
    parser.add_argument("--state", default=str(Path(__file__).with_name("state.json")))
    args = parser.parse_args()
    spec = json.loads(Path(args.openapi).read_text(encoding="utf-8"))
    state = json.loads(Path(args.state).read_text(encoding="utf-8"))

    me, other = Session(), Session()
    sign_in(me, state["accounts"][0]["email"])
    sign_in(other, state["accounts"][1]["email"])
    foreign_app = other.call("GET", "/applications")[1][0]["id"]
    foreign_attachment = other.call("GET", f"/applications/{foreign_app}/attachments")[1][0]["id"]
    ids = {
        "competitionId": state["competitionId"],
        "applicationId": foreign_app,
        "attachmentId": foreign_attachment,
        "requirementId": state["requirementId"],
    }
    # "id" is ambiguous in the document: it names an application, an attachment or a competition by route.
    visitor = Session()

    five_xx: list[str] = []
    open_to_visitor: list[str] = []
    open_to_applicant_foreign: list[str] = []
    open_to_applicant_plain: list[str] = []
    total = 0

    for template, methods in sorted(spec["paths"].items()):
        for method, _ in methods.items():
            if method not in ("get", "post", "put", "patch", "delete"):
                continue
            method = method.upper()
            if template == "/logout":
                continue  # would end the session the sweep is using
            variants = [dict(ids)]
            if "{id" in template:
                owner = "applications" if template.startswith("/applications") else "attachments" if template.startswith("/attachments") else "competitions"
                key = {"applications": foreign_app, "attachments": foreign_attachment, "competitions": state["competitionId"]}[owner]
                variants = [{**ids, "id": key}]
            for variant in variants:
                path = fill(template, variant)
                body = {} if method in ("POST", "PUT", "PATCH") else None
                label = f"{method} {template}"
                total += 1

                status, _ = visitor.call(method, path, body)
                if status >= 500:
                    five_xx.append(f"{label} (visitor) -> {status}")
                if 200 <= status < 300 and not PUBLIC.match(template):
                    open_to_visitor.append(f"{label} -> {status}")

                status, _ = me.call(method, path, body)
                if status >= 500:
                    five_xx.append(f"{label} (applicant) -> {status}")
                if 200 <= status < 300 and label not in APPLICANT_OK and not template.startswith("/public/"):
                    names_resource = bool(re.search(r"\{(applicationId|attachmentId|id|reportId|contractId|evaluationId)", template))
                    staff = bool(re.match(r"^/(accounts|access-requests|reviewer|reviewers)|^/competitions(/|$)", template)) and not template.startswith("/competitions/{id}")
                    (open_to_applicant_foreign if names_resource or staff else open_to_applicant_plain).append(f"{label} -> {status}")

    print(f"{total} operations called as a visitor and as an applicant.\n")
    for title, rows in (
        ("5xx (must be empty)", five_xx),
        ("2xx for a visitor outside the public allowlist (must be empty)", open_to_visitor),
        ("2xx for an applicant on a foreign resource or a staff route (must be empty)", open_to_applicant_foreign),
        ("2xx for an applicant, no resource named (read and judge)", open_to_applicant_plain),
    ):
        print(f"{title}: {len(rows)}")
        for row in rows:
            print(f"  {row}")
    sys.exit(1 if five_xx or open_to_visitor or open_to_applicant_foreign else 0)


if __name__ == "__main__":
    main()
