#!/usr/bin/env python3
"""Behaviour tests for hooks/sdlc_gate.py.

The gate is the one piece of this framework that runs on every single edit in every repo,
so its failure modes are asymmetric: a false silence reads as approval, and a false alarm
gets the plugin uninstalled. Both are pinned here.

    python scripts/test_sdlc_gate.py
"""
import json
import os
import pathlib
import subprocess
import sys
import tempfile

HOOK = pathlib.Path(__file__).resolve().parent.parent / "plugins" / "sdlc-core" / "hooks" / "sdlc_gate.py"

PLAN = """---
slug: audit-log
status: {status}
slices:
  - id: storage
    files: ["src/Storage/Writer.cs", "tests/Storage/WriterTests.cs"]
    depends_on: []
---
# body
"""

CHARTER = """---
solution: Test
artifact_root: {root}
status: approved
---
"""

failures = []


def run(cwd, file_path, block=False):
    env = dict(os.environ)
    env.pop("SDLC_GATE", None)
    if block:
        env["SDLC_GATE"] = "block"
    proc = subprocess.run(
        [sys.executable, str(HOOK)],
        input=json.dumps({"tool_input": {"file_path": file_path}}),
        capture_output=True,
        text=True,
        cwd=cwd,
        env=env,
    )
    return proc.returncode, proc.stderr.strip()


def check(name, got, want):
    if got == want:
        print(f"  ok      {name}")
    else:
        print(f"  FAILED  {name}: expected {want}, got {got}")
        failures.append(name)


def scenario(root_dir, status="approved", charter_root=None):
    """A temp repo with an artifact root, optionally at a non-default location."""
    tmp = pathlib.Path(tempfile.mkdtemp())
    root = tmp / root_dir
    (root / "audit-log").mkdir(parents=True)
    (root / "audit-log" / "plan.md").write_text(PLAN.format(status=status), encoding="utf-8")
    if charter_root is not None:
        (root / "CHARTER.md").write_text(CHARTER.format(root=charter_root), encoding="utf-8")
    (tmp / "src" / "Storage").mkdir(parents=True)
    return tmp


# --- silence where silence is correct -------------------------------------------------
empty = pathlib.Path(tempfile.mkdtemp())
check("no artifact root -> silent", run(empty, "src/Thing.cs")[0], 0)

t = scenario(".sdlc")
check("claimed by approved plan -> silent", run(t, "src/Storage/Writer.cs")[0], 0)
check("markdown never gated", run(t, "README.md")[0], 0)
check("dotfile config never gated", run(t, ".gitignore")[0], 0)
check("the chain itself never gated", run(t, ".sdlc/audit-log/notes.cs")[0], 0)

# --- warnings where the discipline has slipped ----------------------------------------
code, err = run(t, "src/Storage/Rogue.cs")
check("unplanned scope -> warns", code == 0 and "unplanned scope" in err, True)

draft = scenario(".sdlc", status="draft")
code, err = run(draft, "src/Storage/Writer.cs")
check("draft plan -> warns", code == 0 and "none approved" in err, True)

noplan = pathlib.Path(tempfile.mkdtemp())
(noplan / ".sdlc").mkdir()
code, err = run(noplan, "src/Thing.cs")
check("no plan at all -> warns", code == 0 and "No plan.md exists" in err, True)

# --- the half-finished refactor this file exists to prevent ---------------------------
custom = scenario("docs/sdlc", charter_root="docs/sdlc/")
check("non-default root, claimed -> silent", run(custom, "src/Storage/Writer.cs")[0], 0)
code, err = run(custom, "src/Storage/Rogue.cs")
check("non-default root, unplanned -> warns", code == 0 and "unplanned scope" in err, True)

# --- block mode and robustness --------------------------------------------------------
check("block mode refuses the edit", run(t, "src/Storage/Rogue.cs", block=True)[0], 2)
check("block mode still allows a claimed file", run(t, "src/Storage/Writer.cs", block=True)[0], 0)

# PyYAML is optional. On a machine without it the regex fallback is the ONLY parser,
# so it has to agree with yaml on the same manifest or the gate silently stops matching.
shadow = pathlib.Path(tempfile.mkdtemp())
(shadow / "yaml.py").write_text('raise ImportError("blocked for test")', encoding="utf-8")


def run_no_yaml(cwd, file_path):
    env = dict(os.environ)
    env.pop("SDLC_GATE", None)
    env["PYTHONPATH"] = str(shadow)
    proc = subprocess.run(
        [sys.executable, str(HOOK)],
        input=json.dumps({"tool_input": {"file_path": file_path}}),
        capture_output=True, text=True, cwd=cwd, env=env,
    )
    return proc.returncode, proc.stderr.strip()


check("no PyYAML: claimed file still silent", run_no_yaml(t, "src/Storage/Writer.cs")[0], 0)
code, err = run_no_yaml(t, "src/Storage/Rogue.cs")
check("no PyYAML: unplanned still warns", code == 0 and "unplanned scope" in err, True)

proc = subprocess.run([sys.executable, str(HOOK)], input="not json", capture_output=True, text=True, cwd=t)
check("malformed payload never breaks the session", proc.returncode, 0)
proc = subprocess.run([sys.executable, str(HOOK)], input="{}", capture_output=True, text=True, cwd=t)
check("payload with no file_path", proc.returncode, 0)

print()
if failures:
    print(f"{len(failures)} failure(s): {', '.join(failures)}")
    sys.exit(1)
print("test_sdlc_gate: all passed")
