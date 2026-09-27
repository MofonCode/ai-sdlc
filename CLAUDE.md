@AGENTS.md

## Claude Code specifics

This repository is a Claude Code plugin, so its own components are the product: skills under
`skills/<name>/SKILL.md`, hooks in `hooks/hooks.json`, and a manifest at
`.claude-plugin/plugin.json`. A change to any of those changes framework behaviour for every
solution that installs it — treat them as production code, not configuration.

Shell work is PowerShell. Scripts the plugin ships are `.ps1`, and commands in skills and docs
are written for `pwsh`, not bash.

The import above is required rather than stylistic: Claude Code reads `AGENTS.md` directly only
from v2.1.277, and on Windows a `CLAUDE.md` symlinked to `AGENTS.md` checks out as plain text
without `core.symlinks`. The import works in every version and every clone.
