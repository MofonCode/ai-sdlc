# The reference

Three lists for when you already know why and need the line. **One line per thing, and a link for the
reasoning** — a list that explains itself has become a guide.

| List | Holds |
|---|---|
| [`commands.md`](commands.md) | every command, grouped by when you run it, and whether the binary or an agent runs it |
| [`skills.md`](skills.md) | every skill: what each takes, what it does, who starts it |
| [`refusals.md`](refusals.md) | every refusal and hook denial: what you were told, and what to do |

**Each is held to its source by the suite**, because a short list that silently loses a line reads
complete. The skills list is checked against the skills on disk and each one's arguments; the refusals
list against the entries in the refusals guide.

**The cycle itself, step by step, is not here.** It is `../cheat-sheet.md`, one page.

---

## `commands.md`

**Every command**, and whether the binary or an agent runs it, in one table grouped by moment: once per
machine, once per repository, the cycle, any time, stopping. Ends with what a cycle leaves on disk.

The reasoning behind each line is in `../guides/running-a-cycle.md` and
`../guides/adopting-the-framework.md`.

## `skills.md`

**The skills in the order a cycle meets them**, one row each: its name, what it takes, what it does, and
who decides it should run. Each name links to the skill's full entry in `../../skills/README.md`, which
is where the syntax, refusals and reasoning live.

## `refusals.md`

**Every refusal as one line**: the exit codes, then what you were told and the next command, grouped by
what refused. Each name links to its entry in `../guides/handling-refusals.md`.

What it does not cover: answering a blocking finding or a blocked verification, which is a walk rather
than a line — `../guides/handling-blockers.md`.

## See also

- [The cheat sheet](../cheat-sheet.md) — the cycle on one page
- [The guides](../guides/README.md) — the reasoning behind each line
- [The mechanisms](../mechanisms/README.md) — how one works and why
- [The components](../README.md) — every concept the framework names, defined once
- [The skills reference](../../skills/README.md)
