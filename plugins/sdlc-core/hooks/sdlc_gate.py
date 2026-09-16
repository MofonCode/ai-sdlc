#!/usr/bin/env python3
"""PreToolUse gate: warn when code is edited with no approved plan.md.

Deliberately advisory by default. A hard block on every edit is infuriating and gets the
plugin uninstalled; a warning in the transcript is enough to notice the discipline slipping.
Set SDLC_GATE=block to make it refuse the edit instead.

Silent in repos that do not use the framework at all (no .sdlc/ directory), because this
plugin loads in every project.
"""
import json
import os
import pathlib
import sys

SOURCE_SKIP_SUFFIXES = {".md", ".txt", ".json", ".yml", ".yaml", ".toml", ".gitignore"}


def main() -> int:
    try:
        payload = json.load(sys.stdin)
    except Exception:
        return 0  # never break the session over a malformed payload

    path = (payload.get("tool_input") or {}).get("file_path") or ""
    if not path:
        return 0

    p = pathlib.Path(path)

    # Artifacts and docs are how you get *out* of this state. Never gate them.
    if ".sdlc" in p.parts or p.suffix.lower() in SOURCE_SKIP_SUFFIXES:
        return 0

    sdlc = pathlib.Path.cwd() / ".sdlc"
    if not sdlc.is_dir():
        return 0  # repo does not use the framework

    approved = [
        plan
        for plan in sdlc.glob("*/plan.md")
        if "status: approved" in plan.read_text(encoding="utf-8", errors="replace")
    ]
    if approved:
        return 0

    drafts = list(sdlc.glob("*/plan.md"))
    if drafts:
        detail = (
            f"{len(drafts)} plan.md found but none approved "
            f"({', '.join(d.parent.name for d in drafts)})."
        )
    else:
        detail = "No plan.md exists under .sdlc/."

    sys.stderr.write(
        f"[sdlc-gate] Editing {p.name} with no approved plan. {detail} "
        "Run /sdlc-core:plan first, or state the tier if this is a trivial/task change.\n"
    )

    if os.environ.get("SDLC_GATE") == "block":
        return 2  # PreToolUse: exit 2 blocks the tool call
    return 0


if __name__ == "__main__":
    sys.exit(main())
