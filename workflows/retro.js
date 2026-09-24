export const meta = {
  name: 'retro',
  description: 'Write the retro that closes a landed cycle and feeds the corpus',
  whenToUse: 'At landing, after the merge. Pass { cycle, solution, landedAt, findings }.',
  phases: [
    { title: 'Collect', detail: 'the cycle\'s artifacts and its verify result' },
    { title: 'Write', detail: 'the retro, with its findings and dispositions' },
  ],
}

// ---------------------------------------------------------------------------
// A retro does two jobs that pull in different directions, and the design resolves
// them by weighting one over the other rather than pretending both are reliable:
//
//   IMMEDIATE evidence — self-assessed at close. Cheap, and structurally optimistic:
//   "did my plan work?" asked ten minutes after the plan passed its tests is a
//   biased question.
//
//   DELAYED evidence — a later cycle attributing its cause upstream via caused-by.
//   Slow, objective, and the only signal worth much. Weighted higher.
//
// So this document records what happened without grading it. The retro's honesty
// about the immediate half is the whole reason the delayed half exists.
//
// It is also the COMPLETION CERTIFICATE: its presence is what makes a cycle read as
// done. That is why it is written only at landing, and why a blocked, escalated or
// abandoned cycle writes none — a retro for unfinished work would report it complete.
// ---------------------------------------------------------------------------

const input = typeof args === 'string' ? { cycle: args } : (args || {})
const CYCLE = input.cycle || input.cycleId
const SOLUTION = input.solution || 'ai-sdlc'
const CHARTER = input.charter || '.sdlc/CHARTER.md'
const FINDINGS = input.findings || []
const ROUNDS = input.rounds || 1

// Date.now() throws inside a workflow — it would break resume — so the landing time
// is passed in and stamped rather than read.
const LANDED_AT = input.landedAt

if (!CYCLE) {
  throw new Error('retro needs a cycle id: pass it as args, e.g. "20260924-a1b2"')
}
if (!LANDED_AT) {
  throw new Error('retro needs landedAt: it is not derivable after the retro is copied into the corpus, and the workflow runtime cannot read a clock')
}

const CYCLE_DIR = input.cycleDir || `.sdlc/cycles/${CYCLE}`
const RETRO_PATH = input.retroPath || `${CYCLE_DIR}/retro.md`

const COLLECT_SCHEMA = {
  type: 'object',
  required: ['ok'],
  properties: {
    ok: { type: 'boolean' },
    verifyOutcome: { enum: ['passed', 'blocked', 'halted-tier-escalated', 'missing'] },
    suppressionCount: { type: 'integer' },
    criteriaTotal: { type: 'integer' },
    criteriaVerifiable: { type: 'integer' },
    criteriaCovered: { type: 'integer' },
    charterVersion: { type: 'integer' },
    artifactsPresent: { type: 'array', items: { type: 'string' } },
    note: { type: 'string' },
  },
}

const WRITE_SCHEMA = {
  type: 'object',
  required: ['written'],
  properties: {
    written: { type: 'boolean' },
    path: { type: 'string' },
    note: { type: 'string' },
  },
}

// ---------------------------------------------------------------------------

phase('Collect')

const collected = await agent(
  [
    `Collect what closes cycle ${CYCLE}. Read, do not write.`,
    '',
    `Cycle directory: ${CYCLE_DIR}`,
    `Charter:         ${CHARTER}`,
    '',
    'Read the frontmatter of verify.md in that directory and report its outcome, the',
    'suppression count, and the three criteria counts. List which artifacts are present',
    '— spec, plan, verify.',
    '',
    'Set verifyOutcome to "missing" if verify.md does not exist. Do not infer an',
    'outcome from the other artifacts: a retro written without a verify result is a',
    'completion certificate for work nothing confirmed.',
  ].join('\n'),
  { label: 'collect cycle', phase: 'Collect', schema: COLLECT_SCHEMA },
)

const verifyOutcome = (collected && collected.verifyOutcome) || 'missing'

// The certificate is only issued for work that actually landed. This refusal is the
// reason "retro exists" is a trustworthy derivation of done.
if (verifyOutcome !== 'passed') {
  log(`verify outcome is "${verifyOutcome}" — refusing to write a retro`)
  return {
    ok: false,
    stage: 'retro',
    'cycle-id': CYCLE,
    reason: verifyOutcome === 'missing'
      ? 'no verify.md: a retro would certify work nothing confirmed'
      : `verify reported "${verifyOutcome}": a retro would report an unfinished cycle as complete`,
    outcome: 'refused',
  }
}

const blocking = FINDINGS.filter(f => f && f.state === 'blocking').length
const suggested = FINDINGS.filter(f => f && f.state === 'suggested').length
const mechanical = FINDINGS.filter(f => f && f.detection === 'check').length
const reasoned = FINDINGS.filter(f => f && f.detection === 'reasoning').length

// The detection ratio is v01's 70%-mechanical measurement, made per-cycle rather than
// measured once. It says whether fact-gathering is absorbing more of the load over time.
const detectionRatio = FINDINGS.length > 0
  ? Math.round((mechanical / FINDINGS.length) * 100)
  : null

phase('Write')

const written = await agent(
  [
    `Write ${RETRO_PATH}.`,
    '',
    'Required frontmatter:',
    '```yaml',
    '---',
    `cycle-id: ${CYCLE}`,
    `solution: ${SOLUTION}`,
    `landed-at: ${LANDED_AT}`,
    `charter-version: ${(collected && collected.charterVersion) || 1}`,
    'pack-version: null',
    'outcome: landed',
    `rounds: ${ROUNDS}`,
    `suppression-count: ${(collected && collected.suppressionCount) || 0}`,
    input.causedBy ? `caused-by: ${input.causedBy}` : '# caused-by: omit unless this cycle fixed something an earlier one caused',
    '---',
    '```',
    '',
    'Every one of those fields is here because it CANNOT SURVIVE THE COPY any other',
    'way. This document travels from this repository into ai-sdlc\'s corpus, and git',
    'metadata does not travel — `landed-at` in particular, because ai-sdlc\'s git date',
    'is when the retro was copied in, not when the cycle landed.',
    '',
    'Then four body sections.',
    '',
    '## 1 · Findings',
    '',
    'This exact table shape, because aggregation parses it:',
    '',
    '| ID | Category | State | Stage | Detection | Disposition |',
    '|---|---|---|---|---|---|',
    '',
    'One row per finding, verbatim from the data below. Do not reclassify anything, do',
    'not merge rows, do not drop the ones that were dismissed. `rejected` and',
    '`accepted-wont-fix` feed OPPOSITE amendment signals — one says the finding was',
    'wrong, the other says the rule may be too broad — so collapsing them would corrupt',
    'the only evidence the charter improves from.',
    '',
    '```json',
    JSON.stringify(FINDINGS, null, 2),
    '```',
    '',
    '## 2 · Numbers',
    '',
    `Rounds: ${ROUNDS}. Findings: ${FINDINGS.length} (${blocking} blocking, ${suggested} suggested).`,
    detectionRatio === null
      ? 'Detection ratio: not applicable, no findings.'
      : `Detection: ${mechanical} mechanical, ${reasoned} by reasoning — ${detectionRatio}% mechanical.`,
    `Criteria: ${(collected && collected.criteriaCovered) || 0} of ${(collected && collected.criteriaVerifiable) || 0} verifiable covered.`,
    `Suppressions: ${(collected && collected.suppressionCount) || 0}.`,
    '',
    'State those figures and do not interpret them. v01 measured roughly 70% of',
    'findings as mechanical; whether that ratio is rising is a question for the corpus',
    'across many cycles, not for this document about one.',
    '',
    '## 3 · Where the process leaked',
    '',
    'For each finding attributed to a stage EARLIER than the one that caught it, say so',
    'plainly. A defect found at plan but attributed to spec means the spec stage let it',
    'through, and that attribution is the most useful thing in this document.',
    '',
    'Write nothing here if nothing was attributed upstream. An empty section is a real',
    'result.',
    '',
    '## 4 · Observations',
    '',
    'What was awkward, surprising, or slower than it should have been. Be specific and',
    'be brief — a retro that reads as self-congratulation is worthless, and so is one',
    'that invents problems to look rigorous.',
    '',
    'Do NOT grade the cycle or judge whether the process "worked". Self-assessment at',
    'close is structurally optimistic: everything visibly worked, because it passed its',
    'tests ten minutes ago. The trustworthy signal is delayed — a later cycle naming',
    'this one in its `caused-by` — and this document\'s job is to record honestly so',
    'that signal has something to point at.',
  ].join('\n'),
  { label: 'write retro', phase: 'Write', schema: WRITE_SCHEMA },
)

log(`retro written: ${FINDINGS.length} finding(s), ${ROUNDS} round(s)` +
    (detectionRatio === null ? '' : `, ${detectionRatio}% mechanical`))

return {
  ok: Boolean(written && written.written),
  stage: 'retro',
  'cycle-id': CYCLE,
  path: (written && written.path) || RETRO_PATH,
  solution: SOLUTION,
  'landed-at': LANDED_AT,
  rounds: ROUNDS,
  findings: FINDINGS.length,
  blocking: blocking,
  suggested: suggested,
  'detection-ratio-mechanical': detectionRatio,
  // The retro's presence is what makes this cycle read as done. Nothing else needs to
  // record that.
  next: `absorb-retro ${RETRO_PATH}`,
}
