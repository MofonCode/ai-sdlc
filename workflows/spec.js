export const meta = {
  name: 'spec',
  description: 'Turn an approved intent into a spec with individually identified acceptance criteria',
  whenToUse: 'After an intent is approved and merged. Pass { intent: "<cycle-id>" }.',
  phases: [
    { title: 'Read', detail: 'the intent, the charter, and the facts' },
    { title: 'Write', detail: 'produce the spec with its acceptance criteria' },
  ],
}

// ---------------------------------------------------------------------------
// A spec's acceptance criteria are its whole point, and two decisions about them
// are load-bearing:
//
//   * Every criterion carries an id of the form AC-<cycle-id>-<n>. Globally unique
//     by construction, assigned once and never renumbered — because those ids end
//     up in test annotations, and renumbering would break every one of them.
//
//   * Every criterion is marked verifiable or not. A verifiable criterion MUST have
//     a test; an unverifiable one is dispositioned by a person at the deploy gate.
//     Without the distinction you either annotate the unannotatable ("the UI should
//     feel responsive") or quietly let untested criteria pass as tested.
//
// The criteria live in a markdown table in the body rather than in frontmatter.
// They are what a person reads the document for, and a table is parseable without
// being unreadable.
// ---------------------------------------------------------------------------

const input = typeof args === 'string' ? { intent: args } : (args || {})
const CYCLE = input.intent || input.cycleId
const CHARTER = input.charter || '.sdlc/CHARTER.md'

if (!CYCLE) {
  throw new Error('spec needs a cycle id: pass it as args, e.g. "20260924-a1b2"')
}

const INTENT_PATH = input.intentPath || `.sdlc/intents/${CYCLE}.md`
const SPEC_PATH = input.specPath || `.sdlc/cycles/${CYCLE}/spec.md`

const READ_SCHEMA = {
  type: 'object',
  required: ['ok'],
  properties: {
    ok: { type: 'boolean' },
    intent: { type: 'string', description: 'The intent, verbatim.' },
    facts: { type: 'string', description: 'The fact-gatherer output, verbatim.' },
    charterVersion: { type: 'integer' },
    note: { type: 'string' },
  },
}

const WRITE_SCHEMA = {
  type: 'object',
  required: ['written', 'criteria'],
  properties: {
    written: { type: 'boolean' },
    path: { type: 'string' },
    criteria: {
      type: 'array',
      items: {
        type: 'object',
        required: ['id', 'verifiable', 'text'],
        properties: {
          id: { type: 'string', pattern: '^AC-[0-9]{8}-[0-9a-f]{4}-[0-9]+$' },
          verifiable: { type: 'boolean' },
          text: { type: 'string' },
        },
      },
    },
    unverifiableCount: { type: 'integer' },
    note: { type: 'string' },
  },
}

// ---------------------------------------------------------------------------

phase('Read')

const context = await agent(
  [
    'Collect everything needed to write a spec. Read, do not write.',
    '',
    `Intent:  ${INTENT_PATH}`,
    `Charter: ${CHARTER}`,
    '',
    'Also run the fact-gatherer against the intent and return its output verbatim:',
    `  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Get-SdlcFacts.ps1 -Artifact "${INTENT_PATH}" -Charter "${CHARTER}"`,
    '',
    'Return the intent and the facts unmodified. Do not summarise or interpret either;',
    'the next stage needs the originals.',
    '',
    'Set ok=false and explain in `note` if the intent does not exist. A missing intent',
    'is not something to work around — a spec with no intent above it has nothing to',
    'be a spec of.',
  ].join('\n'),
  { label: 'read intent', phase: 'Read', schema: READ_SCHEMA },
)

if (!context || !context.ok) {
  return {
    ok: false,
    stage: 'spec',
    'cycle-id': CYCLE,
    reason: (context && context.note) || `could not read ${INTENT_PATH}`,
  }
}

phase('Write')

const written = await agent(
  [
    `Write the spec for cycle ${CYCLE} to ${SPEC_PATH}.`,
    '',
    'The intent:',
    '```markdown',
    context.intent || '(unavailable)',
    '```',
    '',
    'Verified facts — already checked, do not re-derive:',
    '```json',
    context.facts || '{}',
    '```',
    '',
    `The charter is at ${CHARTER}. Read it. The invariants whose Applies-to includes`,
    '`spec` govern what you write, and two of them shape this document directly:',
    '',
    '  * an intent or spec that describes behaviour without its failure modes is',
    '    forbidden — so every behaviour you specify needs its error paths',
    '  * a statement that admits two readings leading to materially different',
    '    downstream work is forbidden — a plan will be written from this, and an',
    '    ambiguity resolved silently by an agent is a decision nobody made',
    '',
    'Required frontmatter:',
    '```yaml',
    '---',
    `cycle-id: ${CYCLE}`,
    `intent: ${CYCLE}`,
    `charter-version: ${context.charterVersion || 1}`,
    'pack-version: null',
    '---',
    '```',
    '',
    'Required body section, exactly this table shape, because a check parses it:',
    '',
    '## Acceptance criteria',
    '',
    '| ID | Verifiable | Criterion |',
    '|---|---|---|',
    `| AC-${CYCLE}-1 | yes | ... |`,
    `| AC-${CYCLE}-2 | no | ... |`,
    '',
    'Rules for the criteria, all three load-bearing:',
    '',
    `1. Ids are AC-${CYCLE}-<n>, numbered from 1, never renumbered afterwards. They`,
    '   end up in test annotations, so renumbering breaks every one of them.',
    '2. Mark a criterion `yes` only if a test could actually prove it. "The UI should',
    '   feel responsive" is `no`, and that is a correct answer rather than a failure —',
    '   an unverifiable criterion is dispositioned by a person at the deploy gate.',
    '3. Every `yes` criterion will be required to have a test. Do not mark something',
    '   verifiable to look rigorous; you are committing the build stage to writing that',
    '   test, and verification will block if it does not exist.',
    '',
    'Write the rest of the spec as the intent requires: what the behaviour is, its',
    'failure modes, and what is explicitly out of scope. Stay inside the intent — a',
    'spec that specifies more than its intent asked for is scope drift, and the',
    'verify stage compares the two.',
  ].join('\n'),
  { label: 'write spec', phase: 'Write', schema: WRITE_SCHEMA },
)

const criteria = (written && written.criteria) || []
const verifiable = criteria.filter(c => c.verifiable).length

log(`${criteria.length} acceptance criteria — ${verifiable} verifiable, ${criteria.length - verifiable} not`)

if (criteria.length === 0) {
  log('no acceptance criteria: a spec with nothing to verify cannot be verified')
}

return {
  ok: Boolean(written && written.written),
  stage: 'spec',
  'cycle-id': CYCLE,
  path: (written && written.path) || SPEC_PATH,
  criteria: criteria,
  verifiable: verifiable,
  unverifiable: criteria.length - verifiable,
  // The next gate is a human one: interrogate this, then approve it.
  next: `interrogate ${SPEC_PATH}`,
}
