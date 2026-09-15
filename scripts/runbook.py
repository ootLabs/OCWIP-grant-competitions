#!/usr/bin/env python3
"""Drive the work queue in docs/runbook/kolejka.md and the completion gate.

The runbook tells a human (or an agent) what to do next. This script is the part
of it a machine can answer: which task is actually ready, whether the working
copy is in a state worth starting from, and whether the change is finished.

Commands:
    doctor [--fix]   environment and repository health, before anything else
    next             the first ready task from the queue
    status           the whole queue in one screen
    gate [--fast]    run the completion gate, report the first thing that fails

Exits 1 when something needs attention, 0 when clean. Standard library only.
"""

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
QUEUE_FILE = REPO_ROOT / "docs" / "runbook" / "kolejka.md"
TRELLO_CARD_URL = "https://trello.com/c/"

# The queue table is parsed by column position, so these names are the contract.
COLUMNS = ("stan", "id", "zadanie", "kamien", "trello", "zaleznosci", "bloker")
STATES = ("gotowe", "w toku", "kolejka", "zablokowane")

MILESTONE_FILES = {
    "M1": "docs/runbook/M1-fundament.md",
    "M2": "docs/runbook/M2-konkurs.md",
    "M3": "docs/runbook/M3-formularze.md",
    "M4": "docs/runbook/M4-wnioski.md",
    "M5": "docs/runbook/M5-ocena.md",
    "M6": "docs/runbook/M6-wyniki.md",
    "M7": "docs/runbook/M7-wdrozenie.md",
}


class Task:
    def __init__(self, row: dict[str, str], line: int) -> None:
        self.state = row["stan"]
        self.id = row["id"]
        self.title = row["zadanie"]
        self.milestone = row["kamien"]
        self.trello = row["trello"]
        self.blocker = row["bloker"]
        self.line = line
        raw = row["zaleznosci"]
        self.deps = [] if raw == "-" else [d.strip() for d in raw.split(",") if d.strip()]

    @property
    def url(self) -> str:
        return "" if self.trello == "-" else TRELLO_CARD_URL + self.trello

    @property
    def spec(self) -> str:
        return MILESTONE_FILES.get(self.milestone, "docs/runbook/kolejka.md")


def read_queue() -> list[Task]:
    """Every table row in kolejka.md whose first cell is a known state."""
    if not QUEUE_FILE.is_file():
        sys.exit(f"error: {QUEUE_FILE.relative_to(REPO_ROOT)} does not exist")

    tasks: list[Task] = []
    for number, line in enumerate(QUEUE_FILE.read_text(encoding="utf-8").splitlines(), 1):
        if not line.startswith("|"):
            continue
        cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
        if len(cells) != len(COLUMNS) or cells[0] not in STATES:
            continue
        tasks.append(Task(dict(zip(COLUMNS, cells)), number))
    return tasks


def validate(tasks: list[Task]) -> list[str]:
    """Problems that make the queue lie, not problems with the project."""
    problems: list[str] = []
    known = {task.id for task in tasks}

    seen: set[str] = set()
    for task in tasks:
        if task.id in seen:
            problems.append(f"line {task.line}: duplicate id {task.id}")
        seen.add(task.id)
        for dep in task.deps:
            if dep not in known:
                problems.append(f"line {task.line}: {task.id} depends on unknown {dep}")
        if task.state == "gotowe":
            open_deps = [d for d in task.deps if d in known and by_id(tasks)[d].state != "gotowe"]
            if open_deps:
                problems.append(
                    f"line {task.line}: {task.id} is done but depends on open "
                    + ", ".join(open_deps)
                )
        if task.state == "zablokowane" and task.blocker == "-":
            problems.append(f"line {task.line}: {task.id} is blocked with no blocker id")

    in_progress = [task.id for task in tasks if task.state == "w toku"]
    if len(in_progress) > 3:
        problems.append("more than three tasks in progress: " + ", ".join(in_progress))
    return problems


def by_id(tasks: list[Task]) -> dict[str, Task]:
    return {task.id: task for task in tasks}


def ready(tasks: list[Task]) -> list[Task]:
    index = by_id(tasks)
    out = []
    for task in tasks:
        if task.state != "kolejka" or task.blocker != "-":
            continue
        if all(index[dep].state == "gotowe" for dep in task.deps if dep in index):
            out.append(task)
    return out


def describe(task: Task) -> None:
    print(f"{task.id}  {task.title}")
    print(f"  milestone: {task.milestone}")
    print(f"  spec:      {task.spec}")
    if task.url:
        print(f"  trello:    {task.url}")
    if task.deps:
        print(f"  deps:      {', '.join(task.deps)}")


def cmd_next(_: argparse.Namespace) -> int:
    tasks = read_queue()
    problems = validate(tasks)
    if problems:
        print("The queue is inconsistent, fix it before taking work:")
        for problem in problems:
            print(f"  {problem}")
        return 1

    in_progress = [task for task in tasks if task.state == "w toku"]
    candidates = ready(tasks)

    if in_progress:
        print("Next (already in progress, finish it before starting anything else):")
        for task in in_progress:
            describe(task)
        if candidates:
            print()
            print("After that: " + ", ".join(task.id for task in candidates[:3]))
        return 0

    if not candidates:
        blocked = [task for task in tasks if task.state == "zablokowane"]
        print("Nothing is ready to start.")
        if blocked:
            print("Waiting on a document from the client:")
            for task in blocked:
                print(f"  {task.id}  {task.title}  ({task.blocker})")
        print()
        print("See the 'Gdy kolejka stoi' section of runbook.md.")
        return 1

    print("Next:")
    describe(candidates[0])
    if len(candidates) > 1:
        print()
        print("Also ready: " + ", ".join(task.id for task in candidates[1:4]))
    return 0


def cmd_status(_: argparse.Namespace) -> int:
    tasks = read_queue()
    counts = {state: sum(1 for t in tasks if t.state == state) for state in STATES}
    print(
        f"{len(tasks)} tasks: "
        + ", ".join(f"{counts[state]} {state}" for state in STATES)
    )
    print()

    for state in ("w toku", "kolejka", "zablokowane"):
        rows = [t for t in tasks if t.state == state]
        if not rows:
            continue
        print(f"{state}:")
        available = {t.id for t in ready(tasks)}
        for task in rows:
            mark = " "
            if task.id in available:
                mark = ">"
            elif state == "zablokowane":
                mark = "!"
            suffix = f"  [{task.blocker}]" if task.blocker != "-" else ""
            print(f"  {mark} {task.id:<8} {task.title}{suffix}")
        print()

    problems = validate(tasks)
    if problems:
        print("Queue problems:")
        for problem in problems:
            print(f"  {problem}")
        return 1
    print("'>' means ready to start now.")
    return 0


def run(command: list[str], cwd: Path | None = None) -> tuple[int, str]:
    try:
        result = subprocess.run(
            command,
            cwd=cwd or REPO_ROOT,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
    except FileNotFoundError:
        return 127, f"{command[0]} not found"
    return result.returncode, (result.stdout or "") + (result.stderr or "")


def git(*args: str) -> str:
    code, output = run(["git", *args])
    return output.strip() if code == 0 else ""


def cmd_doctor(args: argparse.Namespace) -> int:
    findings: list[tuple[str, str]] = []
    fixed: list[str] = []

    branch = git("rev-parse", "--abbrev-ref", "HEAD")
    print(f"branch: {branch or 'unknown'}")

    dirty = git("status", "--porcelain")
    if dirty:
        findings.append(
            (
                "working tree is not clean",
                "commit, stash or discard before starting a task: git status",
            )
        )

    git("fetch", "--quiet", "origin")
    counts = git("rev-list", "--left-right", "--count", "HEAD...@{upstream}")
    if counts:
        ahead, _, behind = counts.partition("\t")
        behind = behind.strip() or "0"
        if behind != "0":
            findings.append(
                (
                    f"local branch is {behind} commits behind its upstream",
                    "git pull --ff-only",
                )
            )
        if ahead.strip() not in ("", "0"):
            print(f"ahead of upstream by {ahead.strip()} commits")

    hooks = git("config", "core.hooksPath")
    if hooks != ".githooks":
        if args.fix:
            run(["git", "config", "core.hooksPath", ".githooks"])
            fixed.append("core.hooksPath set to .githooks")
        else:
            findings.append(
                (
                    "pre-commit hook is not enabled",
                    "git config core.hooksPath .githooks",
                )
            )

    env_file = REPO_ROOT / ".env"
    if not env_file.is_file():
        example = REPO_ROOT / ".env.example"
        if args.fix and example.is_file():
            env_file.write_bytes(example.read_bytes())
            fixed.append(".env created from .env.example")
        else:
            findings.append((".env is missing", "cp .env.example .env"))

    if shutil.which("docker"):
        code, output = run(["docker", "compose", "ps", "--services", "--status", "running"])
        running = set(output.split()) if code == 0 else set()
        missing = {"db", "backend", "frontend"} - running
        if missing:
            findings.append(
                (
                    "containers not running: " + ", ".join(sorted(missing)),
                    "docker compose up -d",
                )
            )
    else:
        findings.append(("docker not found on PATH", "install Docker, everything runs in it"))

    tasks = read_queue()
    problems = validate(tasks)
    for problem in problems:
        findings.append((f"queue: {problem}", "fix docs/runbook/kolejka.md"))

    print()
    for note in fixed:
        print(f"fixed: {note}")
    if not findings:
        print("Nothing needs attention.")
        return 0

    print(f"Needs attention ({len(findings)}):")
    for problem, remedy in findings:
        print(f"  {problem}")
        print(f"    -> {remedy}")
    if not args.fix:
        print()
        print("Run with --fix to apply the ones that need no decision.")
    return 1


# sys.executable, not "python". A plain "python" is absent on most Linux
# installations, which have python3 and nothing else, so the gate failed on its
# very first step with "python not found" and blamed the repository map for it.
# This also guarantees the sibling scripts run under the same interpreter as
# this one rather than whatever happens to be first on PATH.
PYTHON = sys.executable or "python3"

GATE_STEPS = [
    ("repository map", [PYTHON, "scripts/check_map.py"]),
    ("typographic dashes", [PYTHON, "scripts/check_text.py"]),
    ("backend tests", ["docker", "compose", "exec", "-T", "backend", "dotnet", "test"]),
    ("frontend typecheck", ["docker", "compose", "exec", "-T", "frontend", "npm", "run", "typecheck"]),
    ("frontend tests", ["docker", "compose", "exec", "-T", "frontend", "npm", "test"]),
    ("smoke test", [PYTHON, "scripts/smoke_test.py"]),
]

GATE_HINTS = {
    "repository map": "add, move or drop the row in docs/map/<area>.md, in this commit",
    "typographic dashes": "replace with a comma, a colon, parentheses or a plain hyphen",
    "backend tests": "docker compose logs --tail 100 backend, then see 'Samonaprawa' in runbook.md",
    "frontend typecheck": "a type error is not a style error, fix it rather than casting it away",
    "frontend tests": "docker compose up -d --build --renew-anon-volumes frontend if packages changed",
    "smoke test": "docker compose up -d --build, the stack has to be running for this one",
}


def cmd_gate(args: argparse.Namespace) -> int:
    steps = GATE_STEPS[:-1] if args.fast else GATE_STEPS
    failures: list[str] = []

    for name, command in steps:
        print(f"[ ] {name}", flush=True)
        code, output = run(command)
        if code == 0:
            print(f"[x] {name}")
            continue
        failures.append(name)
        print(f"[!] {name} failed")
        tail = [line for line in output.splitlines() if line.strip()][-25:]
        for line in tail:
            print(f"    {line}")
        # A dead daemon fails every containerised step with the same message, and
        # the per-step hint then sends you reading logs of a container that was
        # never started.
        if "docker" in output.lower() and "daemon" in output.lower():
            print("    -> Docker is not running. Start it, then: docker compose up -d")
        else:
            print(f"    -> {GATE_HINTS[name]}")
        if not args.keep_going:
            break

    print()
    if failures:
        print("Gate is red: " + ", ".join(failures))
        print("The change is not finished. See 'Samonaprawa' in runbook.md.")
        return 1

    print("Gate is green for the automated steps.")
    print("Still on you: you ran the change and used it, the new behaviour has a test,")
    print("docs/ is updated, .env.example carries new variables, docs/log.md has an entry,")
    print("and the Trello acceptance checklist is ticked.")
    return 0


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        # Task titles are Polish and the Windows console default codepage is not.
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    sub = parser.add_subparsers(dest="command")

    doctor = sub.add_parser("doctor", help="environment and repository health")
    doctor.add_argument("--fix", action="store_true", help="apply fixes that need no decision")
    doctor.set_defaults(func=cmd_doctor)

    nxt = sub.add_parser("next", help="the first ready task from the queue")
    nxt.set_defaults(func=cmd_next)

    status = sub.add_parser("status", help="the whole queue in one screen")
    status.set_defaults(func=cmd_status)

    gate = sub.add_parser("gate", help="run the completion gate")
    gate.add_argument("--fast", action="store_true", help="skip the smoke test")
    gate.add_argument("--keep-going", action="store_true", help="run every step, do not stop at the first failure")
    gate.set_defaults(func=cmd_gate)

    args = parser.parse_args()
    if not args.command:
        parser.print_help()
        return 1
    return args.func(args)


if __name__ == "__main__":
    raise SystemExit(main())
