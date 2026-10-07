# Plan — the decision record and the palette

A working document, not a design authority. When each phase lands, its reasoning goes to
`CONCEPT.md` as a decision entry and this file loses that section. When the file is empty, delete it
— and any question still open moves to §19 rather than going with it.

**Both phases have landed.** Their reasoning is D-70 and D-71, which is where somebody looks for it.
§19 items 9 and 10 no longer describe unbuilt work; each now carries the one question its phase
deliberately left open.

| | What | State |
|---|---|---|
| ~~1~~ | ~~`propose constraints`~~ | **landed — D-69** |
| ~~2~~ | ~~a decision record~~ | **landed — D-70** |
| ~~3~~ | ~~a palette~~ | **landed — D-71** |

What shipped, in one line each:

- **`sdlc decisions [--brief\|--all]`** renders the choices a solution has settled, so a stage reads
  what was argued instead of arguing it again.
- **`sdlc propose decisions`** reads what **approved** cycles stated — an intent's `Fixed decisions`,
  a plan's `Shape and departures` — and hands the passages over. Skips a cycle with no standing
  approval, one reopened, and one stopped.
- **`.sdlc/decisions.md`** is the sixth protected file. `DEC_nn` ids, four parts, supersession by new
  id.
- **`sdlc palette`** renders the roles a solution's surfaces use; **`sdlc propose palette`** prints
  the skeleton or names the empty roles, and never a value.
- **`.sdlc/palette.json`** is deliberately *not* protected.

---

## What was wrong in the plan, and was corrected on the way

**`CON_15` does not reach a recorded decision.** §19 item 9 and the first version of this file both
said an entry would be covered as a declared default. It is not: `CON_15`'s `Forbids` clause names
`techstack.md` specifically, so a plan departing from a recorded decision breaks no rule. Making it
bind would take a **new** global constraint rather than a reworded one, because what is forbidden
would change (§7.4) — and a global rule is a person's to write, in the framework's own library. The
record therefore ships binding nothing, which is written into D-70 rather than left to be discovered.

**The derived-view shape could not hold what the record is for.** §19 item 9 leaned toward deriving
the record on demand from the approved artifacts, on the grounds that it cannot go stale. True, and
refused anyway: it can only hold what some cycle happened to say, and most of what wants recording
never passes through a cycle — a theme, a code-style choice, a convention invented once and applied
everywhere since. A record that structurally cannot hold those is not the one anybody asked for.

---

## Open

**O-1 — whether a recorded decision should ever bind.** Now §19 item 9. Needs a new global constraint
and a person to write it.

**O-2 — whether a palette is per solution or shared as a profile concern.** Now §19 item 10. Shipped
per solution; a shared design system is a profile question.

Both left this file for §19 as its header asks. Nothing else is outstanding, so this file is next to
be deleted.
