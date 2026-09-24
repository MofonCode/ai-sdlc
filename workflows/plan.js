export const meta = {
  name: 'plan',
  description: 'Turn an approved spec into a plan that enumerates the files it will touch',
  whenToUse: 'After a spec is approved. Pass { spec: "<cycle-id>" }.',
  phases: [
    { title: 'Read', detail: 'the spec, the charter, and the facts' },
    { title: 'Write', detail: 'produce the plan and its file declaration' },
    { title: 'Tier', detail: 'compute the blast radius from the declared files' },
  ],
}

// ---------------------------------------------------------------------------
// A plan's file declaration does two jobs with one table, which is why INV-05
// requires it:
//
//   * it makes scope drift mechanically detectable — verify compares the
//     declaration against the actual diff, and an unplanned file is a finding
//     rather than a footnote
//   * it is what the blast-radius tier reads
//
// The tier gates the BUILD, not the launch. Dispatch happens before any spec or
// plan exists, so there is nothing to read at launch time — and nothing
// irreversible happens before the plan either, since spec and plan are documents
// and only build touches code.
// ---------------------------------------------------------------------------

const input = typeof args === 'string' ? { spec: args } : (args || {})
const CYCLE = input.spec || input.cycleId
const CHARTER = input.charter || '.sdlc/CHARTER.md'

if (!CYCLE) {
  throw new Error('plan needs a cycle id: pass it as args, e.g. "20260924-a1b2"')
}

const SPEC_PATH = input.specPath || `.sdlc/cycles/${CYCLE}/spec.md`
const PLAN_PATH = input.planPath || `.sdlc/cycles/${CYCLE}/plan.md`

const READ_SCHEMA = {
  type: 'object',
  required: ['ok'],
  properties: {
    ok: { type: 'boolean' },
    spec: { type: 'string' },
    facts: { type: 'string' },
    criteriaCount: { type: 'integer' },
    verifiableCount: { type: 'integer' },
    charterVersion: { type: 'integer' },
    note: { type: 'string' },
  },
}

const WRITE_SCHEMA = {
  type: 'object',
  required: ['written', 'files'],
  properties: {
    written: { type: 'boolean' },
    path: { type: 'string' },
    files: {
      type: 'array',
      items: {
        type: 'object',
        required: ['path', 'change'],
        properties: {
          path: { type: 'string' },
          change: { enum: ['new', 'modify', 'delete'] },
          why: { type: 'string' },
        },
      },
    },
    testsPlanned: {
      type: 'array',
      description: 'Which acceptance criterion each planned test proves.',
      items: {
        type: 'object',
        required: ['criterion', 'testFile'],
        properties: {
          criterion: { type: 'string' },
          testFile: { type: 'string' },
        },
      },
    },
    note: { type: 'string' },
  },
}

const TIER_SCHEMA = {
  type: 'object',
  required: ['tier'],
  properties: {
    tier: { type: 'integer', minimum: 1, maximum: 3 },
    flags: { type: 'array', items: { type: 'string' } },
    fileCount: { type: 'integer' },
    raw: { type: 'string', description: 'The gatherer output, verbatim.' },
  },
}

// ---------------------------------------------------------------------------

phase('Read')

const context = await agent(
  [
    'Collect everything needed to write a plan. Read, do not write.',
    '',
    `Spec:    ${SPEC_PATH}`,
    `Charter: ${CHARTER}`,
    '',
    'Run the fact-gatherer against the spec and return its output verbatim:',
    `  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Get-SdlcFacts.ps1 -Artifact "${SPEC_PATH}" -Charter "${CHARTER}"`,
    '',
    'The facts include the parsed acceptance criteria with their verifiable flags.',
    'Report how many there are and how many are verifiable — the plan has to account',
    'for a test per verifiable criterion, so that count is a requirement rather than',
    'context.',
    '',
    'Set ok=false if the spec does not exist. A plan with no spec above it is',
    'implementing nothing.',
  ].join('\n'),
  { label: 'read spec', phase: 'Read', schema: READ_SCHEMA },
)

if (!context || !context.ok) {
  return {
    ok: false,
    stage: 'plan',
    'cycle-id': CYCLE,
    reason: (context && context.note) || `could not read ${SPEC_PATH}`,
  }
}

phase('Write')

const written = await agent(
  [
    `Write the plan for cycle ${CYCLE} to ${PLAN_PATH}.`,
    '',
    'The spec:',
    '```markdown',
    context.spec || '(unavailable)',
    '```',
    '',
    'Verified facts — already checked, do not re-derive:',
    '```json',
    context.facts || '{}',
    '```',
    '',
    `The charter is at ${CHARTER}. Read it. INV-05 governs this document directly: a`,
    'plan that does not enumerate the files it will touch is forbidden, and the',
    'declaration is not paperwork — verify compares it against the actual diff, so an',
    'undeclared file becomes a scope-drift finding.',
    '',
    'Required frontmatter:',
    '```yaml',
    '---',
    `cycle-id: ${CYCLE}`,
    `spec: ${CYCLE}`,
    `charter-version: ${context.charterVersion || 1}`,
    'pack-version: null',
    '---',
    '```',
    '',
    'Required body section, exactly this table shape, because a check parses it:',
    '',
    '## Files',
    '',
    '| Path | Change |',
    '|---|---|',
    '| src/Example.cs | new |',
    '| tests/Example.Tests.cs | new |',
    '',
    '`change` is one of new, modify, delete — and it is checked against the tree, so',
    'declaring `new` for a file that already exists, or `modify` for one that does not,',
    'fails. That mismatch means the plan was written against a different tree than the',
    'one it will run in.',
    '',
    `The spec has ${context.verifiableCount || 0} verifiable acceptance criteria.`,
    'Each one needs a test, and the test files must appear in the table above. Report',
    'which criterion each planned test proves — verification will require that mapping,',
    'and discovering a missing test then is more expensive than declaring it now.',
    '',
    'Declare only what the spec requires. A plan that touches more than its spec asked',
    'for is scope drift one stage earlier than usual, and it is harder to see there.',
  ].join('\n'),
  { label: 'write plan', phase: 'Write', schema: WRITE_SCHEMA },
)

const files = (written && written.files) || []

phase('Tier')

// The tier is computed by the gatherer rather than judged by an agent: it is a
// function of the declared paths, and a function should not be an opinion.
const tier = await agent(
  [
    'Compute the blast-radius tier for the plan just written, and record it.',
    '',
    'Run:',
    `  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Get-SdlcFacts.ps1 -Artifact "${PLAN_PATH}" -Charter "${CHARTER}"`,
    '',
    'Read `blast-radius` from the output. Report its tier, its flags, and the file',
    'count, and return the whole gatherer output verbatim in `raw`.',
    '',
    `Then add the resulting tier to ${PLAN_PATH}'s frontmatter as`,
    '`predicted-tier: <n>`, leaving everything else untouched.',
    '',
    'Do not compute or adjust the tier yourself. It is a function of the declared',
    'paths, and the point of computing it mechanically is that it cannot be argued with.',
  ].join('\n'),
  { label: 'blast radius', phase: 'Tier', schema: TIER_SCHEMA },
)

const predicted = (tier && tier.tier) || null
const flags = (tier && tier.flags) || []

log(`${files.length} file(s) declared; tier ${predicted || '?'}${flags.length ? ' — flags: ' + flags.join(', ') : ''}`)

if (predicted === 3) {
  log('tier 3: this cannot run unattended through build. A person signs off before code is written.')
}

return {
  ok: Boolean(written && written.written),
  stage: 'plan',
  'cycle-id': CYCLE,
  path: (written && written.path) || PLAN_PATH,
  files: files,
  'predicted-tier': predicted,
  flags: flags,
  'tests-planned': (written && written.testsPlanned) || [],
  next: `interrogate ${PLAN_PATH}`,
}
