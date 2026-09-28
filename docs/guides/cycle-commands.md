---
covers:
  - "skills/**/SKILL.md"
---

# The cycle, as commands

Every command for one cycle, in order, with nothing explained. For why any of it is like this, read
`first-cycle.md` once; for what to do when something refuses, read `when-it-blocks.md`.

This is a view, not an authority. The skills own what they do, and this restates their sequence so it fits
on one screen.

Substitute your cycle id for `<id>` throughout. Everything from `spec` onward takes the id;
`interrogate` takes the id plus which artifact.

## Once per repository

```pwsh
git init
```

```pwsh
git commit --allow-empty -m "before adoption"
```

```
/sdlc:init
```

## Once per cycle

**Intent.**

```
/sdlc:intent <one sentence describing the problem>
```

```pwsh
git checkout -b cycle/<id>
```

```
/sdlc:interrogate <id> intent
```

Then a person adds to the intent's frontmatter:

```yaml
accepted_by: <name>
accepted_at: <timestamp>
```

**Spec.**

```
/sdlc:spec <id>
```

```
/sdlc:interrogate <id> spec
```

Then a person adds the same `accepted_by` and `accepted_at` pair to the spec's frontmatter.

**Plan.**

```
/sdlc:plan <id>
```

```
/sdlc:interrogate <id> plan
```

Then a person adds to the plan's frontmatter:

```yaml
approved_by: <name>
approved_at: <timestamp>
```

**Build and verify.**

```
/sdlc:build <id>
```

```
/sdlc:verify <id>
```

While the outcome is `blocked`:

```
/sdlc:remediate <id>
```

```
/sdlc:verify <id>
```

**Land**, once the outcome is `pass`:

```
/sdlc:land <id>
```

## Anytime

```
/sdlc:constraints
```

```
/sdlc:suggestions
```

```
/sdlc:run --all
```

## What the gates are

Three, and each is a person writing two frontmatter fields by hand. Nothing writes them for you, and no
skill will.

| Artifact | Fields | You are agreeing |
|---|---|---|
| intent | `accepted_by`, `accepted_at` | this is the right problem |
| spec | `accepted_by`, `accepted_at` | this is what done means |
| plan | `approved_by`, `approved_at` | this approach is sound, and this risk is acceptable |

Both fields or neither. A name with no date says nothing about which version you agreed to.
