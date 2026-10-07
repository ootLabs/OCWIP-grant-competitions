#!/usr/bin/env python3
"""The contract of scripts/deploy.sh (S-26, S-39).

A deployment script has nowhere to live in the test suites, and the two things
it has to get right are invisible from reading it:

- it refuses anything that is not one full commit SHA, and it refuses before
  it fetches, checks out, backs up or starts anything (S-26);
- it pins the images to the digests recorded for the commit it is starting, so
  a rollback runs the images of the commit it rolls back to and not the ones of
  the version that has just failed (S-39).

Both are checked against the real script, in a sandbox with a fake docker on
PATH: nothing is pulled, nothing is started, and the repository this runs in is
never touched.

Usage:
    python scripts/check_deploy.py

Exits 1 on the first broken expectation. Standard library only.
"""

from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DIGEST_NEW = "@sha256:" + "ab" * 32
DIGEST_OLD = "@sha256:" + "cd" * 32
SERVICES = ("DB", "MIGRATE", "BACKEND", "FRONTEND", "CADDY", "BACKUP")


def every_digest(digest: str) -> str:
    """A file pinning every image of the compose file, which is what the
    deploy workflow sends and what deploy.sh insists on."""
    return "".join(f"IMAGE_DIGEST_{name}={digest}\n" for name in SERVICES)

# Answers "docker", records how it was called and what IMAGE_* variables the
# script handed it, and never touches a registry.
FAKE_DOCKER = """#!/usr/bin/env python3
import json, os, sys

args = sys.argv[1:]
with open(os.environ["DOCKER_LOG"], "a", encoding="utf-8") as handle:
    handle.write(json.dumps({
        "args": args,
        "tag": os.environ.get("IMAGE_TAG"),
        "digests": {k: v for k, v in os.environ.items() if k.startswith("IMAGE_DIGEST_")},
    }) + "\\n")

if "ps" in args and "--services" in args:
    sys.exit(0)                                  # no backup service running
if "ps" in args and "-a" in args:
    if os.environ.get("FAIL_PS") == "1":
        print("docker: no such thing", file=sys.stderr)
        sys.exit(1)
    for service in ("backend", "frontend", "caddy", "db"):
        print(service + " healthy")
    sys.exit(0)
if "up" in args and os.environ.get("IMAGE_TAG") == os.environ.get("FAIL_UP_FOR", ""):
    sys.exit(1)
sys.exit(0)
"""

failures: list[str] = []


def expect(condition: bool, what: str) -> None:
    if not condition:
        failures.append(what)


class Sandbox:
    """A repository with two commits, deploy.sh in it and a fake docker."""

    def __init__(self, directory: Path) -> None:
        self.path = directory
        (directory / "scripts").mkdir()
        shutil.copy(ROOT / "scripts" / "deploy.sh", directory / "scripts" / "deploy.sh")
        # The real one: its IMAGE_DIGEST_* variables are the list deploy.sh
        # checks a digest file against. The fake docker never reads it.
        shutil.copy(ROOT / "docker-compose.prod.yml", directory / "docker-compose.prod.yml")
        (directory / ".env.prod").write_text("", encoding="utf-8")
        binaries = directory / "bin"
        binaries.mkdir()
        (binaries / "docker").write_text(FAKE_DOCKER, encoding="utf-8")
        (binaries / "docker").chmod(0o755)
        self.log = directory / "docker.log"

        self.git("init", "--quiet", "--initial-branch=main")
        self.git("config", "user.email", "test@example.org")
        self.git("config", "user.name", "Test")
        self.git("add", "-A")
        self.git("commit", "--quiet", "-m", "first")
        self.old = self.git("rev-parse", "HEAD").strip()
        (directory / "marker").write_text("second\n", encoding="utf-8")
        self.git("add", "marker")
        self.git("commit", "--quiet", "-m", "second")
        self.new = self.git("rev-parse", "HEAD").strip()
        # Fetching from itself: deploy.sh fetches origin before it checks out.
        self.git("remote", "add", "origin", str(directory))

    def git(self, *args: str) -> str:
        return subprocess.run(
            ["git", *args], cwd=self.path, capture_output=True, text=True, check=True
        ).stdout

    def deploy(
        self, *args: str, stdin: str | None = None, **environment: str
    ) -> subprocess.CompletedProcess[str]:
        self.log.unlink(missing_ok=True)
        return subprocess.run(
            ["scripts/deploy.sh", *args],
            cwd=self.path,
            capture_output=True,
            text=True,
            input=stdin if stdin is not None else "",
            env={
                **os.environ,
                "PATH": f"{self.path / 'bin'}{os.pathsep}{os.environ['PATH']}",
                "DOCKER_LOG": str(self.log),
                "DEPLOY_TIMEOUT": "30",
                **environment,
            },
        )

    def calls(self) -> list[dict]:
        if not self.log.exists():
            return []
        return [json.loads(line) for line in self.log.read_text(encoding="utf-8").splitlines()]

    def dirty(self) -> str:
        return self.git("status", "--porcelain")


def check_refuses_anything_but_a_commit(box: Sandbox) -> None:
    for argument in ([], [""], ["beef"], ["1" * 39], ["g" + "1" * 39], ["A" * 40], [box.new[:-1] + "zz"]):
        done = box.deploy(*argument)
        shown = argument or ["(no argument)"]
        expect(done.returncode == 2, f"{shown}: expected exit 2, got {done.returncode}")
        expect("full 40 character SHA" in done.stderr, f"{shown}: no message about the SHA")
        expect(box.calls() == [], f"{shown}: docker was called")
        expect(box.dirty() == "", f"{shown}: the checkout changed")
        expect(not (box.path / ".deploy-digests").exists(), f"{shown}: a digest store was created")


def check_a_commit_that_is_not_there_starts_nothing(box: Sandbox) -> None:
    done = box.deploy("0" * 40)
    expect(done.returncode != 0, f"a commit the repository does not have should fail, got {done.returncode}")
    expect("Deployed" not in done.stdout, f"it reported a deployment anyway: {done.stdout!r}")
    expect(
        [call for call in box.calls() if "pull" in call["args"] or "up" in call["args"]] == [],
        "the checkout failed and it pulled and started images regardless",
    )
    expect(not (box.path / ".deployed-tag").exists(), "it wrote the commit down as deployed")


def check_an_unanswering_stack_is_not_healthy(box: Sandbox) -> None:
    done = box.deploy(box.new, DEPLOY_TIMEOUT="1", FAIL_PS="1")
    expect(done.returncode == 1, f"a stack that cannot be asked is not healthy, got {done.returncode}")
    expect("Deployed" not in done.stdout, f"it reported a deployment anyway: {done.stdout!r}")
    expect("not healthy" in done.stderr, f"no message about health: {done.stderr!r}")
    box.git("checkout", "--quiet", "main")


def check_takes_the_commit_from_a_forced_command(box: Sandbox) -> None:
    done = box.deploy(SSH_ORIGINAL_COMMAND=f"cd '/opt/ocwip' && scripts/deploy.sh {box.new}")
    expect(done.returncode == 0, f"a forced command should deploy, got {done.returncode}: {done.stderr}")
    pulls = [call for call in box.calls() if "pull" in call["args"]]
    expect([call["tag"] for call in pulls] == [box.new], f"pulled the wrong commit: {pulls}")

    for asked in ("cd '/opt/ocwip' && rm -rf /", f"deploy {'a' * 41}", f"deploy {box.new[:39]}"):
        done = box.deploy(SSH_ORIGINAL_COMMAND=asked)
        expect(done.returncode == 2, f"{asked!r}: a forced command without a commit should be refused")
        expect(box.calls() == [], f"{asked!r}: it reached docker anyway")


def check_a_tag_still_deploys_without_digests(box: Sandbox) -> None:
    done = box.deploy(box.new)
    expect(done.returncode == 0, f"a deployment without digests should work, got {done.returncode}")
    pulled = [call for call in box.calls() if "pull" in call["args"]]
    expect(pulled != [] and pulled[0]["digests"] == {}, f"digests out of nowhere: {pulled}")
    expect(pulled != [] and pulled[0]["tag"] == box.new, f"the wrong commit: {pulled}")


def check_digests_arrive_on_standard_input(box: Sandbox) -> None:
    done = box.deploy(box.new, stdin=every_digest(DIGEST_NEW))
    expect(done.returncode == 0, f"expected a deployment, got {done.returncode}: {done.stderr}")
    kept = box.path / ".deploy-digests" / f"{box.new}.env"
    expect(kept.exists(), "the digests were not kept for the commit")
    wanted = {f"IMAGE_DIGEST_{name}": DIGEST_NEW for name in SERVICES}
    for call in box.calls():
        if "pull" in call["args"] or "up" in call["args"]:
            expect(call["digests"] == wanted, f"compose did not get the digests: {call}")


def check_a_file_that_pins_only_some_images_is_refused(box: Sandbox) -> None:
    short = every_digest(DIGEST_NEW).replace(f"IMAGE_DIGEST_CADDY={DIGEST_NEW}\n", "")
    done = box.deploy(box.new, stdin=short)
    expect(done.returncode != 0, "a file that pins only some images should be refused")
    expect("IMAGE_DIGEST_CADDY" in done.stderr, f"it did not name the missing image: {done.stderr!r}")
    expect(box.calls() == [], "it pulled with some images left on the tag")
    shutil.rmtree(box.path / ".deploy-digests")


def check_a_line_that_is_not_a_digest_stops_everything(box: Sandbox) -> None:
    for line in (
        'IMAGE_DIGEST_BACKEND=@sha256:$(touch pwned)',
        "IMAGE_DIGEST_BACKEND=:latest",
        "IMAGE_DIGEST_BACKEND=@sha256:" + "ab" * 31,
        "PATH=/pwned",
        "IMAGE_DIGEST_BACKEND=@sha256:" + "ab" * 32 + " extra",
    ):
        done = box.deploy(box.new, stdin=line + "\n")
        expect(done.returncode != 0, f"{line!r}: expected a refusal")
        expect("not an image digest" in done.stderr, f"{line!r}: no message about the digest")
        expect(box.calls() == [], f"{line!r}: docker was called anyway")
        expect(not (box.path / "pwned").exists(), f"{line!r}: the line was run")


def check_a_refused_input_keeps_the_recorded_digests(box: Sandbox) -> None:
    store = box.path / ".deploy-digests"
    kept = store / f"{box.new}.env"
    box.deploy(box.new, stdin=every_digest(DIGEST_NEW))
    expect(kept.exists(), "the digests of the commit were not recorded at all")

    done = box.deploy(box.new, stdin="IMAGE_DIGEST_BACKEND=:latest\n")
    expect(done.returncode != 0, "an input that is not a digest should be refused")
    held = kept.read_text(encoding="utf-8") if kept.exists() else "(gone)"
    expect(
        held == every_digest(DIGEST_NEW),
        f"a refused input replaced the digests recorded for the commit: {held!r}",
    )
    shutil.rmtree(store)


def check_junk_in_the_state_file_is_no_rollback_target(box: Sandbox) -> None:
    state = box.path / ".deployed-tag"
    state.write_text("../../etc/passwd\n", encoding="utf-8")
    done = box.deploy(box.new, FAIL_UP_FOR=box.new)
    expect(done.returncode == 1, f"a failed deployment should exit 1, got {done.returncode}")
    expect(
        "nothing to roll back to" in done.stderr,
        f"a state file that is not a commit became a rollback target: {done.stderr}",
    )
    expect(
        all(call["tag"] != "../../etc/passwd" for call in box.calls()),
        "the contents of the state file reached docker as a commit",
    )
    state.unlink()
    box.git("checkout", "--quiet", "main")


def check_a_rollback_uses_its_own_digests(box: Sandbox) -> None:
    (box.path / ".deployed-tag").write_text(box.old + "\n", encoding="utf-8")
    store = box.path / ".deploy-digests"
    store.mkdir(exist_ok=True)
    (store / f"{box.old}.env").write_text(every_digest(DIGEST_OLD), encoding="utf-8")

    done = box.deploy(box.new, stdin=every_digest(DIGEST_NEW), FAIL_UP_FOR=box.new)
    expect(done.returncode == 1, f"a failed deployment should exit 1, got {done.returncode}")
    expect("Rolled back" in done.stderr, f"no rollback: {done.stderr}")
    for call in box.calls():
        if call["tag"] == box.old:
            expect(
                call["digests"].get("IMAGE_DIGEST_BACKEND") == DIGEST_OLD,
                f"the rollback would have started the images of the failed version: {call}",
            )
    box.git("checkout", "--quiet", "main")
    (box.path / ".deployed-tag").unlink()
    shutil.rmtree(store)


def main() -> int:
    with tempfile.TemporaryDirectory() as directory:
        box = Sandbox(Path(directory))
        check_refuses_anything_but_a_commit(box)
        check_a_commit_that_is_not_there_starts_nothing(box)
        check_an_unanswering_stack_is_not_healthy(box)
        check_takes_the_commit_from_a_forced_command(box)
        check_a_tag_still_deploys_without_digests(box)
        check_digests_arrive_on_standard_input(box)
        check_a_file_that_pins_only_some_images_is_refused(box)
        check_a_line_that_is_not_a_digest_stops_everything(box)
        check_a_refused_input_keeps_the_recorded_digests(box)
        check_junk_in_the_state_file_is_no_rollback_target(box)
        check_a_rollback_uses_its_own_digests(box)

    if failures:
        print(f"scripts/deploy.sh broke {len(failures)} expectations:")
        for failure in failures:
            print(f"  - {failure}")
        return 1
    print("scripts/deploy.sh keeps its contract.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
