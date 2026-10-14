# The cheat sheets

Three pages for when you already know why and need the line. **One line per thing, and a link for the
reasoning** — a cheat sheet that explains itself has become a guide.

| Cheat sheet | Holds |
|---|---|
| [`commands.md`](commands.md) | the short path from an unadopted repository to a pull request, then every command for one cycle |
| [`skills.md`](skills.md) | the eighteen skills: what each takes, what it does, who starts it |
| [`refusals.md`](refusals.md) | every refusal and hook denial: what you were told, and what to do |

**Each is held to its source by the suite**, because a short list that silently loses a line reads
complete. The skills sheet is checked against the skills on disk and each one's arguments; the refusals
sheet against the entries in the refusals guide.

---

## `commands.md`

**Every command for one cycle, in order**, and whether the binary or an agent runs it. Opens with the
short path — adoption in five lines and a cycle in nine — then a table per moment: once per machine,
once per repository, the cycle, any time, stopping. Ends with what a cycle leaves on disk.

The reasoning behind each line is in `../guides/running-a-cycle.md` and
`../guides/adopting-the-framework.md`.

## `skills.md`

**The skills in the order a cycle meets them**, one row each: its name, what it takes, what it does, and
who decides it should run. Each name links to the skill's full entry in `../../skills/README.md`, which
is where when, why, how and what live.

## `refusals.md`

**Every refusal as one line**: the exit codes, then what you were told and the next command, grouped by
what refused. Each name links to its entry in `../guides/handling-refusals.md`.

What it does not cover: answering a blocking finding or a blocked verification, which is a walk rather
than a line — `../guides/handling-blockers.md`.
