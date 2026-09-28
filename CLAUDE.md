@AGENTS.md

## Claude Code specifics

This repository is a Claude Code plugin, so its own components are the product: skills under
`skills/<name>/SKILL.md`, hooks in `hooks/hooks.json`, and a manifest at
`.claude-plugin/plugin.json`. A change to any of those changes framework behaviour for every
solution that installs it — treat them as production code, not configuration.

Shell work is PowerShell, and scripts the plugin ships are `.ps1` targeting **Windows PowerShell 5.1**,
invoked as `powershell.exe`. Not PowerShell 7: a hook that cannot start because an edition is missing
protects nothing while appearing to. `docs/techstack.md` records what 5.1 costs and how the scripts are
written for it.

The import above is required rather than stylistic: Claude Code reads `AGENTS.md` directly only
from v2.1.277, and on Windows a `CLAUDE.md` symlinked to `AGENTS.md` checks out as plain text
without `core.symlinks`. The import works in every version and every clone.
