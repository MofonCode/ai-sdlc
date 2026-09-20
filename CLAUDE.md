# ai-sdlc

This repository **is** the SDLC framework — the engine (`plugins/sdlc-core`), the stack packs
(`plugins/stack-dotnet`), and the artifact chain it builds itself with (`.sdlc/`). It is built
by its own process, so the rules below are not advice about this repo; they are the repo.

## Read these before acting

- **`.sdlc/CHARTER.md`** — the invariants every change is held to, the technology commitments,
  the register of silent failures and the named check that catches each one, and the amendment
  log. Approved, versioned, and binding. Nothing here is a preference.
- **`.sdlc/DIRECTION.md`** — the diagnosis behind the current order of work, and the ideas that
  are not chains yet. Read it before starting anything: it says which chain is next and why, and
  it records why the identifier chain was parked after eleven interrogations rather than a
  twelfth.
- **`.sdlc/OPEN.md`** — the handful of outstanding items that exist in no other file. It
  deliberately does **not** list open chains, because those record their own state; read it for
  what is invisible, not for what is in flight.
- **`.sdlc/<slug>/`** — one directory per change, holding `intent.md` → `spec.md` → `plan.md` →
  `verification.md`. Each document's `status:` line is its stage, and a parked or blocked one
  says so in a banner at the top of its intent, with the date and the reasoning. **This is the
  list of what is in flight.** Do not keep a second copy of it anywhere.

## Two rules that are easy to violate by accident

**Never put a second list beside a source of truth.** This repository has been bitten four times
— a file table beside the manifest that declares those files, an output template beside the
schema it implements, a charter line beside the layout it claims to scaffold, and a register of
checks beside the suite that holds them. Each drifted silently and each needed a test built to
hold the two together. If a second list is genuinely necessary, the check that compares it to
the first is part of the same change.

**Never restore a file with `git checkout --`.** Uncommitted work has been destroyed here doing
exactly that while reverting a mutation test. Copy the file aside first and copy it back.

## Talking about the work

Documents are indexed on purpose — requirements are `R1`, invariants `I1`, findings `F1`, and
that indexing is what makes them referenceable. **In conversation, cite the index and explain
it.** *Label, dash, what it says.* Never the bare identifier on its own. The full rule is in
`plugins/sdlc-core/skills/sdlc-router/references/plain-language.md` and a test holds every engine
skill to pointing at it.

## Before committing

`dotnet test tests/AiSdlc.Tests/AiSdlc.Tests.csproj` — the whole suite, real output seen. It
takes about six minutes, mostly because two rows generate real solutions and build them. Nothing
is committed on a red build; that is charter invariant I12, not a habit.
