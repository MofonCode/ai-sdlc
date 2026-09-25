export const meta = {
  name: 'interrogate',
  description: 'Interrogate an artifact against the charter: four orthogonal roles, triage, merge',
  whenToUse: 'After writing or repairing an artifact, before its gate. Pass the artifact path as args.',
  phases: [
    { title: 'Facts', detail: 'run the fact-gatherer once; its results feed every role' },
    { title: 'Discover', detail: 'four orthogonal roles, one agent each' },
    { title: 'Triage', detail: 'per candidate: find a citation, or cap it at suggested' },
    { title: 'Merge', detail: 'consolidate duplicates and emit one schema-valid document' },
  ],
}

// ---------------------------------------------------------------------------
// The artifact under interrogation. `args` is either a path string or
// { artifact, charter, round }.
// ---------------------------------------------------------------------------

const input = typeof args === 'string' ? { artifact: args } : (args || {})
const ARTIFACT = input.artifact
const CHARTER = input.charter || '.sdlc/CHARTER.md'
const ROUND = input.round || 1

if (!ARTIFACT) {
  throw new Error('interrogate needs an artifact path: pass it as args, e.g. ".sdlc/intents/20260924-aaaa.md"')
}

// ---------------------------------------------------------------------------
// Schemas. The findings schema in schemas/findings.schema.json is the authority
// for the final document; these are the narrower per-stage contracts.
//
// Note what the roles are NOT asked for: `state`. A role discovers; it does not
// decide whether a cycle halts. State is assigned later, from the citation —
// which is the anti-inflation guard, and it only works if the role cannot set it.
// ---------------------------------------------------------------------------

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

const FACTS_SCHEMA = {
  type: 'object',
  required: ['ok', 'facts'],
  properties: {
    ok: { type: 'boolean' },
    facts: { type: 'string', description: 'The raw JSON the gatherer printed, verbatim.' },
    failedChecks: { type: 'array', items: { type: 'string' } },
    note: { type: 'string' },
  },
}

const CANDIDATES_SCHEMA = {
  type: 'object',
  required: ['candidates'],
  properties: {
    candidates: {
      type: 'array',
      items: {
        type: 'object',
        required: ['category', 'stage', 'summary', 'anchor'],
        properties: {
          category: { enum: CATEGORIES },
          stage: { enum: STAGES, description: 'Which artifact should have prevented this; may be upstream.' },
          summary: { type: 'string', description: 'One sentence stating the defect.' },
          anchor: { type: 'string', description: 'A heading or uniquely-quoted string locating it. Never a line number.' },
          secondAnchor: { type: 'string', description: 'For a contradiction: the other half.' },
          repair: { type: 'string', description: 'A concrete change that would resolve it, if one is obvious.' },
        },
      },
    },
  },
}

const TRIAGE_SCHEMA = {
  type: 'object',
  required: ['cited'],
  properties: {
    cited: { type: 'boolean' },
    citeKind: { enum: ['invariant', 'check', 'deferred', 'locations', 'none'] },
    citeId: { type: 'string', description: 'INV-nn, LOC-nn, DEC-nn, or a failed-check id.' },
    reason: { type: 'string', description: 'Why this does or does not earn a citation.' },
    repair: { type: 'string' },
  },
}

const MERGE_SCHEMA = {
  type: 'object',
  required: ['findings'],
  properties: {
    findings: {
      type: 'array',
      items: {
        type: 'object',
        required: ['id', 'category', 'state', 'stage', 'detection', 'summary'],
        properties: {
          id: { type: 'string' },
          category: { enum: CATEGORIES },
          state: { enum: ['open', 'blocking', 'suggested'] },
          stage: { enum: STAGES },
          detection: { enum: ['check', 'reasoning'] },
          role: { enum: ['charter', 'coherence', 'facts', 'gaps', 'form'] },
          summary: { type: 'string' },
          cites: { type: 'array', items: { type: 'object' } },
          repair: { type: 'string' },
        },
      },
    },
    mergedAway: { type: 'integer', description: 'How many duplicates were consolidated.' },
  },
}

// ---------------------------------------------------------------------------
// Roles. Orthogonal by QUESTION, never by area: area lenses overlap constantly
// and dump deduplication on the merge, while these read different evidence and
// so rarely collide.
//
// TODO(disposable-first-attempt): these belong in the charter as data, so the
// retro can eventually say "the gaps role produced two findings, both rejected"
// and drop a lens on evidence. Hardcoded here because reading them out of the
// charter needs a parser the gatherer does not yet have.
// ---------------------------------------------------------------------------

const ROLES = [
  {
    name: 'charter',
    question: 'Does this artifact contradict a charter invariant?',
    evidence: 'the charter, and only the invariants whose Applies-to includes this artifact kind',
    failsBy: 'over-matching — treating everything as a violation',
  },
  {
    name: 'coherence',
    question: 'Does this artifact contradict itself?',
    evidence: 'the artifact alone. Do not consult the charter or the tree',
    failsBy: 'missing contradictions between distant sections',
  },
  {
    name: 'facts',
    question: 'Do this artifact\'s claims about the repository hold?',
    evidence: 'the attached facts, and the tree if you must look further',
    failsBy: 'not checking enough',
  },
  {
    name: 'gaps',
    question: 'What is missing that should be here?',
    evidence: 'domain reasoning about what this kind of artifact must cover',
    failsBy: 'inventing requirements nobody asked for',
  },
]

// When the artifact IS the charter, the charter role has nothing above it to check
// against and is replaced by `form`, which judges each invariant's SHAPE rather than
// its content. That substitution is what makes the charter interrogable without an
// infinite regress.
const FORM_ROLE = {
  name: 'form',
  question: 'Does every invariant carry a non-empty Forbids: and Applies-to:, and does it forbid something an implementation could actually do?',
  evidence: 'the charter itself. An invariant compatible with every implementation is not a rule',
  failsBy: 'judging content instead of shape',
}

const isCharter = ARTIFACT.replace(/\\/g, '/').endsWith('CHARTER.md')
const activeRoles = isCharter
  ? [FORM_ROLE].concat(ROLES.filter(r => r.name !== 'charter'))
  : ROLES

// ---------------------------------------------------------------------------

phase('Facts')

// Roughly 70% of v01's findings were mechanical. Gathering them ONCE and handing
// the same result to every role is what cut a round from 30 tool calls to 13 — if
// each role rediscovers the tree, four clean-slate agents cost more than one
// session rather than less.
const factsResult = await agent(
  [
    'Run the SDLC fact-gatherer and return its output verbatim.',
    '',
    'Command (from the repository root):',
    '  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/Get-SdlcFacts.ps1 ' +
      `-Artifact "${ARTIFACT}" -Charter "${CHARTER}"`,
    '',
    'Return the JSON it printed, unmodified, in the `facts` field. Do not summarise,',
    'reformat, or correct it. Set ok=false and explain in `note` if the command failed.',
  ].join('\n'),
  { label: 'gather facts', phase: 'Facts', schema: FACTS_SCHEMA },
)

if (!factsResult || !factsResult.ok) {
  log('fact-gathering failed — roles will run without attached facts, so mechanical findings will be missed')
}

const factsJson = factsResult && factsResult.facts ? factsResult.facts : '{}'

// A failed check IS a citation, so the findings it implies need no agent at all:
// deterministic, free, and born `blocking` rather than sitting at `open`.
let parsedFacts = {}
try {
  parsedFacts = JSON.parse(factsJson)
} catch (e) {
  log('facts were not parseable JSON; continuing without mechanical findings')
}

const failedChecks = (parsedFacts && parsedFacts['failed-checks']) || []
const checkDetail = {}
for (const c of (parsedFacts && parsedFacts.checks) || []) {
  if (c && c.id) checkDetail[c.id] = c.detail
}

// EVERY check id the gatherer can emit must appear here. An unmapped id falls through to
// `unclassified`, which can never block by construction — so a failed check would be
// reported as advisory while the charter's Coverage table claims that category blocks
// through exactly this route. Five of fourteen were mapped until an interrogation of the
// charter found it; the nine missing included `suppressions-justified` and `plan-adherence`,
// which is to say gate bypasses and scope drift were silently non-blocking.
//
// WorkflowScriptTests binds this map to the gatherer's emitted ids, so adding a check
// without classifying it fails the build rather than quietly widening the hole.
const CHECK_CATEGORY = {
  // A reference that does not resolve: a path, an id, or an annotation naming nothing.
  'paths-resolve': 'unresolvable-reference',
  'invariant-ids-resolve': 'unresolvable-reference',
  'dec-ids-resolve': 'unresolvable-reference',
  'charter-present': 'unresolvable-reference',
  'annotations-resolve': 'unresolvable-reference',
  'plan-files-consistent': 'unresolvable-reference',

  // Something required by the artifact's kind is absent.
  'frontmatter-present': 'missing-case',
  'acceptance-criteria-present': 'missing-case',
  'plan-declares-files': 'missing-case',

  // Two representations of one fact, with nothing binding them.
  'acceptance-criteria-unique': 'duplicated-truth',

  // Marked verifiable, but nothing proves it.
  'traceability-complete': 'untestable',

  // A gate stepped around without a written justification.
  'suppressions-justified': 'gate-bypass',

  // The work did more than the upstream artifact authorised.
  'plan-adherence': 'scope-drift',
  'tier-not-escalated': 'scope-drift',
}

const mechanical = failedChecks.map((id, i) => ({
  id: 'm' + (i + 1),
  category: CHECK_CATEGORY[id] || 'unclassified',
  state: CHECK_CATEGORY[id] ? 'blocking' : 'suggested',
  stage: 'intent',
  detection: 'check',
  role: 'facts',
  summary: 'Check `' + id + '` failed: ' + (checkDetail[id] || 'see facts'),
  cites: [{ kind: 'check', id: id }],
}))

log(`facts attached; ${mechanical.length} mechanical finding(s) derived without an agent`)

// ---------------------------------------------------------------------------

phase('Discover')

// A barrier is correct here and only here: the deduplication below genuinely needs
// every role's output at once, which is the one justification for waiting.
const discovered = await parallel(
  activeRoles.map(role => () =>
    agent(
      [
        `You are the \`${role.name}\` interrogation role. You have exactly one question:`,
        '',
        `    ${role.question}`,
        '',
        `Your evidence is ${role.evidence}.`,
        `Your characteristic failure is ${role.failsBy} — guard against it.`,
        '',
        `Artifact under interrogation: ${ARTIFACT}`,
        `Charter: ${CHARTER}`,
        '',
        'Pre-gathered facts (already verified — do not re-derive them):',
        '```json',
        factsJson,
        '```',
        '',
        'Read the artifact. Report only defects your one question reveals; another role',
        'is asking each of the other questions, so staying in your lane is what keeps',
        'the merge from drowning in four wordings of one problem.',
        '',
        'Do NOT assign urgency or decide whether anything halts the cycle. Report what',
        'you found and where. Anchor each finding to a heading or a uniquely-quoted',
        'string, never a line number: a line number is invalidated by any edit above it.',
        '',
        'Return an empty candidates array if your question reveals nothing. That is a',
        'normal and useful result.',
      ].join('\n'),
      { label: role.name, phase: 'Discover', schema: CANDIDATES_SCHEMA },
    ),
  ),
)

const candidates = []
for (let i = 0; i < discovered.length; i++) {
  const result = discovered[i]
  if (!result || !result.candidates) continue
  for (const c of result.candidates) {
    candidates.push(Object.assign({}, c, { role: activeRoles[i].name }))
  }
}

// Cheap exact deduplication first, so the triage stage is not paid for twice on
// the same finding. Semantic consolidation is the merge agent's job — this only
// catches the identical.
const seen = new Set()
const unique = candidates.filter(c => {
  const key = c.category + '|' + (c.anchor || '').trim().toLowerCase()
  if (seen.has(key)) return false
  seen.add(key)
  return true
})

log(`${candidates.length} candidate(s) from ${activeRoles.length} roles; ${unique.length} after exact dedup`)

// ---------------------------------------------------------------------------

phase('Triage')

// The citation requirement belongs at PROMOTION, not at discovery: at discovery a
// role is pattern-matching, at triage it is investigating. Asking for a citation
// before it has looked is asking it to guess.
const triaged = await pipeline(
  unique,
  (candidate) =>
    agent(
      [
        'Triage one interrogation finding. Your job is to establish whether it earns a',
        'citation — nothing else.',
        '',
        `Artifact: ${ARTIFACT}`,
        `Charter: ${CHARTER}`,
        '',
        'Finding:',
        `  category: ${candidate.category}`,
        `  summary:  ${candidate.summary}`,
        `  anchor:   ${candidate.anchor}`,
        candidate.secondAnchor ? `  second anchor: ${candidate.secondAnchor}` : '',
        '',
        'Pre-gathered facts:',
        '```json',
        factsJson,
        '```',
        '',
        'A citation is exactly one of:',
        '  invariant — an INV-nn or LOC-nn DECLARED in the charter whose Applies-to covers',
        '              this artifact kind, and which this finding demonstrably violates',
        '  check     — a failed check id from the facts above',
        '  deferred  — a DEC-nn in the register, when the subject is deliberately open',
        '  locations — two positions in the artifact that demonstrably conflict. Permitted',
        '              ONLY for internal-contradiction',
        '',
        'Be strict. An uncited finding is not discarded — it becomes a suggestion and',
        'accumulates as evidence. Inventing a citation to make something look important',
        'is the one failure that matters here, because it is what makes the repair loop',
        'stop converging.',
        '',
        'If no citation genuinely applies, set cited=false and say why.',
      ].join('\n'),
      { label: candidate.category, phase: 'Triage', schema: TRIAGE_SCHEMA },
    ).then(verdict => ({ candidate, verdict })),
)

// State is assigned HERE, in plain code, from the citation — never by an agent.
// That is what makes the anti-inflation guard a guard: an agent can argue for
// importance, but it cannot grant it.
const assembled = mechanical.slice()
let n = 0

for (const row of triaged) {
  if (!row || !row.candidate) continue
  n++

  const c = row.candidate
  const v = row.verdict || {}
  const cites = []

  if (v.cited && v.citeKind && v.citeKind !== 'none') {
    if (v.citeKind === 'locations') {
      if (c.anchor && c.secondAnchor) {
        cites.push({
          kind: 'locations',
          locations: [
            { file: ARTIFACT, anchor: c.anchor },
            { file: ARTIFACT, anchor: c.secondAnchor },
          ],
        })
      }
    } else if (v.citeId) {
      cites.push({ kind: v.citeKind, id: v.citeId })
    }
  }

  // RULE 4: a location pair is self-evident only for a contradiction.
  const citesOk = cites.length > 0 &&
    !(cites[0].kind === 'locations' && c.category !== 'internal-contradiction')

  // RULE 2: unclassified can never block, however well cited.
  const state = (c.category === 'unclassified' || !citesOk) ? 'suggested' : 'blocking'

  assembled.push({
    id: 'r' + n,
    category: c.category,
    state: state,
    stage: c.stage,
    detection: 'reasoning',
    role: c.role,
    summary: c.summary,
    cites: citesOk ? cites : undefined,
    repair: v.repair || c.repair || undefined,
  })
}

// ---------------------------------------------------------------------------

phase('Merge')

const merged = await agent(
  [
    'Consolidate an interrogation into one findings document.',
    '',
    'Findings as assembled, with state already assigned from citations:',
    '```json',
    JSON.stringify(assembled, null, 2),
    '```',
    '',
    'Do three things and nothing else:',
    '',
    '1. Merge findings that are the same defect described differently. Four roles',
    '   looking at one artifact will sometimes reach the same problem from different',
    '   directions; leaving those as separate entries inflates the taxonomy counts',
    '   permanently, because the retro aggregates by category.',
    '   When merging, keep the entry with a citation over one without.',
    '',
    '2. Sharpen each summary to one sentence stating the defect — not its consequence,',
    '   not a recommendation.',
    '',
    '3. Return them ordered: blocking first, then suggested.',
    '',
    'Do NOT change any `state`, add a citation, or invent findings. State was assigned',
    'from citations by rule, and re-deciding it here would defeat the purpose of',
    'assigning it by rule.',
  ].join('\n'),
  { label: 'consolidate', phase: 'Merge', schema: MERGE_SCHEMA },
)

const findings = merged && merged.findings ? merged.findings : assembled

const blocking = findings.filter(f => f.state === 'blocking').length
const suggested = findings.filter(f => f.state === 'suggested').length
log(`${blocking} blocking, ${suggested} suggested`)

// The script has no filesystem access, so the caller persists this. It is shaped to
// validate against schemas/findings.schema.json.
return {
  'cycle-id': input.cycleId || null,
  target: input.target || (isCharter ? 'charter' : 'intent'),
  'charter-version': (parsedFacts && parsedFacts.charter && parsedFacts.charter.version) || null,
  'pack-version': null,
  round: ROUND,
  findings: findings,
}
