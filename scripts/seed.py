#!/usr/bin/env python3
"""Fill an empty local database with the smallest set of rows worth working on.

Every developer otherwise clicks the same operator, the same competition and the
same two applications by hand, and everybody's local database ends up shaped
slightly differently. This produces one shape, so a bug reproduced on one machine
reproduces on another.

The row set is not decoration. It is the fixture the permission tests in T-13.3
need: two applicants, one application each, so that reaching for somebody else's
application is a thing that can actually be attempted.

Usage:
    docker compose up -d
    python scripts/seed.py

Refuses to touch a database that already holds rows, so it can never overwrite
work in progress. Exits 1 on refusal or failure. Standard library only.

The accounts it creates CANNOT log in: password hashing arrives with
registration in T-12.1, so the hash column gets an obvious placeholder rather
than something that looks like a credential.
"""

from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path
from typing import NoReturn

# docker compose finds its configuration by walking up from the working
# directory, so the script has to name the repository root itself. Without this
# the command only works when it happens to be run from the root, and from
# anywhere else it fails with "no configuration file provided", which says
# nothing about the real cause.
REPO_ROOT = Path(__file__).resolve().parent.parent

# Read from the environment, not from .env: docker compose loads that file, this
# script does not. A value changed only in .env therefore has to be exported
# before running this, and the connection error below says so.
POSTGRES_USER = os.environ.get("POSTGRES_USER", "ocwip")
POSTGRES_DB = os.environ.get("POSTGRES_DB", "ocwip")

# Every table the schema has. Emptiness is checked across all of them, not just
# the ones seeded, because a partially populated database is the case where an
# insert would silently attach new rows to somebody else's data.
#
# The guard is only as complete as this tuple, and nothing in Python keeps it
# current: a table added by a later migration and forgotten here is never
# counted, so the guard would stay quiet about exactly the rows it exists to
# protect. unlisted_tables() therefore asks the database instead of trusting
# this list.
TABLES = (
    "users",
    "entities",
    "competitions",
    "form_definitions",
    "applications",
    "attachments",
    # Identity's own, added by T-12.0 and seeded empty: we issue no claims, use
    # no external sign in providers and store no authenticator tokens. They are
    # here because emptiness is checked across the whole schema, not across the
    # part this script writes. docs/model-danych.md says why they exist at all.
    "user_claims",
    "user_logins",
    "user_tokens",
)

# EF Core's own bookkeeping, not a domain table and never seeded. The snake_case
# convention does not touch it, so it keeps a PascalCase name while everything
# around it is lower case.
MIGRATIONS_TABLE = "__EFMigrationsHistory"

# Fixed identifiers, so a test, a bug report and a URL can quote one and mean the
# same row on every machine. Readable on purpose: a UUID that says 0041 in a log
# is recognisably seed data and not a real application.
OPERATOR = "00000000-0000-4000-a000-000000000001"
APPLICANT_ONE = "00000000-0000-4000-a000-000000000002"
APPLICANT_TWO = "00000000-0000-4000-a000-000000000003"
ENTITY_ONE = "00000000-0000-4000-a000-000000000011"
ENTITY_TWO = "00000000-0000-4000-a000-000000000012"
COMPETITION = "00000000-0000-4000-a000-000000000021"
FORM_DEFINITION = "00000000-0000-4000-a000-000000000031"
APPLICATION_SUBMITTED = "00000000-0000-4000-a000-000000000041"
APPLICATION_DRAFT = "00000000-0000-4000-a000-000000000042"
ATTACHMENT = "00000000-0000-4000-a000-000000000051"

# Never a real hash and never a real password. Nothing in this repository, test
# data included, should read as a credential (AGENTS.md, security rule 4).
PASSWORD_PLACEHOLDER = "placeholder-not-a-hash-see-T-12.1"

# example.org is reserved by RFC 2606, so a stray notification from a half built
# mail module cannot reach anybody.
EMAIL_OPERATOR = "anna.kowalska@example.org"
EMAIL_APPLICANT_ONE = "marek.nowak@example.org"
EMAIL_APPLICANT_TWO = "katarzyna.wisniewska@example.org"

# Whole minutes, matching ck_competitions_start_date_whole_minute. Truncated in
# UTC explicitly rather than in the session timezone, so the statement holds even
# against a session that is not set to UTC.
NOW_MINUTE = "(date_trunc('minute', now() AT TIME ZONE 'UTC') AT TIME ZONE 'UTC')"

# The JSON contract is card T-20, so this is deliberately a sketch and not a
# proposal. Keys are English like every other identifier, labels are Polish like
# every other piece of product text (AGENTS.md, language rule).
FORM_DEFINITION_JSON = """
{
  "note": "Placeholder structure. The contract of this column is decided in T-20.",
  "sections": [
    {
      "title": "Dane oferenta",
      "fields": [
        {"key": "task_name", "label": "Nazwa zadania publicznego", "type": "text", "required": true}
      ]
    },
    {
      "title": "Budżet",
      "fields": [
        {"key": "requested_amount", "label": "Wnioskowana kwota dotacji", "type": "number", "required": true}
      ]
    }
  ]
}
"""

ANSWERS_SUBMITTED_JSON = """
{
  "task_name": "Sąsiedzka biblioteka pod chmurką",
  "requested_amount": 8000
}
"""

ANSWERS_DRAFT_JSON = """
{
  "task_name": "Warsztaty naprawcze dla mieszkańców"
}
"""

SEED_SQL = f"""
BEGIN;

-- Entities first: an account points at one, not the other way round.
INSERT INTO entities
    (id, type, name, contact_information, nip, address, is_active, deactivated_at)
VALUES
    ('{ENTITY_ONE}', 'Organisation', 'Stowarzyszenie Aktywne Opole',
     'kontakt@example.org, tel. 700 100 200',
     -- Ten numer nie przechodzi sumy kontrolnej NIP i to jest zamierzone:
     -- dane testowe nie mają wyglądać na prawdziwe.
     '1234567890', 'ul. Testowa 1, 45-001 Opole', true, NULL),
    ('{ENTITY_TWO}', 'InformalGroup', 'Grupa nieformalna Sąsiedzi z Zaodrza',
     'sasiedzi@example.org, tel. 700 300 400',
     -- Grupa nieformalna nie ma NIP-u ani adresu organizacji. To nie jest brak
     -- danych, tylko drugi z trzech typów podmiotu (docs/model-danych.md).
     NULL, NULL, true, NULL);

-- normalized_email is spelled out here and the other Identity columns are
-- not, because it is the one new NOT NULL column without a store default:
-- uniqueness stands on it, so an account without one would collide with nobody.
INSERT INTO users
    (id, first_name, last_name, email, normalized_email,
     password_hash, role, pesel,
     email_confirmed, is_active, deactivated_at, entity_id)
VALUES
    -- The operator has no entity: they run the competition for OCWIP, they do
    -- not apply for a grant.
    ('{OPERATOR}', 'Anna', 'Kowalska',
     '{EMAIL_OPERATOR}', upper('{EMAIL_OPERATOR}'),
     '{PASSWORD_PLACEHOLDER}', 'Operator', NULL, true, true, NULL, NULL),
    -- No PESEL on any account. It only appears at the agreement stage, and a
    -- made up one in that column passes every validation there is.
    ('{APPLICANT_ONE}', 'Marek', 'Nowak',
     '{EMAIL_APPLICANT_ONE}', upper('{EMAIL_APPLICANT_ONE}'),
     '{PASSWORD_PLACEHOLDER}', 'Applicant', NULL, true, true, NULL,
     '{ENTITY_ONE}'),
    ('{APPLICANT_TWO}', 'Katarzyna', 'Wiśniewska',
     '{EMAIL_APPLICANT_TWO}', upper('{EMAIL_APPLICANT_TWO}'),
     '{PASSWORD_PLACEHOLDER}', 'Applicant', NULL, true, true, NULL,
     '{ENTITY_TWO}');

-- The rest of Identity's columns, filled the way UserManager fills them and the
-- way the T-12.0 migration filled the rows that already existed. upper() has to
-- agree with ToUpperInvariant or a seeded account is one UserManager cannot
-- find; NormalizedAddressTests is what holds those two together.
UPDATE users
   SET user_name = email,
       normalized_user_name = upper(email),
       security_stamp = gen_random_uuid()::text,
       concurrency_stamp = gen_random_uuid()::text;

-- Open right now: started a week ago, closes in a month. A seeded competition
-- that is already closed cannot be applied to, which makes it useless for the
-- one thing a developer wants it for.
INSERT INTO competitions
    (id, title, description, start_date, end_date, max_grant_amount, status,
     is_active, deactivated_at)
VALUES
    ('{COMPETITION}', 'Konkurs testowy: inicjatywy lokalne 2026',
     'Dane testowe ze scripts/seed.py. Ten konkurs nie istnieje naprawdę.',
     {NOW_MINUTE} - interval '7 days',
     {NOW_MINUTE} + interval '30 days',
     10000.00, 'Published', true, NULL);

INSERT INTO form_definitions
    (id, competition_id, version_number, definition, is_active, deactivated_at)
VALUES
    ('{FORM_DEFINITION}', '{COMPETITION}', 1,
     '{FORM_DEFINITION_JSON.strip()}'::jsonb, true, NULL);

-- One application per applicant, and that split is the point: it is what makes
-- "applicant two reaches for applicant one's application" a case T-13.3 can
-- actually test.
INSERT INTO applications
    (id, competition_id, entity_id, form_definition_id, answers, status,
     submitted_at, number, is_active, deactivated_at)
VALUES
    -- Submitted, so it carries both a submission instant and a number. The
    -- schema pairs each of those with the status by its own check constraint.
    ('{APPLICATION_SUBMITTED}', '{COMPETITION}', '{ENTITY_ONE}',
     '{FORM_DEFINITION}', '{ANSWERS_SUBMITTED_JSON.strip()}'::jsonb,
     'Submitted', now() - interval '2 days', '001', true, NULL),
    -- Draft, so it carries neither. A draft that burns a number would leave a
    -- gap in the register that nobody can explain to an applicant.
    ('{APPLICATION_DRAFT}', '{COMPETITION}', '{ENTITY_TWO}',
     '{FORM_DEFINITION}', '{ANSWERS_DRAFT_JSON.strip()}'::jsonb,
     'Draft', NULL, NULL, true, NULL);

-- Metadata only, no bytes anywhere. Uploading and permission checked downloads
-- are T-32. The row exists so that card, and the permission tests, start with an
-- attachment belonging to a specific organisation.
INSERT INTO attachments
    (id, application_id, file_name, content_type, size_in_bytes, storage_path,
     is_active, deactivated_at)
VALUES
    ('{ATTACHMENT}', '{APPLICATION_SUBMITTED}', 'statut-stowarzyszenia.pdf',
     'application/pdf', 182400,
     'seed/2026/0f3a9c1b8e2d4f6a.pdf', true, NULL);

COMMIT;
"""

# Reads back what was written instead of trusting the inserts. The pairing of
# status with number and submission instant is checked explicitly, because that
# pair is what the permission tests and the submission card both lean on. So is
# the split of the two applications across two entities: both applications
# landing under one applicant would still count as two rows, and would quietly
# destroy the only thing this fixture exists for.
VERIFY_SQL = f"""
SELECT
    (SELECT count(*) FROM users)                                    AS users,
    (SELECT count(*) FROM users WHERE role = 'Operator')            AS operators,
    (SELECT count(*) FROM users WHERE role = 'Applicant')           AS applicants,
    (SELECT count(*) FROM users
      WHERE normalized_email = upper(email)
        AND normalized_user_name = upper(email)
        AND security_stamp IS NOT NULL)                               AS normalized,
    (SELECT count(*) FROM entities)                                 AS entities,
    (SELECT count(*) FROM competitions)                             AS competitions,
    (SELECT count(*) FROM form_definitions)                         AS form_definitions,
    (SELECT count(*) FROM applications
       WHERE status = 'Submitted'
         AND number IS NOT NULL AND submitted_at IS NOT NULL)       AS submitted,
    (SELECT count(*) FROM applications
       WHERE status = 'Draft'
         AND number IS NULL AND submitted_at IS NULL)               AS drafts,
    (SELECT count(DISTINCT entity_id) FROM applications)            AS owners,
    (SELECT count(*) FROM applications a
       JOIN users u ON u.entity_id = a.entity_id
      WHERE u.email = '{EMAIL_APPLICANT_ONE}'
        AND a.status = 'Submitted')                                 AS submitted_of_one,
    (SELECT count(*) FROM attachments)                              AS attachments;
"""

EXPECTED = {
    "users": 3,
    "operators": 1,
    "applicants": 2,
    "normalized": 3,
    "entities": 2,
    "competitions": 1,
    "form_definitions": 1,
    "submitted": 1,
    "drafts": 1,
    "owners": 2,
    "submitted_of_one": 1,
    "attachments": 1,
}


def psql(sql: str) -> tuple[int, str, str]:
    """Run one statement batch inside the db container.

    ON_ERROR_STOP is what makes the transaction in SEED_SQL mean anything:
    without it psql keeps going after a failed statement and COMMIT lands on a
    half filled database.
    """
    command = [
        "docker", "compose", "exec", "-T", "db",
        "psql", "-U", POSTGRES_USER, "-d", POSTGRES_DB,
        "-v", "ON_ERROR_STOP=1", "--no-align", "--tuples-only",
    ]
    try:
        completed = subprocess.run(
            command,
            input=sql,
            capture_output=True,
            text=True,
            encoding="utf-8",
            cwd=REPO_ROOT,
            check=False,
        )
    except FileNotFoundError:
        fail("docker was not found on PATH, so the stack cannot be reached")
    return completed.returncode, completed.stdout.strip(), completed.stderr.strip()


def fail(message: str) -> NoReturn:
    print(f"FAIL: {message}")
    sys.exit(1)


def existing_rows() -> dict[str, int]:
    """Row counts per table, or an exit if the schema is not there yet."""
    query = " UNION ALL ".join(
        f"SELECT '{table}', count(*) FROM {table}" for table in TABLES
    )
    code, out, err = psql(f"{query};")

    if code != 0:
        # Narrow on purpose. A wrong role or database name also says "does not
        # exist", and reading that as a missing schema sends the developer off
        # to wait for migrations that would never have fixed it.
        if f'role "{POSTGRES_USER}" does not exist' in err:
            fail(
                f'the database has no role "{POSTGRES_USER}". POSTGRES_USER is '
                "read from the environment, not from .env, so export it before "
                "running this"
            )
        if f'database "{POSTGRES_DB}" does not exist' in err:
            fail(
                f'there is no database "{POSTGRES_DB}". POSTGRES_DB is read '
                "from the environment, not from .env, so export it before "
                "running this"
            )
        if "relation" in err and "does not exist" in err:
            fail(
                "the tables are missing, so migrations have not run yet. "
                "Start the stack with docker compose up -d and wait for the "
                "backend to apply them"
            )
        fail(f"could not read the database: {err}")

    counts: dict[str, int] = {}
    for line in out.splitlines():
        table, _, count = line.partition("|")
        counts[table] = int(count)

    # An answer short of a count per table would read as an empty database and
    # seed straight over existing rows, which is the one thing this script
    # promises never to do.
    missing = [table for table in TABLES if table not in counts]
    if missing:
        fail(
            "the row counts came back incomplete, so emptiness could not be "
            f"established for {', '.join(missing)}. Nothing was changed"
        )
    return counts


def unlisted_tables() -> set[str]:
    """Tables the database has that TABLES does not name.

    Called after existing_rows(), so the connection is already known to work
    and a failure here is a real one rather than a missing schema the developer
    has just been told about.
    """
    code, out, err = psql(
        "SELECT table_name FROM information_schema.tables "
        "WHERE table_schema = 'public';"
    )
    if code != 0:
        fail(f"could not read the table list: {err}")

    present = {line.strip() for line in out.splitlines() if line.strip()}
    return present - set(TABLES) - {MIGRATIONS_TABLE}


def main() -> int:
    print("Checking that the database is empty.")
    counts = existing_rows()

    # Those counts are only worth as much as their coverage, so establish the
    # coverage before drawing a conclusion from them.
    unlisted = unlisted_tables()
    if unlisted:
        fail(
            "the database has tables this script does not know about: "
            f"{', '.join(sorted(unlisted))}. Add them to TABLES, otherwise "
            "they are never checked for emptiness"
        )

    populated = {table: count for table, count in counts.items() if count}

    if populated:
        listed = ", ".join(f"{table}={count}" for table, count in populated.items())
        print(f"FAIL: the database already holds rows ({listed}).")
        print("This script only ever seeds an empty database, so nothing was changed.")
        print("Start over with: docker compose down -v && docker compose up -d")
        return 1

    print("Inserting the seed data.")
    code, _, err = psql(SEED_SQL)
    if code != 0:
        fail(f"the seed transaction was rolled back: {err}")

    print("Verifying what landed.")
    code, out, err = psql(VERIFY_SQL)
    if code != 0:
        fail(f"could not read the seeded rows back: {err}")

    values = out.split("|")
    # zip would pair up whatever came back and leave the rest missing, so a
    # short answer has to fail here rather than as a KeyError two lines down.
    if len(values) != len(EXPECTED):
        fail(
            f"the read back returned {len(values)} columns, "
            f"expected {len(EXPECTED)}: {out!r}"
        )

    actual = dict(zip(EXPECTED, (int(value) for value in values)))
    wrong = {key: actual[key] for key in EXPECTED if actual[key] != EXPECTED[key]}
    if wrong:
        fail(f"the seed inserted the wrong shape: {wrong}, expected {EXPECTED}")

    print()
    print("Seeded. Nothing here can log in until T-12.1 adds password hashing.")
    print(f"  operator     {EMAIL_OPERATOR}")
    print(f"  applicant 1  {EMAIL_APPLICANT_ONE}  submitted application 001")
    print(f"  applicant 2  {EMAIL_APPLICANT_TWO}  draft application")
    print(f"  competition  {COMPETITION}, open for another 30 days")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
