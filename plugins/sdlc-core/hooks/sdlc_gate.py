#!/usr/bin/env python3
"""PreToolUse gate: warn when code is edited outside an approved plan.

Deliberately advisory by default. A hard block on every edit is infuriating and gets the
plugin uninstalled; a warning in the transcript is enough to notice the discipline slipping.
Set SDLC_GATE=block to make it refuse the edit instead.

Silent in repos that do not use the framework at all (no artifact root), because this plugin
loads in every project.

Two distinct states are worth warning about and they are not the same problem:

  no approved plan   - nothing has been agreed; this is the gate the README describes.
  unplanned scope    - a plan is approved but does not claim this file. That is the
                       verifier's manifest check, moved from after the build to before it,
                       which is where it is cheap.
"""
import json
import os
import pathlib
import re
import sys

# Artifacts, docs and config are how you get *out* of this state, or are edited while
# setting a repo up. Never gate them.
SKIP_SUFFIXES = {".md", ".txt", ".json", ".yml", ".yaml", ".toml"}
SKIP_NAMES = {".gitignore", ".editorconfig", ".gitattributes"}

DEFAULT_ROOT = ".sdlc"
SKIP_DIRS = {".git", "node_modules", "bin", "obj", ".venv", "dist", "target"}
FRONTMATTER = re.compile(r"\A---\s*\n(.*?)\n---\s*(\n|\Z)", re.DOTALL)
APPROVED = re.compile(r"^status:\s*approved\s*$", re.MULTILINE)
ARTIFACT_ROOT = re.compile(r"^artifact_root:\s*(.+?)\s*$", re.MULTILINE)
QUOTED = re.compile(r"""["']([^"']+)["']""")


def frontmatter(path: pathlib.Path) -> str:
    """The YAML block, or "" if the file has none. Never raises."""
    try:
        text = path.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return ""
    m = FRONTMATTER.match(text)
    return m.group(1) if m else ""


def artifact_root(cwd: pathlib.Path) -> pathlib.Path | None:
    """Where this repo keeps its chain, or None if it does not use the framework.

    The charter declares its own root in `artifact_root`, so a repo that keeps its backlog
    visible at top level (`intent/`, `specs/`, `docs/sdlc/`) is found rather than silently
    ignored - a hook that goes quiet on a non-default root is worse than no hook, because
    it reads as approval.

    Two levels deep, because `docs/sdlc/` is a documented root and is two levels. Noise
    directories are skipped so this stays cheap on every single edit.
    """
    default = cwd / DEFAULT_ROOT
    charters = [default / "CHARTER.md"]
    charters += sorted(cwd.glob("*/CHARTER.md"))
    charters += sorted(
        c for c in cwd.glob("*/*/CHARTER.md") if not (set(c.parts) & SKIP_DIRS)
    )

    for charter in charters:
        if not charter.is_file():
            continue
        declared = ARTIFACT_ROOT.search(frontmatter(charter))
        if declared:
            root = (cwd / declared.group(1).strip().strip("'\"")).resolve()
            if root.is_dir():
                return root
        return charter.parent

    # A repo mid-flight can have a chain before it has a charter.
    return default if default.is_dir() else None


def claimed_files(fm: str) -> set[str]:
    """Every path in a plan's slice manifest, normalised to posix.

    PyYAML is not guaranteed to be installed and this hook must never be the reason a
    session breaks, so the regex fallback handles the shape the schema mandates:
    `files: ["a", "b"]`.
    """
    try:
        import yaml  # noqa: PLC0415 - optional, resolved per call by design

        slices = (yaml.safe_load(fm) or {}).get("slices") or []
        files = {f for s in slices for f in (s.get("files") or [])}
    except Exception:
        files = set()
        for line in fm.splitlines():
            if line.strip().startswith(("files:", "- files:")):
                files.update(QUOTED.findall(line))

    return {f.replace("\\", "/").lstrip("./") for f in files}


def main() -> int:
    try:
        payload = json.load(sys.stdin)
    except Exception:
        return 0  # never break the session over a malformed payload

    raw = (payload.get("tool_input") or {}).get("file_path") or ""
    if not raw:
        return 0

    p = pathlib.Path(raw)
    if p.suffix.lower() in SKIP_SUFFIXES or p.name.lower() in SKIP_NAMES:
        return 0

    cwd = pathlib.Path.cwd()
    root = artifact_root(cwd)
    if root is None:
        return 0  # repo does not use the framework
    if root.name in p.parts:
        return 0  # editing the chain itself

    plans = sorted(root.glob("*/plan.md"))
    approved = [(plan, fm) for plan in plans if APPROVED.search(fm := frontmatter(plan))]

    if not approved:
        detail = (
            f"{len(plans)} plan.md found under {root.name}/ but none approved "
            f"({', '.join(d.parent.name for d in plans)})."
            if plans
            else f"No plan.md exists under {root.name}/."
        )
        action = "Run /sdlc-core:plan first, or state the tier if this is a trivial/task change."
    else:
        try:
            target = p.resolve().relative_to(cwd).as_posix()
        except ValueError:
            target = p.as_posix()

        owners = [plan.parent.name for plan, fm in approved if target in claimed_files(fm)]
        if owners:
            return 0  # claimed by an approved plan: exactly what the gate is for

        detail = (
            f"{target} is claimed by no slice of the approved plan(s): "
            f"{', '.join(plan.parent.name for plan, _ in approved)}."
        )
        action = (
            "This is unplanned scope. Add the file to a slice manifest and re-approve, "
            "or say why it is out-of-chain work."
        )

    sys.stderr.write(f"[sdlc-gate] Editing {p.name}. {detail} {action}\n")

    if os.environ.get("SDLC_GATE") == "block":
        return 2  # PreToolUse: exit 2 blocks the tool call
    return 0


if __name__ == "__main__":
    sys.exit(main())
