---
covers:
  - ".claude-plugin/*.json"
---

# Installing, updating and removing

Two ways in, and which one you want depends on whether you are *using* the framework or *changing* it.
One command per block.

| Source | For | Picks up |
|---|---|---|
| GitHub | using it — this is what a teammate runs | what has been pushed |
| a local directory | changing the framework itself | your working tree, including uncommitted edits |

The plugin is hosted on **GitHub**, at `MofonCode/ai-sdlc`. There is no Azure DevOps path; `land` shells
out to `gh`, and CI is GitHub Actions.

## Install from GitHub

Works from anywhere — it clones the repository itself, so you do not need a copy.

```pwsh
claude plugin marketplace add MofonCode/ai-sdlc
```

```pwsh
claude plugin install sdlc@ai-sdlc
```

## Install from a local directory

Use this when you are editing the framework. It reads your working tree, so a change is picked up by a
reinstall without pushing anything.

Run the first from wherever you cloned this repository. The `./` is required — a bare `.` is rejected.

```pwsh
cd C:\Repos\ai-sdlc
```

```pwsh
claude plugin marketplace add ./
```

```pwsh
claude plugin install sdlc@ai-sdlc
```

The two sources use the same marketplace name, so they cannot both be registered. Switching means
removing one first — the **Remove** section below.

## Check what you have

```pwsh
claude plugin details sdlc@ai-sdlc
```

Expect a version, `Skills (12)`, and `Hooks (1) PreToolUse`. **If the hook count is zero the
deterministic layer is not loaded and nothing is enforced** — treat that as a failed install rather than
a detail, because every cycle will still appear to work.

```pwsh
claude plugin marketplace list
```

That says which source you are on, which is the first thing to check when a change you made is not
showing up.

## Update

A directory-marketplace install **caches by version**, and `claude plugin update` does nothing when the
version has not moved. So a refresh is a full reinstall, in this order:

```pwsh
claude plugin uninstall sdlc@ai-sdlc
```

```pwsh
claude plugin install sdlc@ai-sdlc
```

The two are a pair. Running only the first leaves you with no plugin.

On the GitHub source, pull the marketplace first so the clone is current, then do the pair above:

```pwsh
claude plugin marketplace update ai-sdlc
```

## Remove

```pwsh
claude plugin uninstall sdlc@ai-sdlc
```

```pwsh
claude plugin marketplace remove ai-sdlc
```

Removing the plugin leaves every `.sdlc/` directory untouched. The artifacts are a repository's own
record, not the plugin's, and they stay readable without it.

## Three things that catch people

**Hooks need workspace trust.** Project hooks do not run until the workspace is trusted. Until then the
gates are silent, and a cycle appears to work while nothing is enforced.

**A session loads skills at startup.** Reinstall, then start a new session — an open one keeps what it
already had.

**Line endings differ between sources without meaning anything.** A directory install copies your working
tree; a GitHub install checks out through `.gitattributes`. Comparing the two byte for byte shows every
line of a `.ps1` as changed when nothing has.
