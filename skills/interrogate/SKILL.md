---
name: interrogate
description: Challenge one artifact against the effective constraint set and write the findings. Use before an intent, spec or plan advances, and whenever a person asks whether an artifact would survive review.
argument-hint: "[path to the artifact]"
allowed-tools: Bash(powershell.exe *) Bash(git *)
---

# Interrogate `$0`

Challenge the artifact at **$0** against every constraint that applies to it, and write what you
find. You are an adversary, not an editor: you do not fix the artifact, you report on it.

## Assemble the effective constraint set

Two sources, neither copied into the other:

1. `${CLAUDE_PLUGIN_ROOT}/constraints.md` — global, `CON-nn`
2. `.sdlc/constraints.md` in this solution — local, `LOC-nn`. Often absent; that is normal

Read both. Each constraint states what it **Applies to**. Check only the ones that apply to this
artifact's kind — a spec is not judged by a rule about waivers.

## Judge

For each applicable constraint, decide whether the artifact breaches its `Forbids:` clause.

- **Cite exactly one constraint per finding.** A problem that breaches two constraints is two
  findings
- **Never invent a constraint.** If something is wrong and no constraint forbids it, say so in
  your closing summary as an observation. Do not manufacture an ID
- **Report nothing when nothing is wrong.** An empty findings array is a legitimate and common
  result. Over-reporting is as damaging as under-reporting: it teaches everyone to skim findings
- **Severity is not a feeling.** `blocking` means the artifact must not advance as written.
  `suggestion` means it may advance and would be better changed

## Write the findings

Write `.sdlc/cycles/<cycle-id>/findings/<artifact-name>.r<round>.json`, where round starts at 1
and increments — never overwrite an earlier round, because the sequence is the record of whether
repair worked. For an intent, which has no cycle directory yet, write
`.sdlc/intents/findings/<id>.r<round>.json`.

The document must validate against `${CLAUDE_PLUGIN_ROOT}/schemas/findings.schema.json`. Every
field is required for a reason:

- `framework_version` — read it from `${CLAUDE_PLUGIN_ROOT}/.claude-plugin/plugin.json`. Global
  constraints live in the library, so an upgrade changes the rules with no commit here; without
  the version a verdict cannot be reproduced
- `locus` — a quoted phrase, a heading, or a path and line. A finding nobody can locate cannot
  be acted on
- `remedy` — required on every `blocking` finding. Remediation cannot work a finding that does
  not say what would resolve it
- `disposition` — always `open` here. Only a waiver makes it `waived`, and waivers are not yours
  to write

## Then report

Print a short summary: the count at each severity, and each blocking finding as one line naming
its constraint and its locus. Do not restate the findings document in prose — it is the record,
and the summary is a pointer to it.

If the artifact is sound, say so plainly in one line. Resist the urge to add advice.
