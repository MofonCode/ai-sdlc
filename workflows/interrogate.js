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
// Roles come from the CHARTER, read out of the attached facts. They are orthogonal by
// QUESTION, never by area: area lenses overlap constantly and dump deduplication on the
// merge, while these read different evidence and so rarely collide.
//
// They used to be hardcoded here, and the copy disagreed with the charter within a day:
// charter v1's `form` question omitted `Applies-to:` while this file asked for both clauses.
// That is INV-03, so there is now ONE source and NO fallback copy. A fallback would be the
// second list again, and interrogating with roles that might not match the charter is worse
// than declining to interrogate.
//
// Because the roles arrive with the facts, they cannot be known until the Facts phase has
// run. That is why activeRoles is computed there rather than here.
// ---------------------------------------------------------------------------

// A backslash via fromCharCode rather than a regex escape. The obvious form is
// `.replace(/[BACKSLASH][BACKSLASH]/g, '/')` and it did not survive: this file was generated
// through layers that ate one level of escaping, and it shipped SYNTACTICALLY INVALID for four
// commits - `/[BACKSLASH]/g` is an unterminated regex, so the only workflow would have thrown
// at launch. There is nothing here to lose a backslash from.
const isCharter = ARTIFACT.split(String.fromCharCode(92)).join('/').endsWith('CHARTER.md')

// When the artifact IS the charter, `charter` has nothing above it to check against and is
// replaced by `form`, which judges each invariant's SHAPE rather than its content. That
// substitution is what makes the charter interrogable without an infinite regress, and the
// charter itself declares both roles.
function rolesFromFacts(facts) {
  const declared = (facts && facts.roles) || []

  if (declared.length === 0) {
    throw new Error(
      'The charter declares no interrogator roles, so there is nothing to fan out. Roles are ' +
      'charter data as of charter v3 and this workflow deliberately keeps no copy of them.')
  }

  const byName = {}
  for (const r of declared) {
    if (r && r.name) byName[r.name] = r
  }

  const wanted = isCharter
    ? ['form', 'coherence', 'facts', 'gaps']
    : ['charter', 'coherence', 'facts', 'gaps']

  const active = []
  for (const name of wanted) {
    if (byName[name]) active.push(byName[name])
  }

  if (active.length === 0) {
    throw new Error('The charter declares roles, but none named: ' + wanted.join(', '))
  }

  return active
}

// What counts as MISSING differs completely between an intent and a plan, and leaving that
// unstated is what produces the `gaps` role's declared failure - inventing requirements
// nobody asked for. The charter carries the per-artifact list; this hands the relevant row to
// the role instead of asking it to guess.
function gapsGuidance(facts) {
  const kind = (facts && facts.artifact && facts.artifact.kind) || null
  const table = (facts && facts['gaps-by-artifact']) || {}
  if (!kind) return null
  return table[kind] || null
}

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

const activeRoles = rolesFromFacts(parsedFacts)
const gapsRow = gapsGuidance(parsedFacts)

log(`${activeRoles.length} role(s) from the charter: ${activeRoles.map(r => r.name).join(', ')}`)

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
        `Your characteristic failure is ${role['fails-by']} — guard against it.`,
        role.name === 'gaps' && gapsRow ? '' : '',
        role.name === 'gaps' && gapsRow
          ? 'For this kind of artifact the charter says a gap is: ' + gapsRow
          : '',
        role.name === 'gaps' && gapsRow
          ? 'That list is what you are entitled to raise. It is deliberately not exhaustive ' +
            'of everything that could be missing — reasoning beyond it is your job — but a ' +
            'finding you cannot tie to it should expect to stay `suggested`.'
          : '',
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

// THE GUARD ONLY GUARDS IF THE CITATION IS REAL.
//
// Until this existed, `cites.push({ kind: v.citeKind, id: v.citeId })` took whatever the
// triage agent named. So `{kind:'invariant', id:'INV-99'}` reached `blocking` with an
// invariant that does not exist, and `{kind:'check', id:'anything'}` reached it with no check
// at all. State was assigned in code, which is what the anti-inflation guard promises - but
// from an input nothing validated, which is what it was supposed to prevent.
//
// A CHECK CITATION IS STRICTER THAN THE OTHERS, deliberately. An invariant or a DEC only has
// to EXIST; a check has to have actually FAILED in this run. Citing a check that exists and
// passed is citing nothing, and it is the easiest false citation to write because the id looks
// entirely plausible.
const declaredInvariants = (parsedFacts && parsedFacts.charter && parsedFacts.charter['invariant-ids']) || []
const declaredDecisions = (parsedFacts && parsedFacts.charter && parsedFacts.charter['dec-ids']) || []
const declaredPackRules = (parsedFacts && parsedFacts.pack && parsedFacts.pack['rule-ids']) || []

function citationResolves(kind, id) {
  if (kind === 'invariant') {
    // Pack rules count: INV-04 delegates its substance to them, which is what makes every
    // pack rule citable through one invariant.
    return declaredInvariants.indexOf(id) !== -1 || declaredPackRules.indexOf(id) !== -1
  }
  if (kind === 'deferred') {
    return declaredDecisions.indexOf(id) !== -1
  }
  if (kind === 'check') {
    return failedChecks.indexOf(id) !== -1
  }
  return false
}

const rejectedCitations = []

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
    } else if (v.citeId && citationResolves(v.citeKind, v.citeId)) {
      cites.push({ kind: v.citeKind, id: v.citeId })
    }
    else if (v.citeId) {
      // The agent named something that does not exist, or a check that did not fail. The
      // citation is dropped, so the finding falls to `suggested` - which is the guard working
      // rather than an error.
      rejectedCitations.push(c.category + ' cited ' + v.citeKind + ' ' + v.citeId)
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

// Said out loud rather than swallowed. A dropped citation is the guard working, and it is also
// evidence about the triage stage: a role that keeps naming checks that did not fail is a role
// producing plausible-looking noise, which is exactly what the amendment signals want to know.
if (rejectedCitations.length > 0) {
  log(`${rejectedCitations.length} citation(s) rejected as unresolvable: ${rejectedCitations.join('; ')}`)
}

// The script has no filesystem access, so the caller persists this. It is shaped to
// validate against schemas/findings.schema.json.
return {
  'cycle-id': input.cycleId || null,
  target: input.target || (isCharter ? 'charter' : 'intent'),
  'charter-version': (parsedFacts && parsedFacts.charter && parsedFacts.charter.version) || null,
  'pack-version': null,
  round: ROUND,
  // PROVENANCE, so a check citation stays auditable after the run. Without it nothing
  // downstream can tell whether `{kind:'check', id:'x'}` named a check that failed, because
  // which checks failed is a property of the run and not of the artifact.
  'failed-checks': failedChecks,
  'rejected-citations': rejectedCitations,
  findings: findings,
}
