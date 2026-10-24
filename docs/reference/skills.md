# Skills

Every skill, one line each, in the order a cycle meets them. Each name links to its entry in
`../../skills/README.md`, which has the syntax, refusals and reasoning. Every skill is run by an agent;
*started by* is who decides it should happen.

In Claude Code a skill is `/sdlc:<name> <arguments>`. On a host that loads skills through Agent Plugins
there is no slash form: ask in a sentence, and include the cycle id.

| Skill | Takes | Does | Started by |
|---|---|---|---|
| **Adoption** | | | |
| [`init`](../../skills/README.md#init) | `[--platform github\|azure-devops\|local] [--integration-branch <name>]` | interviews you about the solution and proposes its first constraints; hands you the adoption command, and walks the drafts it leaves | a person\* |
| **The planning chain** | | | |
| [`intent`](../../skills/README.md#intent) | `[--type bug\|add\|change] [--from <lead>]` | opens a cycle and interviews you into why the work exists, from a lead if given one | a person |
| [`spec`](../../skills/README.md#spec) | `<cycle id>` | derives testable acceptance criteria from the intent alone | a person |
| [`plan`](../../skills/README.md#plan) | `<cycle id>` | decides how, and what will prove each criterion | a person |
| **The loop** | | | |
| [`interrogate`](../../skills/README.md#interrogate) | `<cycle id> <intent\|spec\|plan\|verify>` | judges one document against the rules, in a fresh process. Never edits | a stage skill, or a person |
| [`refine`](../../skills/README.md#refine) | `<cycle id> <intent\|spec\|plan>` | applies a round's findings to that document. Never judges | a stage skill, or a person |
| **Correction** | | | |
| [`steer`](../../skills/README.md#steer) | `<cycle id> [<finding id>...] ["what to change"]` | carries a change of mind, or suggestions taken in or declined, down the intent, spec and plan | a person, or a stage's hand-back |
| **The gate** | | | |
| [`draft`](../../skills/README.md#draft) | `approval <id> \| abandon <id> \| solution [--standard profile\|existing] \| techstack [--empty]` | prints what belongs in a file only a person writes | a person |
| **Build and verify** | | | |
| [`build`](../../skills/README.md#build) | `<cycle id>` | implements the approved plan, builds, tests, records the evidence | a person, or `batch-build` |
| [`verify`](../../skills/README.md#verify) | `<cycle id>` | judges the build against the spec from the evidence alone | a person, or `batch-build` |
| [`remediate`](../../skills/README.md#remediate) | `<cycle id>` | fixes source and tests after a `blocked` verification | a person, or `batch-build` |
| **Pull requests and unattended runs** | | | |
| [`pr`](../../skills/README.md#pr) | `<cycle id> [--plan \| --draft] \| --all` | pushes the cycle's branch and opens its pull request into the integration branch: a verified build, an approved plan, or a draft; asks before every push | a person\*, or `batch-build` |
| [`batch-build`](../../skills/README.md#batch-build) | `[--pr] [--unattended]` | builds and verifies every queued cycle, each on its own branch, then reports the sitrep | a person, or the factory |
| [`resolve-conflict`](../../skills/README.md#resolve-conflict) | `<cycle id>` | brings a pull request up to date with the integration branch when it conflicts or goes red, inside the declared files, then verifies again | a person |
| **Reading and deciding** | | | |
| [`status`](../../skills/README.md#status) | `<cycle id> [intent\|spec\|plan]` | where a cycle stands, and the next command | anyone |
| [`constraints`](../../skills/README.md#constraints) | `[intent\|spec\|plan\|diff\|verify]` | the rules in force, all tiers resolved | every stage skill, first |
| [`suggestions`](../../skills/README.md#suggestions) | `[cycle id]` | what was noticed and not acted on | a person |
| [`propose`](../../skills/README.md#propose) | `convergence \| constraints \| decisions \| theme` | proposes entries for a person to adopt, with the evidence behind each — `convergence` the rules, decisions and stack together, after a cycle merges | a person |

\* **A person must start these two.** Every other skill may be started by an agent as well — nothing in
the plugin stops one — so *started by* there is who normally decides. These two are different, because
each does something an agent should not decide on its own behalf:

- **`init` is enforced.** The skill only interviews; the adoption command it hands you is denied to an
  agent by the pre-tool hook, so you run it yourself. Adoption writes the hook configuration that
  constrains the agent, the pipeline that gates the merge, and the file saying where the code came from —
  an agent that could run it would be setting up its own guardrails.
- **`pr` is asked, not enforced.** The skill asks you before every push, and no hook blocks a push. It is
  the only stage that reaches outside the repository: a pull request notifies people and lands in a
  queue, and withdrawing one is something somebody has to notice. What stops an unready cycle from
  merging is the pull-request check, not the question.

## See also

- [The skills reference](../../skills/README.md) — each skill's syntax, refusals and reasoning
- [Commands](commands.md) — every command, binary and agent alike
- [Refusals](refusals.md) — what to do when something refuses
