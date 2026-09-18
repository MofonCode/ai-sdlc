#!/usr/bin/env node
// Validate one Workflow script. This is the ONLY JavaScript left in this repository's
// tooling, and it exists for a reason no other runtime can satisfy: a workflow body has to
// be evaluated the way the Workflow runtime evaluates it, which means a JS parser.
//
// The runtime parses `export const meta` out and wraps the remaining body in an async
// function, so top-level `await` and top-level `return` are legal there. That is why
// `node --check` as an ES module is the WRONG validator - it rejects the top-level return
// this format requires - and why this file wraps before parsing.
//
// Everything else that used to live alongside this - manifests, frontmatter, skill bodies,
// hook targets - is plain file and string work and now lives in the MSTest project, where
// the rest of this repository's checks are.
//
//   node scripts/check-workflow.mjs <path-to-workflow.js>
//
// Exit 0 and print "ok", or exit 1 and print one problem per line.

import fs from 'node:fs'

const file = process.argv[2]
if (!file) {
  console.error('usage: check-workflow.mjs <path-to-workflow.js>')
  process.exit(2)
}

const problems = []
const src = fs.readFileSync(file, 'utf8')

const metaMatch = src.match(/export\s+const\s+meta\s*=\s*\{[\s\S]*?\n\}/)

if (!metaMatch) {
  problems.push('no `export const meta = {...}` literal')
} else {
  const body = src.slice(0, metaMatch.index) + src.slice(metaMatch.index + metaMatch[0].length)

  try {
    // Not executed - constructing the function is enough to parse it.
    new Function(
      'args', 'agent', 'parallel', 'pipeline', 'phase',
      'return (async () => {\n' + body + '\n})()'
    )
  } catch (e) {
    problems.push('body is not valid inside the async wrapper - ' + e.message)
  }

  let meta
  try {
    meta = new Function(metaMatch[0].replace('export ', '') + '; return meta')()
  } catch (e) {
    problems.push('meta is not a pure literal - ' + e.message)
  }

  if (meta) {
    if (!meta.name) problems.push('meta.name is missing')
    if (!meta.description) problems.push('meta.description is missing')

    // A phase() title the runtime does not know renders a blank phase. Nothing errors;
    // the progress view is simply wrong, which is the silent class this repo guards.
    const declared = (meta.phases ?? []).map((p) => p.title)
    const used = [
      ...new Set([...src.matchAll(/phase\(\s*['"`]([^'"`]+)['"`]\s*\)/g)].map((m) => m[1])),
    ]

    for (const title of used) {
      if (!declared.includes(title)) {
        problems.push(`phase("${title}") is not declared in meta.phases`)
      }
    }
    for (const title of declared) {
      if (!used.includes(title)) {
        problems.push(`meta.phases declares "${title}" but no phase() call uses it`)
      }
    }
  }
}

if (problems.length) {
  for (const p of problems) console.error(p)
  process.exit(1)
}

console.log('ok')
