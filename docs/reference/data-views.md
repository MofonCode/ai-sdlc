# Data views

**Every piece of data the framework keeps, one line each**: what it is, where it lives, the command that shows
it, and who writes it. Read the command's output rather than the file where there is one: the command is what a
stage reads, and it resolves what the file only states — tiers, areas, status worked out from the record.

**Who writes it** is one of three: *a person* (one of the seven files only a person writes, placed from a draft
or by hand — the hook denies every agent write); *the binary* (a command writes it, and the hook denies an agent
writing it directly); or *a stage* (the skill that runs that stage writes it as its document).

| Data | Lives in | See it with | Written by |
|---|---|---|---|
| **The solution's records** | | | |
| [what the solution is, and how it runs](../README.md) | `.sdlc/solution.md` | `sdlc settings` | a person |
| [what it is built from, and which stack rules bind it](../guides/declaring-the-stack.md) | `.sdlc/techstack.md` | `sdlc stack` | a person |
| [the rules in force, every tier](../README.md) | `CONSTRAINTS.md` in the binary, the stack profile, `.sdlc/constraints.md` | `sdlc constraints`; one rule: `sdlc constraints show <id>` | the framework; the solution's own by a person |
| [the decisions settled](../README.md) | `.sdlc/decisions.md` | `sdlc decisions`; one: `sdlc decisions show <id>`; one area's: `sdlc decisions --area <name>` | a person |
| [what a stage reads of them](../README.md) | worked out, never stored | `sdlc decisions --for <stage> --cycle <id>`, `sdlc constraints --for <stage> --cycle <id>` | — |
| [the parts of the code, and what each covers and carries](../README.md) | `.sdlc/areas.md` | `sdlc areas` | a person |
| [the theme](../README.md) | `.sdlc/theme.json` | `sdlc theme` | a cycle that declares it |
| [the perspectives a cycle gets](../mechanisms/perspectives.md) | worked out from the intent and the plan's files | `sdlc perspectives <id>`; one: `sdlc perspective show <name> --for <stage>` | — |
| [the coverage baseline](../guides/handling-refusals.md) | `.sdlc/coverage.json` | `sdlc evidence <id> --coverage <path>` reports against it, and against `coverage_floor` (`sdlc settings`) | the binary; a person may re-measure it |

| **One cycle** | | | |
| [the intent, spec, plan and verify](../guides/running-a-cycle.md) | `.sdlc/cycles/<id>/` | `sdlc status <id>` | each stage |
| [the approvals](../mechanisms/protected-files.md) | `.sdlc/cycles/<id>/approval.md` | `sdlc status <id>`, `sdlc gate <id>` | a person |
| [why a cycle stopped](../mechanisms/protected-files.md) | `.sdlc/cycles/<id>/abandoned.md` | `sdlc cycles` | a person |
| [what each round found](../guides/handling-blockers.md) | `.sdlc/cycles/<id>/findings/<artifact>.json` | `sdlc findings <id> --stage <artifact>` | the binary, through `sdlc round` |
| [its suggestions, and what became of each](../guides/running-a-cycle.md) | `.sdlc/cycles/<id>/findings/suggestions.json` | `sdlc suggestions <id>`; every status: `--all` | the binary, through `sdlc round` |
| [what its readers cost](../../tools/README.md) | `.sdlc/cycles/<id>/findings/usage.json` | `sdlc status <id>` | the binary |
| [the build's evidence](../guides/running-a-cycle.md) | `.sdlc/cycles/<id>/evidence/` | `sdlc evidence check <id>` | the binary, through `sdlc evidence` |
| [where the build bent the approved documents](../guides/handling-blockers.md) | `.sdlc/cycles/<id>/deviations.md`; the answers in `approval.md` | `sdlc deviations <id>` | the build and remediate stages; the answers by a person |
| [a plan parked behind another's code](../guides/running-a-cycle.md) | `.sdlc/cycles/<id>/parked.md` | `sdlc backlog` | the binary, through `sdlc park` |
| [whether it holds, end to end](../guides/handling-refusals.md) | worked out from all of the above | `sdlc verify-chain <id>` | — |
| **Across cycles** | | | |
| [every cycle and where each stands](../guides/running-a-cycle.md) | `.sdlc/cycles/` | `sdlc cycles` | — |
| [what may be built together](../guides/running-a-cycle.md) | worked out from the plans' files | `sdlc cycles --claims` | — |
| [what is ready to merge and what needs a person](../guides/setting-up-the-factory.md) | worked out from the cycles and the remote | `sdlc sitrep` | — |
| [work waiting on other work](../guides/running-a-cycle.md) | `.sdlc/successors/<slug>.md`, and the parked cycles | `sdlc backlog`, `sdlc successors` | the intent stage, or `sdlc draft mining` |
| [the open suggestions, every cycle](../guides/running-a-cycle.md) | each cycle's `suggestions.json` | `sdlc suggestions` | the binary |
| [what the record says is missing](../../skills/README.md#mine) | read from every cycle and the ledger | `sdlc mine` | — |
| **Mining** | | | |
| [the mined cycles, each sitting, what is watched and what was ignored](../../skills/README.md#mine) | `.sdlc/mined.json` | `sdlc mine --history` | the binary, through `sdlc mine record` |
| [a husk's contents, once its folder is gone](../../skills/README.md#mine) | git, at the merge commit the ledger names | `git show <merge>:.sdlc/cycles/<id>/<file>` | — |
| [a report a command kept](../guides/running-a-cycle.md) | `.sdlc/reports/` — ignored by git, read back by nothing | the file itself | the binary |

**Not here:** what a whole session cost, which is a host's transcript rather than a solution's file —
`tools/Sdlc.UsageReport`, in [`../../tools/README.md`](../../tools/README.md).

## See also

- [Commands](commands.md) — every command, and what a cycle leaves on disk
- [Protected files](../mechanisms/protected-files.md) — why seven of these are a person's
- [The components](../README.md) — every concept the framework names, defined once
