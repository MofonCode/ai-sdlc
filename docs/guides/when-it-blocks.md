<!--
covers:
  - "tools/*.ps1"
  - "schemas/verify.schema.json"
-->

# When something refuses

Every refusal in this framework is deliberate and names a remedy. This is the index to them.

## Verify said `blocked`

Open blocking findings, and remediation budget left.

```
/sdlc:remediate <id>
```

```
/sdlc:verify <id>
```

Three remediation passes, each followed by a fresh verification, and then the outcome becomes
`escalated`. The counter only resets when **you** change one of verify's inputs, so an unattended run
cannot talk its way into a fourth attempt.

## Verify said `escalated`

Three passes are spent and blocking findings remain. The machine is done; the cycle is yours.

`verify.md` is never hand-edited — a hook refuses it, and the reason is the whole audit story: the
document's value is that it was derived. You change an **input** and re-derive. There are four ways, and
all of them are a person's:

| Exit | When the blocker is | What you change |
|---|---|---|
| fix by hand | real, and the machine could not do it | the code, then `/sdlc:verify <id>` |
| amend the spec | the *criterion* was wrong, not the code | the spec, remove its approval, re-interrogate, re-approve |
| waive | real, understood, and accepted anyway | a waiver record naming who, when and why |
| abandon | not worth solving | close the cycle unlanded |

The spec-amendment exit is the one people forget, and it is often the right one. A badly written criterion
otherwise forces you to contort code to satisfy it.

**A waiver's reason has to say why the finding is acceptable** — not restate the finding, not cite
schedule pressure alone. It is the only control on the one override in the system, and it is what a reader
has in six months instead of the conversation.

## A hook refused an edit

| Refusal | What it means | What to do |
|---|---|---|
| no plan for this branch | the cycle has no `plan.md` | run `/sdlc:plan <id>` and have it approved |
| the plan is not approved | no `approved_by` and `approved_at` | a person adds both. This is the gate, not a formality |
| the plan does not declare this file | the change is reaching outside what was approved | either the plan is incomplete — amend and re-approve — or this is scope the plan never described |
| a verify document is derived | you tried to edit one | change an input and re-derive |
| that findings document already exists | rounds are numbered and kept | write the next round |
| that evidence already exists | build writes evidence once | a fresh run is a new cycle, not an overwrite |
| the artifact does not match its schema | key names or nesting are wrong | the refusal lists which; the skill's body has the shape |
| not a git repository | `.sdlc/` exists but there is no repo | `git init` and commit. Without it nothing is enforced |

## Interrogation said the artifact is settled

A person already approved it, so a finding has nothing left to act on.

If it has since been **amended**, the approval is stale: remove those frontmatter fields, interrogate,
and have a person approve it again. Otherwise the cycle has moved on — after an approved spec comes
`/sdlc:plan`, after an approved plan comes `/sdlc:build`.

## Landing refused

| Outcome | Why |
|---|---|
| no verify document | an unverified cycle has no claim to land |
| `blocked` | remediation budget remains; use it |
| `escalated` | verification is still failing after three attempts, and it says how many were spent |

The count matters to whoever reads it: work that resisted three attempts is not the same as work nobody
tried to fix.

## Nothing is refusing and everything works first time

Check the branch. The gates key on `cycle/<id>` exactly, and off that branch they are silent by design —
so a mistyped branch means nothing is enforced while everything appears to work.

```pwsh
git rev-parse --abbrev-ref HEAD
```

Then check the hooks are loaded at all.

```pwsh
claude plugin details sdlc@ai-sdlc
```

`Hooks (1) PreToolUse` is what you want. Zero means the deterministic layer is absent.
