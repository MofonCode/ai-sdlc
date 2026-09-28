---
covers:
  - ".claude-plugin/*.json"
---

# Installing, updating and removing

One command per block.

## Install

From this repository, register it as a marketplace. The `./` is required — a bare `.` is rejected.

```pwsh
cd C:\Repos\ai-sdlc
```

```pwsh
claude plugin marketplace add ./
```

```pwsh
claude plugin install sdlc@ai-sdlc
```

## Check what you have

```pwsh
claude plugin details sdlc@ai-sdlc
```

Expect a version, `Skills (12)`, and `Hooks (1) PreToolUse`. If the hook count is zero the deterministic
layer is not loaded, and nothing is enforced — treat that as a failed install rather than a detail.

```pwsh
claude plugin list
```

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

If the version *has* moved, this also works and is worth trying first:

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

## Two things that catch people

**Hooks need workspace trust.** Project hooks do not run until the workspace is trusted. Until then the
gates are silent, and a cycle appears to work while nothing is enforced.

**A session loads skills at startup.** Reinstall, then start a new session — an open one keeps what it
already had.
