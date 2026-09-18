#!/usr/bin/env node
// Structural checks over the marketplace. Cheap, no dependencies, runnable in CI.
//
// This exists because every failure it catches is SILENT at runtime: a skill with broken
// frontmatter is not loaded and never announces itself, a workflow whose phase() titles do
// not match its meta renders a blank phase, and a plugin listed in the marketplace whose
// directory has moved simply does not install. None of that surfaces as an error - it
// surfaces as the framework quietly not working, which is the expensive kind of broken.
//
//   node scripts/check-plugins.mjs

import fs from 'node:fs'
import path from 'node:path'

const ROOT = path.resolve(import.meta.dirname, '..')
const problems = []
const fail = (where, msg) => problems.push(`${where}: ${msg}`)

const read = (p) => fs.readFileSync(p, 'utf8')
const walk = (dir, out = []) => {
  if (!fs.existsSync(dir)) return out
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name)
    if (e.isDirectory()) walk(p, out)
    else out.push(p)
  }
  return out
}
const rel = (p) => path.relative(ROOT, p).split(path.sep).join('/')

// Frontmatter must be the FIRST thing in the file; a leading blank line silently
// disables the whole block.
function frontmatter(file) {
  const src = read(file)
  const m = /^---\r?\n([\s\S]*?)\r?\n---(\r?\n|$)/.exec(src)
  if (!m) return null
  const fields = {}
  for (const line of m[1].split(/\r?\n/)) {
    const kv = /^([A-Za-z_][\w-]*):\s*(.*)$/.exec(line)
    if (kv) fields[kv[1]] = kv[2].trim()
  }
  return fields
}

// --- marketplace and plugin manifests -------------------------------------------------
const mkPath = path.join(ROOT, '.claude-plugin', 'marketplace.json')
let marketplace
try {
  marketplace = JSON.parse(read(mkPath))
} catch (e) {
  fail(rel(mkPath), `invalid JSON - ${e.message}`)
}

for (const entry of marketplace?.plugins ?? []) {
  const dir = path.resolve(ROOT, entry.source ?? '')
  if (!fs.existsSync(dir)) {
    fail(rel(mkPath), `plugin "${entry.name}" points at a missing directory: ${entry.source}`)
    continue
  }
  const manifestPath = path.join(dir, '.claude-plugin', 'plugin.json')
  if (!fs.existsSync(manifestPath)) {
    fail(rel(dir), 'no .claude-plugin/plugin.json')
    continue
  }
  let manifest
  try {
    manifest = JSON.parse(read(manifestPath))
  } catch (e) {
    fail(rel(manifestPath), `invalid JSON - ${e.message}`)
    continue
  }
  if (manifest.name !== entry.name) {
    fail(rel(manifestPath), `name "${manifest.name}" does not match marketplace entry "${entry.name}"`)
  }
  if (!/^\d+\.\d+\.\d+$/.test(manifest.version ?? '')) {
    fail(rel(manifestPath), `version "${manifest.version}" is not semver - updates are cached per version`)
  }
}

// --- skills and agents ----------------------------------------------------------------
for (const file of walk(path.join(ROOT, 'plugins'))) {
  const r = rel(file)
  const isSkill = r.endsWith('/SKILL.md')
  const isAgent = /^plugins[/][^/]+[/]agents[/][^/]+[.]md$/.test(r)
  if (!isSkill && !isAgent) continue

  const fm = frontmatter(file)
  if (!fm) {
    fail(r, 'no YAML frontmatter as the first line - this file will not be loaded')
    continue
  }
  for (const key of ['name', 'description']) {
    if (!fm[key]) fail(r, `frontmatter is missing \`${key}\``)
  }
  if (isSkill) {
    const dirName = path.basename(path.dirname(file))
    if (fm.name && fm.name !== dirName) {
      fail(r, `frontmatter name "${fm.name}" does not match its directory "${dirName}"`)
    }
  }
  if (isAgent && fm.name && fm.name !== path.basename(file, '.md')) {
    fail(r, `frontmatter name "${fm.name}" does not match its filename`)
  }
}

// --- skill and agent BODIES -----------------------------------------------------------
// Frontmatter validation alone let a command ship with a literal backslash-n in it and an
// unclosed fence that swallowed the rest of its section. Nothing raised an error: the skill
// loaded fine and simply told the agent to run a command that cannot execute. That is the
// silent-failure class this repo is most exposed to, so bodies get checked too.
const BACKSLASH_N = String.fromCharCode(92) + 'n'

for (const file of walk(path.join(ROOT, 'plugins'))) {
  const r = rel(file)
  if (!r.endsWith('/SKILL.md') && !/^plugins[/][^/]+[/]agents[/][^/]+[.]md$/.test(r)) continue

  const lines = read(file).split(String.fromCharCode(10)).map((l) => l.replace(String.fromCharCode(13), ''))
  let inFence = false
  let fenceOpenedAt = 0

  lines.forEach((line, i) => {
    const n = i + 1
    const trimmed = line.trim()

    if (trimmed.startsWith('```')) {
      if (!inFence) {
        inFence = true
        fenceOpenedAt = n
      } else if (trimmed === '```') {
        inFence = false
      } else {
        // A closing fence with trailing text does not close the block: everything below is
        // swallowed into the code block and never read as instructions.
        fail(r, 'line ' + n + ': closing fence has trailing text - it does not close the block opened at line ' + fenceOpenedAt)
        inFence = false
      }
      return
    }

    // A literal backslash-n is never intended in a command or in prose. It is the signature
    // of a string written through one escaping layer too many.
    if (line.includes(BACKSLASH_N) && !trimmed.startsWith('|')) {
      fail(r, 'line ' + n + ': literal backslash-n in body - ' + trimmed.slice(0, 60))
    }
  })

  if (inFence) fail(r, 'unclosed code fence opened at line ' + fenceOpenedAt)
}

// --- workflows ------------------------------------------------------------------------
for (const plugin of fs.readdirSync(path.join(ROOT, 'plugins'))) {
  const wfDir = path.join(ROOT, 'plugins', plugin, 'workflows')
  for (const file of walk(wfDir).filter((f) => f.endsWith('.js'))) {
    const r = rel(file)
    const src = read(file)
    const m = src.match(/export\s+const\s+meta\s*=\s*\{[\s\S]*?\n\}/)
    if (!m) {
      fail(r, 'no `export const meta = {...}` literal')
      continue
    }
    const body = src.slice(0, m.index) + src.slice(m.index + m[0].length)
    try {
      // The runtime wraps the body in an async function, so top-level await and return
      // are legal here and `node --check` as ESM would wrongly reject them.
      new Function('args', 'agent', 'parallel', 'pipeline', 'phase',
        `return (async () => {\n${body}\n})()`)
    } catch (e) {
      fail(r, `body is not valid inside the async wrapper - ${e.message}`)
      continue
    }
    let meta
    try {
      meta = new Function(`${m[0].replace('export ', '')}; return meta`)()
    } catch (e) {
      fail(r, `meta is not a pure literal - ${e.message}`)
      continue
    }
    if (!meta.name) fail(r, 'meta.name is missing')
    if (!meta.description) fail(r, 'meta.description is missing')
    const declared = (meta.phases ?? []).map((p) => p.title)
    const used = [...new Set([...src.matchAll(/phase\(\s*['"`]([^'"`]+)['"`]\s*\)/g)].map((x) => x[1]))]
    for (const t of used) {
      if (!declared.includes(t)) fail(r, `phase("${t}") is not declared in meta.phases`)
    }
    for (const t of declared) {
      if (!used.includes(t)) fail(r, `meta.phases declares "${t}" but no phase() call uses it`)
    }
  }
}

// --- hooks ----------------------------------------------------------------------------
for (const plugin of fs.readdirSync(path.join(ROOT, 'plugins'))) {
  const hooksPath = path.join(ROOT, 'plugins', plugin, 'hooks', 'hooks.json')
  if (!fs.existsSync(hooksPath)) continue
  let hooks
  try {
    hooks = JSON.parse(read(hooksPath))
  } catch (e) {
    fail(rel(hooksPath), `invalid JSON - ${e.message}`)
    continue
  }
  for (const group of Object.values(hooks.hooks ?? {}).flat()) {
    for (const h of group.hooks ?? []) {
      const m = /\$\{CLAUDE_PLUGIN_ROOT\}\/(\S+?)["\s]/.exec(h.command + ' ')
      if (!m) continue
      const target = path.join(ROOT, 'plugins', plugin, m[1])
      if (!fs.existsSync(target)) {
        fail(rel(hooksPath), `command points at a missing file: ${m[1]}`)
      }
    }
  }
}

// --- report ---------------------------------------------------------------------------
if (problems.length) {
  console.error(`\n${problems.length} problem(s):\n`)
  for (const p of problems) console.error(`  ${p}`)
  process.exit(1)
}
console.log('check-plugins: all structural checks passed')
