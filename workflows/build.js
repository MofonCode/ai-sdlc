export const meta = {
  name: 'build',
  description: 'Implement an approved plan, then halt if the work turned out more dangerous than approved',
  whenToUse: 'After a plan is approved. Pass { plan: "<cycle-id>" }.',
  phases: [
    { title: 'Read', detail: 'the plan, its declared files, and its predicted tier' },
    { title: 'Implement', detail: 'one agent per declared file' },
    { title: 'Adherence', detail: 'compare the real diff against what the plan declared' },
  ],
}

// ---------------------------------------------------------------------------
// Build is the first stage that touches code, which makes it the first stage where
// anything is irreversible. Two consequences:
//
//   * A tier-3 plan does not reach here unattended. That gate is the plan's, not
//     this one's, but this stage refuses to proceed if it was skipped.
//   * The work is checked against the plan AFTERWARDS, because a plan predicts and
//     an implementation discovers. A build that touched files the plan never
//     declared has drifted, and a build whose actual tier exceeds the predicted one
//     has become more dangerous than it was approved to be — which halts rather
//     than reports.
// ---------------------------------------------------------------------------

const input = typeof args === 'string' ? { plan: args } : (args || {})
const CYCLE = input.plan || input.cycleId
const CHARTER = input.charter || '.sdlc/CHARTER.md'
const UNATTENDED = input.unattended === true

if (!CYCLE) {
  throw new Error('build needs a cycle id: pass it as args, e.g. "20260924-a1b2"')
}

const PLAN_PATH = input.planPath || `.sdlc/cycles/${CYCLE}/plan.md`
const SPEC_PATH = input.specPath || `.sdlc/cycles/${CYCLE}/spec.md`

const READ_SCHEMA = {
  type: 'object',
  required: ['ok'],
  properties: {
    ok: { type: 'boolean' },
    plan: { type: 'string' },
    predictedTier: { type: 'integer' },
    files: {
      type: 'array',
      items: {
        type: 'object',
        required: ['path', 'change'],
        properties: {
          path: { type: 'string' },
          change: { enum: ['new', 'modify', 'delete'] },
        },
      },
    },
    verifiableCriteria: { type: 'array', items: { type: 'string' } },
    note: { type: 'string' },
  },
}

const IMPLEMENT_SCHEMA = {
  type: 'object',
  required: ['done', 'path'],
  properties: {
    done: { type: 'boolean' },
    path: { type: 'string' },
    summary: { type: 'string' },
    criteriaCovered: {
      type: 'array',
      description: 'Acceptance criterion ids this file\'s tests prove, if it is a test file.',
      items: { type: 'string' },
    },
    note: { type: 'string' },
  },
}

const ADHERENCE_SCHEMA = {
  type: 'object',
  required: ['ok'],
  properties: {
    ok: { type: 'boolean' },
    actualTier: { type: 'integer' },
    predictedTier: { type: 'integer' },
    tierEscalated: { type: 'boolean' },
    undeclared: { type: 'array', items: { type: 'string' } },
    declaredUntouched: { type: 'array', items: { type: 'string' } },
    buildPasses: { type: 'boolean' },
    testsPass: { type: 'boolean' },
    raw: { type: 'string' },
  },
}

// ---------------------------------------------------------------------------

phase('Read')

const context = await agent(
  [
    'Collect what is needed to implement a plan. Read, do not write any code yet.',
    '',
    `Plan:    ${PLAN_PATH}`,
    `Spec:    ${SPEC_PATH}`,
    `Charter: ${CHARTER}`,
    '',
    'Run the fact-gatherer against the plan and read `blast-radius` and',
    '`declared-files` from its output:',
    `  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Get-SdlcFacts.ps1 -Artifact "${PLAN_PATH}" -Charter "${CHARTER}"`,
    '',
    'Report the predicted tier, the declared files, and the ids of every verifiable',
    'acceptance criterion in the spec — each of those needs a test, and this stage is',
    'what owes them.',
    '',
    'Set ok=false if the plan does not exist or declares no files. A plan with no',
    'declaration cannot be built against, because there would be nothing to check the',
    'result for drift.',
  ].join('\n'),
  { label: 'read plan', phase: 'Read', schema: READ_SCHEMA },
)

if (!context || !context.ok) {
  return {
    ok: false,
    stage: 'build',
    'cycle-id': CYCLE,
    reason: (context && context.note) || `could not read ${PLAN_PATH}`,
  }
}

const predicted = context.predictedTier || null
const files = context.files || []

// Unattended runs are permitted at tier 1 only. The gate belongs to the plan stage,
// but refusing here as well costs nothing and closes the case where a dispatcher is
// wrong about what it is running.
if (UNATTENDED && predicted !== 1) {
  log(`tier ${predicted}: refusing to build unattended. A person signs off before code is written.`)
  return {
    ok: false,
    stage: 'build',
    'cycle-id': CYCLE,
    'predicted-tier': predicted,
    reason: 'unattended builds are permitted at tier 1 only',
    outcome: 'halted-tier',
  }
}

phase('Implement')

log(`implementing ${files.length} declared file(s) at tier ${predicted}`)

// One agent per declared file, as a pipeline: files are independent, and waiting for
// the slowest before starting the next buys nothing.
const built = await pipeline(
  files,
  (file) =>
    agent(
      [
        'Implement exactly one file from an approved plan.',
        '',
        `File:   ${file.path}`,
        `Change: ${file.change}`,
        '',
        'The plan:',
        '```markdown',
        context.plan || '(unavailable)',
        '```',
        '',
        `The spec is at ${SPEC_PATH} and the charter at ${CHARTER}. Read both.`,
        '',
        'Touch this file and no other. The plan declared a set of files, and verify',
        'compares the real diff against that declaration — a file you change here that',
        'the plan did not name becomes a scope-drift finding, and it is attributed to',
        'this stage.',
        '',
        context.verifiableCriteria && context.verifiableCriteria.length
          ? 'If this is a test file, annotate each test with the acceptance criterion it ' +
            'proves, using [TestProperty("AC", "<id>")]. Verifiable criteria in this spec: ' +
            context.verifiableCriteria.join(', ') + '. An unannotated test proves nothing ' +
            'traceable, and verification blocks on a criterion with no test.'
          : 'This spec has no verifiable criteria, so no annotations are required.',
        '',
        'The charter forbids a build warning, a failed analyzer, and an unjustified',
        'suppression. If you need a suppression, write the reason next to it — a',
        'suppression without one is a finding, and the count is tracked.',
      ].join('\n'),
      { label: file.path, phase: 'Implement', schema: IMPLEMENT_SCHEMA },
    ),
)

const done = built.filter(b => b && b.done).length
log(`${done}/${files.length} declared file(s) implemented`)

phase('Adherence')

const adherence = await agent(
  [
    'Check what the build actually did against what the plan declared.',
    '',
    'Run the fact-gatherer against the plan again — it now sees the working-tree diff',
    'and computes the ACTUAL tier from it, using the same function that produced the',
    'predicted one:',
    `  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Get-SdlcFacts.ps1 -Artifact "${PLAN_PATH}" -Charter "${CHARTER}"`,
    '',
    'Read `plan-adherence` and `actual-blast-radius`. Report:',
    '  * the actual tier and whether it escalated past the predicted one',
    '  * files changed but not declared',
    '  * files declared but never touched',
    '',
    'Then build and test, and report whether each passed:',
    '  dotnet build',
    '  dotnet test',
    '',
    'Return the gatherer output verbatim in `raw`. Set ok=false if the build or the',
    'tests fail — a build that does not compile has not implemented anything, whatever',
    'the file count says.',
  ].join('\n'),
  { label: 'adherence', phase: 'Adherence', schema: ADHERENCE_SCHEMA },
)

const escalated = Boolean(adherence && adherence.tierEscalated)
const undeclared = (adherence && adherence.undeclared) || []

if (escalated) {
  log(`tier escalated: predicted ${predicted}, actual ${adherence.actualTier} — halting for a person`)
}
if (undeclared.length > 0) {
  log(`${undeclared.length} undeclared file(s) changed — scope drift, attributed to build`)
}

// A tier escalation halts rather than reports. The work turned out more dangerous
// than it was approved to be, and the approval does not stretch to cover it.
const outcome = escalated
  ? 'halted-tier-escalated'
  : (adherence && adherence.ok ? 'built' : 'failed')

return {
  ok: outcome === 'built',
  stage: 'build',
  'cycle-id': CYCLE,
  outcome: outcome,
  'predicted-tier': predicted,
  'actual-tier': (adherence && adherence.actualTier) || null,
  'tier-escalated': escalated,
  undeclared: undeclared,
  'declared-untouched': (adherence && adherence.declaredUntouched) || [],
  'build-passes': Boolean(adherence && adherence.buildPasses),
  'tests-pass': Boolean(adherence && adherence.testsPass),
  files: built.filter(Boolean),
  next: `verify ${CYCLE}`,
}
