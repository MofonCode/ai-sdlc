# Reference

Material kept because it is worth reading, not because anything reads it. Nothing here is a test
fixture and nothing validates against the current schema.

## `dice-roll-rev-7-review.md`

A review of one run of the framework from adoption to build, in `dice-roll-rev-7` on 2026-10-05: eight
findings about the framework, each with what it would take to remedy it and whether the remedy is a
judgement somebody has to make. Written from the artifacts, the transcript and the running
application — the solution was rebuilt and driven in a browser rather than taken on the evidence's
word.

It is a review rather than a plan, so it goes stale as the findings are acted on. The four it marks as
needing no decision are the ones to check first against what the code now does.

## `iteration-3-cycle/`

One complete cycle, run for real in `dice-trial` on 2026-09-29 under iteration 3: an intent through
two interrogation rounds, a spec, a plan, a build with its evidence, and a verification. It is the
best worked example the project has, and it is here because `dice-trial` is a trial repository that
gets cleared.

**It is in iteration 3's shape, not this one.** The frontmatter is `id` rather than `cycle`, the cycle
id is `20260929_4e1a` rather than `20260929_1456_32`, the intent has a `Who` section and a `Done looks
like` section, and there is no `## Open` on every artifact. So it would fail `ArtifactSchema` on
several counts, and that is expected — **do not make it pass.** Its value is the reasoning, and
reshaping it to the current envelope would mean editing the one artifact nobody edited under pressure.

### What to read it for

**The intent is the calibration target.** Four things in it are what good looks like:

- *"this repository holds no solution at all, so the need is unmet by absence rather than by something
  that works badly"* — the problem stated without a solution in it, for a case where nothing exists.
- *"Two askings in a row may therefore show the same number, and that is impartiality working rather
  than a fault: no criterion may require consecutive rolls to differ"* — an intent closing off a
  criterion the spec would otherwise have been tempted to write. This is the sentence to point at when
  somebody asks what an intent is for.
- *"Two further bounds were offered in the interview and **not** taken, so they are not bounds and must
  not be treated as any"* — a declined option recorded as declined. Most interviews lose this.
- Four open items, each one genuinely unsettleable with the reason it is, and one of them explicitly
  warning the spec not to derive a criterion from it. That is `CON_14` satisfied before `CON_14`
  existed.

**The findings rounds show what interrogation actually produces.** Six rounds across three artifacts,
and the second round of each is where the interesting objections are. Round seven of a different intent
found that a headline change was unreachable; this cycle is smaller, and still took two rounds per
artifact to settle.

**The evidence shows what a verification has to work from.** `tests.trx` is a real runner's output, and
`diff.patch` is a real diff — which is what the normalised test summary exists to sit beside rather
than replace.

### Two things it also records about iteration 3

Both are in `CONCEPT.md` §17 as the trial's findings, and this is where they came from:

- `dotnet test` exited zero without running the suite, because the runner was not configured. The
  evidence here is what made that visible.
- A criterion's proof rested entirely on a substituted source. Non-blocking, and substantive — which is
  why `suggestions` exists.
