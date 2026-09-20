# Open items that live in no other file

**What this file is for.** A few things are outstanding that no document in this repository
records. A session starting fresh reads the charter, the chain directories and the code, and
learns none of them — so each one survives only as long as someone remembers to say it out loud.
This is where they are written down instead.

**What this file is deliberately not.** It is **not a list of open chains.** Every chain under
`.sdlc/` already carries its own stage in its `status:` line and, where it is parked or blocked,
a banner at the top of its intent saying so, with the date and the reasoning. Copying that here
would make two lists with nothing comparing them, which is the exact failure this repository has
now been bitten by four times — the pack's file table beside its manifest, the verification
schema beside the agent that writes it, the pack's charter line beside the layout it scaffolds,
and the silent-failure register beside the test suite. Each drifted; each needed a check built to
hold it. **To see what is in flight, read the chain documents.**

**How an item leaves.** By being done, or by becoming a chain of its own — at which point the
chain's documents are the record and the line here is deleted. Not by being forgotten.

Last reviewed: 2026-09-20. Four items open; the missing git remote was closed the same day.

---

## 1. `/sdlc-core:init` has never been hand-run

Seven clauses of the shipped `init-scaffolds-solution` spec describe what the command *asks a
person*, and nothing in the test suite can observe a prompt. They shipped verified by reading,
which is stated in the spec and is not the same as tested:

| Clause | What it requires |
|---|---|
| R15, kind half | Stop when the interview has not settled the solution kind, rather than choosing one |
| R16 | In a repo that already has an approved charter, offer amendment and gap-filling as the two things it can do |
| R17, confirmation half | Present the derived solution name and take back a yes or a different string |
| R20 | If the executor fails, relay its report verbatim and do not claim the repo is scaffolded |
| R60 | Invoke the executor with the manifest path, the settled kind, the target directory and the confirmed identifier |
| S10, residual | Call the executor only after the charter is approved, at the right point in the flow |
| S11, asking half | Actually conduct the interview |

**What closes it:** running `/sdlc-core:init` by hand in an empty directory, and recording what
happened. `pack-promises-what-it-scaffolds` changed the R15 clause, so that run is now also the
only evidence the change reads correctly to someone who did not write it.

**Why it is not a chain:** it is an act, not a change. There is nothing to plan.

**Where to do it, as of 2026-09-20:** `MofonCode/MofonCode.Core` was created empty and needs
initialising anyway. Running `init` there does the hand-run against a repository that genuinely
needs the output, rather than against a directory created to be thrown away — which also
exercises the interview honestly, because the answers matter.

## 2. Four task-tier changes shipped with no fresh-context verification

The framework runs adversarial verification in a clean context for **feature**-tier work only.
Everything shipped since the scaffold was task tier and got none:

- `verification-reports-options` — verification records lead with a summary and end with options
- `register-names-every-check` — the silent-failure register and the test suite name each other
- `stack-dotnet-house-stack`
- `pack-promises-what-it-scaffolds`

The last of those has the strongest case for one it never got: it superseded half of a
requirement in an approved, shipped spec, it amended the charter, and two of its four
requirements ship on a reading of prose with no instrument behind them.

**The open question is not whether to verify that one change.** It is whether "task tier" should
mean "no verification" or "a shorter verification", given that a task-tier change can supersede a
shipped requirement and amend the charter — which is what happened. Answering it is a change to
the framework's own gates, and it belongs with `findings-leave-only-by-decision`, whose subject
is the same shape: a thing that should have been decided instead quietly not happening.

**What closes it:** a decision on that question, and a verification pass on any change still
standing unverified when it is made.

## 3. The export-and-return half of the self-improvement request

The request had two halves. **Shipped:** verification records now open with a plain-language
summary of what was found and close with the owner's options. **Not drafted:** running this
framework against a solution in another repository, exporting something from that run, and
bringing it back here so the engine improves from its own use.

Nothing about the second half exists in any file — not an intent, not a non-goal, not a note.

**What closes it:** an intent, if it is still wanted. The nearest thing already drafted is
`findings-leave-only-by-decision`, which is about findings not escaping the record; this is about
findings *arriving* from outside. They are related and they are not the same chain.

## 4. One row of the silent-failure register registers nothing

The charter's register of known silent failures carries the row:

    | Scaffold produces an unbuildable solution | `ScaffoldEmitsBuildableSolution_<kind>` |

The check that holds the register and the suite against each other keeps a backticked string only
when it matches an identifier, and the angle brackets fail that — so **the parser drops the row
entirely**. It names no instrument in either direction: nothing asserts that check exists, and it
counts toward nothing.

Harmless today, because three other rows name the same class. Worth recording because a register
whose purpose is to catch silent failures contains one.

**What closes it:** rewriting the row so its Check column names something the parser accepts, or
teaching the parser to expand a placeholder. Found on 2026-09-20 while scoping
`solution-identifier-can-carry-a-dot`; not that chain's to fix.

---

## Deliberately not listed here, and where each actually lives

- **Chain state** — parked, blocked, drafted, shipped. In each chain's own `status:` line and
  banner under `.sdlc/<slug>/`. See the rule at the top of this file.
- **The encapsulation rules** — private handlers, an internal `DbContext`, non-public EF
  configurations. Recorded as an explicit non-goal in both `scaffold-emits-a-module` and
  `scaffold-emits-an-application-layer`. Poorly placed, since both of those are themselves parked
  or blocked, but genuinely written down; it becomes its own chain next.
- **The charter's unresolved contradiction** — that amending the charter is forbidden as a side
  effect of shipping while its silent-failure register cannot stay complete unless it moves while
  something ships. In the charter's own amendment log, recorded at versions 4, 5, 6 and 7.
- **The two live defects in `SharedKernel`** — thread-pool-sized concurrency limits, and
  `Expression.Invoke` in the specification combinators. Named as a dependency in
  `scaffold-emits-an-application-layer`. **MarketSense was sunsetted on 2026-09-20**, so these
  stop being another repository's problem and become part of whatever is carried into
  `MofonCode.Core`: code lifted out of a dead codebase arrives with its defects, and nothing
  will fix them there.
