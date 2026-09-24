export const meta = {
  name: 'repair',
  description: 'Repair blocking findings, re-check each repair diff, terminate on no-progress',
  whenToUse: 'After interrogate returns blocking findings. Pass { artifact, findings }.',
  phases: [
    { title: 'Repair', detail: 'one agent per blocking finding that carries a repair' },
    { title: 'Recheck', detail: 'per repair: did it resolve the finding, and what did it introduce' },
  ],
}

// ---------------------------------------------------------------------------
// v01 measured this and it replicated on the first attempt here: round 2 found
// 3-of-5 blocking items INSIDE round 1's repairs, and 3-of-4 when repeated. The
// finding rate does not decay, so "iterate until clean" never fires and a fixed
// round cap ships known-broken artifacts wearing a clean label.
//
// Two consequences shape this script:
//
//   * Termination is NO-PROGRESS, not clean and not a count. A round that fails to
//     reduce the blocking population twice running halts and escalates.
//   * A recheck looks for what the repair INTRODUCED, not only whether it worked.
//     Repairs are the most dangerous surface, and a recheck that merely confirms
//     the fix is blind to the thing that actually goes wrong.
// ---------------------------------------------------------------------------

const input = args || {}
const ARTIFACT = input.artifact
const CHARTER = input.charter || '.sdlc/CHARTER.md'
const STALL_LIMIT = input.stallLimit || 2

if (!ARTIFACT) {
  throw new Error('repair needs { artifact, findings }')
}
if (!input.findings || !Array.isArray(input.findings)) {
  throw new Error('repair needs a findings array — run interrogate first')
}

const CATEGORIES = [
  'unresolvable-reference',
  'unfounded-claim',
  'scope-drift',
  'gate-bypass',
  'internal-contradiction',
  'duplicated-truth',
  'ambiguity',
  'untestable',
  'missing-case',
  'charter-violation',
  'unclassified',
]

const STAGES = ['charter', 'intent', 'spec', 'plan', 'build', 'verify']

const RECHECK_SCHEMA = {
  type: 'object',
  required: ['resolved', 'reason'],
  properties: {
    resolved: { type: 'boolean', description: 'Does the change actually address the finding it claims to fix?' },
    reason: { type: 'string' },
    introduced: {
      type: 'array',
      description: 'Defects the repair itself created. This is the field that matters.',
      items: {
        type: 'object',
        required: ['category', 'stage', 'summary', 'anchor'],
        properties: {
          category: { enum: CATEGORIES },
          stage: { enum: STAGES },
          summary: { type: 'string' },
          anchor: { type: 'string' },
          secondAnchor: { type: 'string' },
          cited: { type: 'boolean', description: 'True only if it demonstrably violates a declared invariant or contradicts itself.' },
          citeKind: { enum: ['invariant', 'check', 'deferred', 'locations', 'none'] },
          citeId: { type: 'string' },
          repair: { type: 'string' },
        },
      },
    },
  },
}

const APPLY_SCHEMA = {
  type: 'object',
  required: ['applied'],
  properties: {
    applied: { type: 'boolean' },
    diff: { type: 'string', description: 'What changed, as a unified diff or a precise before/after.' },
    note: { type: 'string' },
  },
}

// ---------------------------------------------------------------------------

let pool = input.findings.slice()
let round = input.round || 1
let stalls = 0
let previousBlockingCount = null

const history = []
const escalated = []

function blockingWithRepair(findings) {
  return findings.filter(f => f && f.state === 'blocking' && f.repair)
}

function blockingWithoutRepair(findings) {
  return findings.filter(f => f && f.state === 'blocking' && !f.repair)
}

// A blocking finding carrying no repair cannot self-heal, so it escalates
// immediately rather than being carried round after round. A deferred-decision
// blocker is always in this class: no artifact edit resolves "nobody has decided
// this yet".
function harvestEscalations() {
  for (const f of blockingWithoutRepair(pool)) {
    escalated.push(f)
  }
  pool = pool.filter(f => !(f.state === 'blocking' && !f.repair))
}

harvestEscalations()

while (true) {
  const repairable = blockingWithRepair(pool)

  if (repairable.length === 0) {
    log(`round ${round}: nothing left to repair`)
    break
  }

  if (stalls >= STALL_LIMIT) {
    log(`round ${round}: no progress in ${stalls} consecutive rounds — halting and escalating`)
    for (const f of repairable) escalated.push(f)
    pool = pool.filter(f => f.state !== 'blocking')
    break
  }

  log(`round ${round}: repairing ${repairable.length} blocking finding(s)`)

  phase('Repair')

  // Each finding repairs and rechecks independently — a pipeline, not a barrier.
  // There is no cross-finding dependency, so waiting for the slowest repair before
  // starting any recheck would waste the fast ones' time for nothing.
  const outcomes = await pipeline(
    repairable,

    (finding) =>
      agent(
        [
          'Apply exactly one repair to an artifact, and nothing else.',
          '',
          `Artifact: ${ARTIFACT}`,
          '',
          'Finding to repair:',
          `  category: ${finding.category}`,
          `  summary:  ${finding.summary}`,
          `  repair:   ${finding.repair}`,
          '',
          'Make the smallest change that resolves this finding. Do not tidy anything',
          'else, do not improve neighbouring prose, do not fix other problems you',
          'notice — a wider diff is a wider surface for the next round to find defects',
          'in, and that is measurably what goes wrong here.',
          '',
          'Report what you changed precisely enough that another agent can judge it',
          'without re-reading the whole artifact.',
        ].join('\n'),
        { label: 'fix ' + finding.category, phase: 'Repair', schema: APPLY_SCHEMA },
      ).then(applied => ({ finding, applied })),

    (prev, finding) =>
      agent(
        [
          'Check one repair. You have two jobs and the second is the important one.',
          '',
          `Artifact: ${ARTIFACT}`,
          `Charter: ${CHARTER}`,
          '',
          'The finding this claimed to fix:',
          `  category: ${finding.category}`,
          `  summary:  ${finding.summary}`,
          '',
          'What the repair changed:',
          '```',
          (prev && prev.applied && prev.applied.diff) || '(no diff reported)',
          '```',
          '',
          'FIRST: does the change actually resolve that finding? Not "is it an',
          'improvement" — does it resolve *that* finding.',
          '',
          'SECOND, and this is why you are here: what did the repair INTRODUCE?',
          'Repairs are the most dangerous surface in this process. Measured twice, on',
          'two codebases: roughly three in four repairs create a new blocking defect —',
          'usually a contradiction between the repaired passage and something it used',
          'to agree with. Read the change against its surroundings, not in isolation.',
          '',
          'Mark a new defect `cited` only if it demonstrably violates an invariant',
          'declared in the charter, or contradicts another passage you can point at.',
          'Uncited defects are still worth reporting — they become suggestions.',
          '',
          'Return an empty `introduced` array if the repair is genuinely clean. That',
          'happens and is a real result; do not manufacture a defect to look thorough.',
        ].join('\n'),
        { label: 'recheck ' + finding.category, phase: 'Recheck', schema: RECHECK_SCHEMA },
      ).then(recheck => ({ finding, applied: prev && prev.applied, recheck })),
  )

  // Fold the round's results back into the pool.
  let resolvedCount = 0
  let introducedCount = 0
  const next = []

  for (const f of pool) {
    const outcome = outcomes.find(o => o && o.finding && o.finding.id === f.id)

    if (!outcome) {
      next.push(f)
      continue
    }

    const recheck = outcome.recheck || {}

    if (recheck.resolved) {
      resolvedCount++
      next.push(Object.assign({}, f, {
        state: 'suggested',
        disposition: 'fixed',
      }))
    } else {
      // Still blocking. Drop the repair so the next round does not retry a change
      // already shown not to work; without this the loop can spin on one finding.
      next.push(Object.assign({}, f, {
        repair: undefined,
        'recheck-note': recheck.reason || 'repair did not resolve the finding',
      }))
    }

    for (const intro of recheck.introduced || []) {
      introducedCount++
      const cites = []

      if (intro.cited && intro.citeKind && intro.citeKind !== 'none') {
        if (intro.citeKind === 'locations') {
          if (intro.anchor && intro.secondAnchor) {
            cites.push({
              kind: 'locations',
              locations: [
                { file: ARTIFACT, anchor: intro.anchor },
                { file: ARTIFACT, anchor: intro.secondAnchor },
              ],
            })
          }
        } else if (intro.citeId) {
          cites.push({ kind: intro.citeKind, id: intro.citeId })
        }
      }

      // State is assigned here, in code, from the citation — never by the agent that
      // found it. RULE 4: a location pair is self-evident only for a contradiction.
      // RULE 2: unclassified can never block.
      const citesOk = cites.length > 0 &&
        !(cites[0].kind === 'locations' && intro.category !== 'internal-contradiction')
      const state = (intro.category === 'unclassified' || !citesOk) ? 'suggested' : 'blocking'

      next.push({
        id: 'i' + round + '-' + introducedCount,
        category: intro.category,
        state: state,
        stage: intro.stage,
        detection: 'reasoning',
        summary: intro.summary,
        cites: citesOk ? cites : undefined,
        repair: intro.repair || undefined,
        'introduced-by-repair-of': outcome.finding.id,
      })
    }
  }

  pool = next
  harvestEscalations()

  const blockingNow = pool.filter(f => f.state === 'blocking').length

  history.push({
    round: round,
    repaired: repairable.length,
    resolved: resolvedCount,
    introduced: introducedCount,
    blockingAfter: blockingNow,
  })

  log(`round ${round}: ${resolvedCount} resolved, ${introducedCount} introduced by the repairs, ${blockingNow} blocking left`)

  // NO-PROGRESS, not "clean". A round that does not reduce the blocking population
  // has not made progress, however much work it did — and the measurement says that
  // is the normal case rather than the exception.
  if (previousBlockingCount !== null && blockingNow >= previousBlockingCount) {
    stalls++
    log(`no reduction (${previousBlockingCount} → ${blockingNow}); stall ${stalls}/${STALL_LIMIT}`)
  } else {
    stalls = 0
  }

  previousBlockingCount = blockingNow
  round++
}

const stillBlocking = pool.filter(f => f.state === 'blocking')

return {
  artifact: ARTIFACT,
  rounds: round - (input.round || 1),
  history: history,
  converged: stillBlocking.length === 0 && escalated.length === 0,
  escalated: escalated,
  findings: pool,
  // The caller decides what to do with an unconverged result. It does not silently
  // pass: an artifact leaves this loop either clean or with a named reason.
  outcome: stillBlocking.length === 0 && escalated.length === 0
    ? 'converged'
    : (stalls >= STALL_LIMIT ? 'halted-no-progress' : 'escalated'),
}
