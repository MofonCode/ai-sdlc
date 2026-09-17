# Stack profiles

Defaults `init` **proposes**, so the owner confirms instead of composing a stack from nothing.
A proposal is not a decision: always show what you picked and why, and take an override without
argument.

## Contents

- How to choose a profile
- Profile: `dotnet`
- Profile: `python-ml`
- What gets written
- What never gets asked

## How to choose a profile

Read the signals before proposing. In order:

1. **An existing stack in the repo wins outright** — `*.csproj`/`*.sln`, `pyproject.toml`,
   `package.json`. Never propose a profile that contradicts code already there.
2. **Competition or contest repo** (a Kaggle slug, `train.csv`/`test.csv`, a
   `sample_submission.csv`, a rules or leaderboard reference) → `python-ml`.
3. **Otherwise** → `dotnet`, this owner's default for solution work.

State the signal you used in one line: *"No existing project files and no competition markers,
so proposing the dotnet profile — say the word if this is Python."*

## Profile: `dotnet`

The default for solution projects.

| Commitment | Value |
|---|---|
| Language | C#, latest LTS `net` target |
| Build | `dotnet build` |
| Tests | xUnit, `dotnet test` |
| Style | `.editorconfig` (analyzer severities included), `dotnet format` |
| Nullable | enabled, warnings as errors |

Ask, rather than assume: persistence, hosting, and whether this is a library, service, or app.
Those genuinely differ per project and a wrong guess is expensive.

## Profile: `python-ml`

The default for competitions and analysis work.

| Commitment | Value |
|---|---|
| Language | Python 3.12+ |
| Deps | `pyproject.toml` |
| Tests | pytest |
| Style | ruff (lint + format), `.editorconfig` for the editor |
| Types | type hints on public functions; not enforced by default |

Ask, rather than assume: notebook or module layout, and the ML libraries in play.

## What gets written

On confirmation, `init` writes the profile's style files if they are absent — it never
overwrites an existing one, since a repo with an `.editorconfig` has already made this decision:

| Profile | File | Source |
|---|---|---|
| `dotnet` | `.editorconfig` | `assets/dotnet.editorconfig` |
| `python-ml` | `.editorconfig` | `assets/python.editorconfig` |

The charter then records **the profile name and any overrides**, not the style rules themselves:

```markdown
## Technology commitments

- Profile: `dotnet` (C#, xUnit, .editorconfig + dotnet format)
- Persistence: Azure Table Storage — append-only fits the market-data invariant
- Hosting: Azure Container Apps — matches the existing deployment
```

`spec-writer` reads that section before the codebase, so a named profile is all a spec needs.

## What never gets asked

**Code style is not interrogated, and does not belong in the charter.** Brace placement, indent
width, and naming conventions are enforced by `.editorconfig` and the formatter — deterministic,
checkable in CI, and free of any dependence on an agent remembering them.

The charter holds *invariants*: properties that make a change wrong regardless of what it was
trying to do. Style is not one. Mixing the two dilutes the section that actually gates future
intents.

If the owner raises style during the interview, record it as a one-line note and move on — or,
better, translate it into an `.editorconfig` rule and a formatter that enforces it.
