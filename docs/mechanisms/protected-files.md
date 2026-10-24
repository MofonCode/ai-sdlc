# Protected files

Which files in a governed solution only a person may change, how the framework stops an agent changing
them, and what that guard deliberately leaves alone. Read it when an agent tells you it cannot write a
file, or before you go looking for the setting that would let it.

**There is no such setting, and that is the design.** The rest of this page is why. Making a change to
one of them is a task, and it lives in What your solution owns in
`../guides/adopting-the-framework.md`: which tool prepares each file, and the step that is yours.

## What is guarded

**Six files, and one command.**

| Guarded | Where | What it holds |
|---|---|---|
| `solution.md` | `.sdlc/` | where the code came from, which conventions new work follows, where it is pushed, and the paragraph saying what the solution is |
| `techstack.md` | `.sdlc/` | what the solution is built from, which stack rules it is held to, and which build settings files it takes |
| `constraints.md` | `.sdlc/` | this solution's own `LOC_` rules |
| `decisions.md` | `.sdlc/` | the choices this solution has settled |
| `approval.md` | `.sdlc/cycles/<id>/` | the gate decisions on one cycle's three planning documents |
| `abandoned.md` | `.sdlc/cycles/<id>/` | why one cycle stopped |
| the adoption command | — | it writes the hook configuration that does the guarding |

**Each one is a decision, and a decision means something only if no tool could have made it.** Most of
them are also something a cycle is judged against — the constraints, the declared stack, the recorded
decisions — and a cycle able to edit the rule it is judged by can satisfy any rule by moving it. Why each
file is a person's, file by file, is in the denial table of `../guides/handling-refusals.md`.

**Each of the first three carries a note at its top** saying that only a person changes it. Adoption
writes the note into the drafts it leaves you, so the file explains a denied agent write before the
denial reads like a broken install.

## How it is guarded

**A pre-tool hook.** Adoption writes one hook configuration per supported host — `.claude/settings.json`,
`.github/hooks/sdlc.json`, `.agents/hooks.json` — and each tells the host to ask `sdlc` before the agent
runs a tool. `sdlc` answers allow or deny, and on a denial the host shows the agent the reason. The agent
never gets to make the call.

**Two rules decide it, one per kind of tool call.**

| The agent is about to | Denied when |
|---|---|
| write or edit a file | the path has a `.sdlc` segment anywhere in it, and its last segment is one of the six names. Absolute, relative, either separator |
| run a shell command | the command names the adoption verb; or a **redirect points at** one of the six files; or one piece of the command — between `;`, `&&`, `\|\|`, `\|` and newlines, outside quotes — runs a **write verb** (a move, a copy, a delete, an in-place edit) **and names one of the six as a whole name** |

**The shell rule reads the text of the command, not the paths it would touch.** It does not parse the
line or require `.sdlc` to appear, because a command can change directory first. That makes it broad on
purpose: a false refusal costs you one rephrasing, and a miss costs the guarantee. Two consequences you
will meet:

- **A commit message naming a protected file can be refused** when an agent runs the commit — a message
  reading *Move techstack.md into place* has both a write-shaped word and the file name in its text. `git commit -F <file>` with the message in a file is the way past it, and that is the rule
  working rather than a hole.
- **Moving a draft onto a protected file is refused in every spelling** — `mv`, `Move-Item`,
  `Rename-Item`, `Copy-Item`, through any shell the host offers. Placing a draft is the decision, so it
  is the step the rule exists to keep.

**The rules are absolute.** There is no mode, no flag, no environment variable and no check on which
skill is running, so there is nowhere for an exception to be carved out — and nowhere for a bypass to
live. That is also why the whole rule states in one sentence: every agent write to these six files is
denied.

## What is not guarded

| | |
|---|---|
| **reading them** | `cat approval.md` is allowed. Refusing it would make the gate obstructive without making it safer |
| **the drafts beside them** | `solution.draft.md`, `techstack.draft.md`, `constraints.draft.md`, `approval.<decision>.draft.md`. An agent may write and edit these freely; only the rename onto the real file is denied |
| **the theme** | `.sdlc/theme.json`. Nobody is judged against a colour, so a cycle may declare it and change a value like any other file |
| **you** | the hook runs on an agent's tool calls. A command you type in your own terminal, or on Claude Code a line you run with a leading `!`, is yours and is never asked about |

**So the guard is not a lock on the file.** It stops the agent from making a decision that is yours.
What stops a governed change from merging without the chain behind it is a different layer — the
pull-request check — and `../guides/installing-the-plugin.md` and `../guides/adopting-the-framework.md` say how that one is
made required.

## Where to go next

| | |
|---|---|
| why each file is a person's, and every other denial | `../guides/handling-refusals.md` |
| placing the first three, and changing any of the six | `../guides/adopting-the-framework.md` |
| changing the declared stack | `../guides/declaring-the-stack.md` |
| approving or abandoning a cycle | `../guides/running-a-cycle.md` |
