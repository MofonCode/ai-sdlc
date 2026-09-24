export const meta = {
  name: 'verify',
  description: 'Assemble verify.md from generated evidence: traceability, adherence, gates, dispositions',
  whenToUse: 'After build. Pass { cycle: "<cycle-id>", findings: [...] }.',
  phases: [
    { title: 'Gather', detail: 'traceability, adherence and gate results, all generated' },
    { title: 'Assemble', detail: 'write verify.md and decide the outcome' },
  ],
}

// ---------------------------------------------------------------------------
// Everything in a verify document is DERIVED. Traceability comes from test
// annotations, adherence from the diff against the plan's declaration, gate results
// from the build and the suppression scan. Nothing here is written by hand, because
// a hand-maintained traceability table beside the real tests drifts the moment
// anything is refactored and nothing detects it.
//
// The rule this document enforces, taken verbatim from VSDD: every finding, from
// adversary or tool, at any severity, is dispositioned — and a gap between a derived
// artifact and its source is corrected, never signed off.
// ---------------------------------------------------------------------------

const input = typeof args === 'string' ? { cycle: args } : (args || {})
const CYCLE = input.cycle || input.cycleId
const CHARTER = input.charter || '.sdlc/CHARTER.md'
const FINDINGS = input.findings || []

if (!CYCLE) {
  throw new Error('verify needs a cycle id: pass it as args, e.g. "20260924-a1b2"')
}

const SPEC_PATH = input.specPath || `.sdlc/cycles/${CYCLE}/spec.md`
const PLAN_PATH = input.planPath || `.sdlc/cycles/${CYCLE}/plan.md`
const VERIFY_PATH = input.verifyPath || `.sdlc/cycles/${CYCLE}/verify.md`

const GATHER_SCHEMA = {
  type: 'object',
  required: ['ok'],
  properties: {
    ok: { type: 'boolean' },
    specFacts: { type: 'string', description: 'Gatherer output for the spec, verbatim — carries traceability and suppressions.' },
    planFacts: { type: 'string', description: 'Gatherer output for the plan, verbatim — carries adherence and both tiers.' },
    unmapped: { type: 'array', items: { type: 'string' } },
    orphanAnnotations: { type: 'array', items: { type: 'string' } },
    criteriaTotal: { type: 'integer' },
    criteriaVerifiable: { type: 'integer' },
    criteriaCovered: { type: 'integer' },
    suppressionCount: { type: 'integer' },
    unjustifiedSuppressions: { type: 'array', items: { type: 'string' } },
    undeclared: { type: 'array', items: { type: 'string' } },
    predictedTier: { type: 'integer' },
    actualTier: { type: 'integer' },
    tierEscalated: { type: 'boolean' },
    buildPasses: { type: 'boolean' },
    testsPass: { type: 'boolean' },
    charterVersion: { type: 'integer' },
    note: { type: 'string' },
  },
}

const ASSEMBLE_SCHEMA = {
  type: 'object',
  required: ['written', 'outcome'],
  properties: {
    written: { type: 'boolean' },
    path: { type: 'string' },
    outcome: { enum: ['passed', 'blocked', 'halted-tier-escalated'] },
    blockers: { type: 'array', items: { type: 'string' } },
    note: { type: 'string' },
  },
}

// ---------------------------------------------------------------------------

phase('Gather')

const evidence = await agent(
  [
    'Gather the generated evidence a verify document is assembled from. Read and run;',
    'do not write anything yet, and do not compute any of these numbers yourself.',
    '',
    'Run the fact-gatherer twice and return both outputs verbatim:',
    `  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Get-SdlcFacts.ps1 -Artifact "${SPEC_PATH}" -Charter "${CHARTER}"`,
    `  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Get-SdlcFacts.ps1 -Artifact "${PLAN_PATH}" -Charter "${CHARTER}"`,
    '',
    'From the spec output read `traceability`: which verifiable criteria have no',
    'annotated test (`unmapped`), which annotations name criteria the spec does not',
    'declare (`orphan-annotations`), the suppression count, and any suppression with no',
    'written justification.',
    '',
    'From the plan output read `plan-adherence`, `blast-radius` and',
    '`actual-blast-radius`: files changed but never declared, and both tiers.',
    '',
    'Then run and report the results of:',
    '  dotnet build',
    '  dotnet test',
    '',
    'Every number in a verify document is derived. If you find yourself deciding one,',
    'you are reading the wrong field.',
  ].join('\n'),
  { label: 'gather evidence', phase: 'Gather', schema: GATHER_SCHEMA },
)

if (!evidence || !evidence.ok) {
  return {
    ok: false,
    stage: 'verify',
    'cycle-id': CYCLE,
    reason: (evidence && evidence.note) || 'could not gather evidence',
  }
}

// The outcome is decided in code from the evidence, not by the agent that assembles
// the document. An agent asked both to report and to rule on its own report will
// round in the direction of done.
const blockers = []

const unmapped = evidence.unmapped || []
const orphans = evidence.orphanAnnotations || []
const unjustified = evidence.unjustifiedSuppressions || []
const undeclared = evidence.undeclared || []
const escalated = Boolean(evidence.tierEscalated)

if (unmapped.length > 0) blockers.push(`${unmapped.length} verifiable criterion(s) with no annotated test`)
if (orphans.length > 0) blockers.push(`${orphans.length} annotation(s) naming criteria the spec does not declare`)
if (unjustified.length > 0) blockers.push(`${unjustified.length} suppression(s) with no written justification`)
if (undeclared.length > 0) blockers.push(`${undeclared.length} file(s) changed but not declared`)
if (evidence.buildPasses === false) blockers.push('the build does not pass')
if (evidence.testsPass === false) blockers.push('the tests do not pass')

const undispositioned = FINDINGS.filter(
  f => f && f.state === 'blocking' && !f.disposition,
).length

if (undispositioned > 0) {
  blockers.push(`${undispositioned} blocking finding(s) with no disposition`)
}

const outcome = escalated
  ? 'halted-tier-escalated'
  : (blockers.length === 0 ? 'passed' : 'blocked')

phase('Assemble')

const assembled = await agent(
  [
    `Write ${VERIFY_PATH}. Every value below is already decided — transcribe it.`,
    '',
    'Required frontmatter:',
    '```yaml',
    '---',
    `cycle-id: ${CYCLE}`,
    `plan: ${CYCLE}`,
    `charter-version: ${evidence.charterVersion || 1}`,
    'pack-version: null',
    `outcome: ${outcome}`,
    `predicted-tier: ${evidence.predictedTier || 1}`,
    `actual-tier: ${evidence.actualTier || evidence.predictedTier || 1}`,
    `suppression-count: ${evidence.suppressionCount || 0}`,
    `criteria-total: ${evidence.criteriaTotal || 0}`,
    `criteria-verifiable: ${evidence.criteriaVerifiable || 0}`,
    `criteria-covered: ${evidence.criteriaCovered || 0}`,
    `undispositioned: ${undispositioned}`,
    '---',
    '```',
    '',
    'Four body sections, in this order:',
    '',
    '## 1 · Traceability',
    '   A table of every acceptance criterion: its id, whether it is verifiable, and',
    '   the test files proving it. GENERATED from the annotations in the spec facts —',
    '   do not add a criterion the spec does not declare, and do not claim coverage the',
    '   annotations do not show. An unverifiable criterion has no test and is not',
    '   expected to; mark it for human disposition at the deploy gate.',
    '',
    '## 2 · Plan adherence',
    '   What the plan declared against what the diff actually touched, and both tiers.',
    '',
    '## 3 · Tool gates',
    '   Build, tests, and the suppression count. State the count plainly: it is tracked',
    '   across cycles and an increase is itself a finding.',
    '',
    '## 4 · Dispositions',
    '   Every finding, with its disposition and — for rejected or accepted-wont-fix —',
    '   the reason. Nothing is left without a recorded outcome.',
    '',
    'Findings to disposition:',
    '```json',
    JSON.stringify(FINDINGS, null, 2),
    '```',
    '',
    `The outcome is ${outcome} and was decided from the evidence. Do not change it, and`,
    'do not soften the language around it: a verify document that reads as passing when',
    'it is blocked is worse than no document, because the next stage believes it.',
    blockers.length > 0 ? '\nBlockers:\n' + blockers.map(b => '  - ' + b).join('\n') : '',
  ].join('\n'),
  { label: 'write verify.md', phase: 'Assemble', schema: ASSEMBLE_SCHEMA },
)

log(`${outcome}${blockers.length ? ' — ' + blockers.length + ' blocker(s)' : ''}`)

if (escalated) {
  log(`tier escalated: predicted ${evidence.predictedTier}, actual ${evidence.actualTier}. A person decides.`)
}

return {
  ok: outcome === 'passed',
  stage: 'verify',
  'cycle-id': CYCLE,
  path: (assembled && assembled.path) || VERIFY_PATH,
  outcome: outcome,
  blockers: blockers,
  'suppression-count': evidence.suppressionCount || 0,
  'criteria-covered': evidence.criteriaCovered || 0,
  'criteria-verifiable': evidence.criteriaVerifiable || 0,
  undispositioned: undispositioned,
  next: outcome === 'passed' ? `retro ${CYCLE}` : 'repair, or escalate to a person',
}
