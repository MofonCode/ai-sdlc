export const meta = {
  name: 'sdlc-parallel-build',
  description: 'Build an approved plan.md by dispatching one implementer per file-disjoint slice, then verify the whole change in fresh context',
  phases: [
    { title: 'Build', detail: 'One implementer per slice, parallel within each dependency wave' },
    { title: 'Verify', detail: 'Fresh-context adversarial verification against spec.md and plan.md' },
  ],
}

// Invoke with args = { slug, slices } taken verbatim from the approved plan.md frontmatter:
//   { slug: "order-book-snapshots",
//     slices: [ { id, files: [...], requirements: [...], depends_on: [...] }, ... ] }
//
// The manifests MUST already be pairwise disjoint - /plan proves this before offering the
// build. This script re-checks rather than trusting it, because a stale args payload is the
// one way two implementers end up holding the same file.

const { slug, slices } = args

// --- Disjointness, re-proven here. A failure is a planning bug, not a runtime condition. ---
const owner = new Map()
for (const s of slices) {
  for (const f of s.files) {
    if (owner.has(f)) {
      throw new Error(
        `plan.md manifest overlap: "${f}" claimed by both "${owner.get(f)}" and "${s.id}". ` +
          `Fix .sdlc/${slug}/plan.md and re-approve - do not reconcile this in the merge.`
      )
    }
    owner.set(f, s.id)
  }
}

// --- Dependency waves. Everything in a wave has no unmet dependency, so it runs at once. ---
const waves = []
const done = new Set()
let remaining = [...slices]
while (remaining.length) {
  const ready = remaining.filter((s) => (s.depends_on ?? []).every((d) => done.has(d)))
  if (!ready.length) {
    throw new Error(
      `Dependency cycle or unknown depends_on among: ${remaining.map((s) => s.id).join(', ')}`
    )
  }
  waves.push(ready)
  ready.forEach((s) => done.add(s.id))
  remaining = remaining.filter((s) => !ready.includes(s.id))
}

const SLICE_RESULT = {
  type: 'object',
  properties: {
    sliceId: { type: 'string' },
    filesTouched: { type: 'array', items: { type: 'string' } },
    requirementsSatisfied: { type: 'array', items: { type: 'string' } },
    buildPassed: { type: 'boolean' },
    notes: { type: 'string' },
  },
  required: ['sliceId', 'filesTouched', 'buildPassed'],
}

const built = []
for (const wave of waves) {
  phase('Build')
  const results = await parallel(
    wave.map((s) => () =>
      agent(
        `Implement slice "${s.id}" of .sdlc/${slug}/plan.md.

Read .sdlc/${slug}/spec.md and .sdlc/${slug}/plan.md first. Implement ONLY requirements ${JSON.stringify(s.requirements ?? [])}.

You may create or modify ONLY these files:
${s.files.map((f) => `  - ${f}`).join('\n')}

This is a hard boundary. Other implementers are editing other files concurrently, so touching
anything outside this list corrupts their work. If you believe the slice cannot be completed
without an unlisted file, STOP and report that instead of editing it - that is a planning bug
and it needs a plan revision, not a workaround.

Follow the conventions in CLAUDE.md and the patterns the spec cites. Write the tests named for
this slice. Run the build and the tests, and report the real result including failures.`,
        { label: `build:${s.id}`, phase: 'Build', schema: SLICE_RESULT }
      )
    )
  )
  built.push(...results)

  const broken = results.filter((r) => !r.buildPassed)
  if (broken.length) {
    // Later waves depend on these, so a green build is a precondition, not a preference.
    return {
      slug,
      status: 'halted',
      failedSlices: broken.map((r) => ({ sliceId: r.sliceId, notes: r.notes })),
      completedSlices: built.filter((r) => r.buildPassed).map((r) => r.sliceId),
    }
  }
}

// --- Verification, once, over the whole change. Fresh context is the point. ---
phase('Verify')
const verdict = await agent(
  `Verify the completed change for .sdlc/${slug}/ against its spec.md and plan.md.

Follow the "verifier" agent method: read the spec and plan before the code, run the build and
tests and report the real output, walk every requirement for both implementation and test
coverage, compare git diff --name-only against the plan manifest, hunt silent failures, and
check time-series contamination (lookahead, train/test bleed, survivorship, restatement) if
this code touches ordered data.

Slices built: ${built.map((r) => r.sliceId).join(', ')}`,
  {
    label: `verify:${slug}`,
    phase: 'Verify',
    schema: {
      type: 'object',
      properties: {
        ship: { type: 'boolean' },
        blocking: { type: 'array', items: { type: 'string' } },
        shouldFix: { type: 'array', items: { type: 'string' } },
        unplannedFiles: { type: 'array', items: { type: 'string' } },
        uncoveredRequirements: { type: 'array', items: { type: 'string' } },
      },
      required: ['ship', 'blocking'],
    },
  }
)

return { slug, status: 'built', waves: waves.map((w) => w.map((s) => s.id)), built, verdict }
