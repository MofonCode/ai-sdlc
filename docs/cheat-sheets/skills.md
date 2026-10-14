# Skills

The eighteen skills, one line each, in the order a cycle meets them. Each name links to its entry in
`../../skills/README.md`, which has when, why, how and what.

In Claude Code a skill is `/sdlc:<name> <arguments>`. On a host that loads skills through Agent Plugins
there is no slash form: ask in a sentence, and include the cycle id.

## Adoption

| Skill | Takes | Does | Started by |
|---|---|---|---|
| [`init`](../../skills/README.md#init) | `[--platform github\|azure-devops\|local]` | interviews you about the solution and proposes its first constraints; hands you the adoption command | **a person** |

## The planning chain

| Skill | Takes | Does | Started by |
|---|---|---|---|
| [`intent`](../../skills/README.md#intent) | `[a sentence describing the work, optional]` | opens a cycle and interviews you into why the work exists | a person |
| [`spec`](../../skills/README.md#spec) | `<cycle id>` | derives testable acceptance criteria from the intent alone | a person |
| [`plan`](../../skills/README.md#plan) | `<cycle id>` | decides how, and what will prove each criterion | a person |

## The loop

| Skill | Takes | Does | Started by |
|---|---|---|---|
| [`interrogate`](../../skills/README.md#interrogate) | `<cycle id> <intent\|spec\|plan\|verify>` | judges one document against the rules, in a fresh process. Never edits | a stage skill, or a person |
| [`refine`](../../skills/README.md#refine) | `<cycle id> <intent\|spec\|plan>` | applies a round's findings to that document. Never judges | a stage skill, or a person |

## Correction

| Skill | Takes | Does | Started by |
|---|---|---|---|
| [`steer`](../../skills/README.md#steer) | `<cycle id> "what to change"` | carries a change of mind down the intent, spec and plan | a person |

## The gate

| Skill | Takes | Does | Started by |
|---|---|---|---|
| [`draft`](../../skills/README.md#draft) | `approval <id> \| abandon <id> \| solution [--standard profile\|existing] \| techstack [--empty]` | prints what belongs in a file only a person writes | a person |

## Build and verify

| Skill | Takes | Does | Started by |
|---|---|---|---|
| [`build`](../../skills/README.md#build) | `<cycle id>` | implements the approved plan, builds, tests, records the evidence | a person, or `batch-build` |
| [`verify`](../../skills/README.md#verify) | `<cycle id>` | judges the build against the spec from the evidence alone | a person, or `batch-build` |
| [`remediate`](../../skills/README.md#remediate) | `<cycle id>` | fixes source and tests after a `blocked` verification | a person, or `batch-build` |

## Pull requests and unattended runs

| Skill | Takes | Does | Started by |
|---|---|---|---|
| [`pr`](../../skills/README.md#pr) | `<cycle id> \| --all` | pushes and opens the pull request; asks before every push | **a person** |
| [`batch-build`](../../skills/README.md#batch-build) | `[--pr]` | builds and verifies every approved cycle, concurrently where files allow | a person |
| [`resolve-conflict`](../../skills/README.md#resolve-conflict) | `<file path>` | resolves a collision on a file no cycle can claim alone | a person, or `batch-build` |

## Reading and deciding

| Skill | Takes | Does | Started by |
|---|---|---|---|
| [`status`](../../skills/README.md#status) | `<cycle id> [intent\|spec\|plan]` | where a cycle stands, and the next command | anyone |
| [`constraints`](../../skills/README.md#constraints) | `[intent\|spec\|plan\|diff\|verify]` | the rules in force, all tiers resolved | every stage skill, first |
| [`suggestions`](../../skills/README.md#suggestions) | `[cycle id]` | what was noticed and not acted on | a person |
| [`propose`](../../skills/README.md#propose) | `constraints \| decisions \| theme` | proposes entries for a person to adopt, with the evidence behind each | a person |

**Bold** marks the two a person must start: the hook denies an agent the adoption command, and `pr`
asks before every push. Every skill is run by an agent; "started by" is who decides it should happen.
