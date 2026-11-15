# Skills

Every skill, one line each, in the order a cycle meets them. Each name links to its entry in
`../../skills/README.md`, which has the syntax, refusals and reasoning. Every skill is run by an agent;
*started by* is who decides it should happen.

**A cycle is five steps**, and a person can name each in a sentence rather than a command: begin an
intent (`intent`), take it to plan (`to-plan`, with `steer` to have it otherwise), approve (`draft`), build it
(`build`, which verifies, remediates and opens the pull request), and review the pull request — merging it, or
asking for a change (`remediate`). The rest are run by those, or are for after a cycle.

**Two kinds** (D-186): a **playbook** — `to-plan`, `next`, `build`, `remediate` —
composes the skills it runs and does no stage work; every other skill does one job and starts none, whoever starts it. The skills index has
the table.

In Claude Code a skill is also `/sdlc:<name> <arguments>`. On a host that loads skills through Agent Plugins
there is no slash form: ask in a sentence, and include the cycle id.

| Skill | Takes | Does | Started by |
|---|---|---|---|
| **Adoption** | | | |
| [`init`](../../skills/README.md#init) | `[--platform github\|azure-devops\|local] [--integration-branch <name>]` | interviews you about the solution and proposes its first constraints; hands you the adoption command, and walks the drafts it leaves | a person\* |
| **The planning chain** | | | |
| [`intent`](../../skills/README.md#intent) | `[--type bug\|add\|change] [--from <lead>] \| --reslice <cycle id>...` | slices the work into atomic cycles once you accept it, opens each, and interviews you into why the work exists, from a lead if given one — a ready successor included; with `--reslice`, cuts again intents already written that have no spec | a person |
| [`spec`](../../skills/README.md#spec) | `[<cycle id>]` | derives testable acceptance criteria from the intent alone | `to-plan`, or a person |
| [`plan`](../../skills/README.md#plan) | `[<cycle id>]` | decides how, and what will prove each criterion | `to-plan`, or a person |
| [`to-plan`](../../skills/README.md#to-plan) | `[<cycle id>] [<finding id>...] ["what to change"]` | takes an intent to a plan — one cycle, or every one this branch carries — picking up where each stopped, and hands back once with the approvals drafted; given what should change, runs `steer` first and checks again | a person |
| [`next`](../../skills/README.md#next) | `` | pulls the next wave from the backlog, plans it in one sandbox, and stops at the approvals | a person |
| **Judging and editing** | | | |
| [`interrogate`](../../skills/README.md#interrogate) | `[<cycle id>] <intent\|spec\|plan\|verify>` | judges one document against the rules, in a fresh process. Never edits | `to-plan` in its loop, or a person |
| [`refine`](../../skills/README.md#refine) | `[<cycle id>] <intent\|spec\|plan>` | applies a round's findings to that document. Never judges | `to-plan` in its loop, or a person |
| **Correction** | | | |
| [`steer`](../../skills/README.md#steer) | `[<cycle id>] [<finding id>...] ["what to change"]` | writes a change of mind, or suggestions taken in or declined, into the intent, spec and plan; checking them again is `to-plan`'s | `to-plan`, or a person |
| **The gate** | | | |
| [`draft`](../../skills/README.md#draft) | `approval <id> \| abandon <id> \| solution [--standard profile\|existing] \| techstack [--empty]` | prints what belongs in a file only a person writes | a person |
| **Build and verify** | | | |
| [`build`](../../skills/README.md#build) | `[<cycle id>] [--unattended]` | a playbook: carries the approved cycle — or every one, given none — to its pull request. `sdlc build` admits the plan and starts it again in a process of its own, which runs `implement`, `verify`, `remediate` and `pr`; hands back the report | a person, or the factory |
| [`implement`](../../skills/README.md#implement) | `<cycle id>` | changes code and tests to do what it is handed — the plan, a verification's findings, or a person's instruction — records deviations and choices, and records the evidence | `build` or `remediate` |
| [`verify`](../../skills/README.md#verify) | `[<cycle id>]` | judges the build against the spec from the evidence alone, in a fresh process | `build`, `remediate`, or a person |
| [`remediate`](../../skills/README.md#remediate) | `[<cycle id>]` | a playbook, *fix it*: after a `blocked` verification, an escalation with your instruction, a change you ask for at the pull request, or a pull request that conflicts — `resolve-conflict` first on a conflict — runs `implement` and `verify` while it converges, deviating once before it escalates, and `pr` updates the pull request | `build`, or a person |
| **Pull requests and conflicts** | | | |
| [`pr`](../../skills/README.md#pr) | `[<cycle id>...] [--plan \| --draft] \| --plan \| --all` | pushes the cycle's branch and opens its pull request into the integration branch: a verified build, an approved plan — or a slicing's plans together, or a sandbox's approved plans with `--plan` alone — or a draft; started by hand, it asks before the push, and the build's own process opens one without asking, because saying *build it* was the consent | a person\*, `build`, or `remediate` |
| [`resolve-conflict`](../../skills/README.md#resolve-conflict) | `<cycle id>` | brings a pull request up to date with the integration branch when it conflicts or goes red, inside the declared files, and records fresh evidence; `remediate` verifies | `remediate`, or a person |
| **Reading and deciding** | | | |
| [`status`](../../skills/README.md#status) | `[<cycle id>] [intent\|spec\|plan]` | where a cycle stands, and the next command | anyone |
| [`constraints`](../../skills/README.md#constraints) | `[intent\|spec\|plan\|diff\|verify]` | the rules in force, all tiers resolved | every stage skill, first |
| [`suggestions`](../../skills/README.md#suggestions) | `[cycle id] [--all]` | what was noticed and not acted on, each concern once; with `--all`, what became of each | a person |
| [`propose`](../../skills/README.md#propose) | `constraints \| decisions \| theme` | proposes entries for a person to adopt, with the evidence behind each | a person |
| [`mine`](../../skills/README.md#mine) | `` | reads every finished cycle at once, proposes the rules, decisions, stack, areas and successors together, and records the sitting so mined cycles become husks — once a verify passes, on the cycle's branch or after the merge | a person |

\* **A person must start these two.** Every other skill may be started by an agent as well — nothing in
the plugin stops one — so *started by* there is who normally decides. These two are different, because
each does something an agent should not decide on its own behalf:

- **`init` is enforced.** The skill only interviews; the adoption command it hands you is denied to an
  agent by the pre-tool hook, so you run it yourself. Adoption writes the hook configuration that
  constrains the agent, the pipeline that gates the merge, and the file saying where the code came from —
  an agent that could run it would be setting up its own guardrails.
- **`pr` is asked, not enforced.** Started by hand, the skill asks you before the push, and no hook blocks a
  push; inside *build it* the question was asked once, when you said it (D-178). It is
  the only stage that reaches outside the repository: a pull request notifies people and lands in a
  queue, and withdrawing one is something somebody has to notice. What stops an unready cycle from
  merging is the pull-request check, not the question.

## See also

- [The skills reference](../../skills/README.md) — each skill's syntax, refusals and reasoning
- [Commands](commands.md) — every command, binary and agent alike
- [Refusals](refusals.md) — what to do when something refuses
