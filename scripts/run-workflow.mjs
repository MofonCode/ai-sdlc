#!/usr/bin/env node
// Execute one Workflow script the way the runtime does, against doubles, and report what it
// did. The companion to check-workflow.mjs, which parses a body and deliberately never runs
// it - a distinction that let a workflow whose wave loop cannot terminate pass every check
// this repository had.
//
//   node scripts/run-workflow.mjs <path-to-workflow.js> <path-to-scenario.json>
//
// The scenario supplies what the body would otherwise get from a live session:
//
//   { "args":           { ... },                    // the workflow's `args` global
//     "agentResponses": { "<label>": { ... } },     // one per agent({ label }) the body makes
//     "limits":         { "ms": 5000 } }            // wall-clock budget for the body
//
// Prints one JSON object and exits 0 when the body returns:
//
//   { "ok": true,
//     "phases":     ["Gate", "Build", ...],         // phase() calls, in order
//     "dispatched": [["gate:x"], ["build:a","build:b"]],  // agent labels, GROUPED
//     "result":     <the body's return value> }
//
// Exits 1 with { "ok": false, "error": "..." } when the body throws, dispatches an agent the
// scenario does not answer, or does not finish inside the budget.
//
// WHY THE GROUPING. `dispatched` is an array of arrays, not a flat list, because the one thing
// that makes a parallel build parallel is that a wave's agents are dispatched together. A flat
// list of labels is identical for a body that fans out and one that runs the same work in
// sequence, so a flat list cannot prove the thing worth proving.
//
// WHY A WORKER THREAD. The body's wave loop is synchronous. No timer fires inside it, no
// promise settles, and no await yields - so nothing in-process can interrupt it, and a
// Promise.race against a timeout simply never resolves. The body therefore runs in a worker
// and the parent terminates it on overrun, which interrupts the isolate mid-loop. Without
// this, a non-terminating body hangs until something further out gives up, and reports a
// timeout rather than the thing that actually happened.

import fs from 'node:fs'
import { fileURLToPath } from 'node:url'
import { Worker, isMainThread, workerData, parentPort } from 'node:worker_threads'

// The globals the Workflow runtime binds around a body. check-workflow.mjs binds the same
// five when it parses; WorkflowExecutionTests asserts the two lists match, so a body that
// parses here cannot call something the other validator never allowed for.
const BOUND = ['args', 'agent', 'parallel', 'pipeline', 'phase']

const DEFAULT_BUDGET_MS = 5000

if (isMainThread) {
  main()
} else {
  runBody()
}

// --- parent: spawn, bound, report ---------------------------------------------------------

function main() {
  const [workflowPath, scenarioPath] = process.argv.slice(2)
  if (!workflowPath || !scenarioPath) {
    console.error('usage: run-workflow.mjs <path-to-workflow.js> <path-to-scenario.json>')
    process.exit(2)
  }

  let scenario
  try {
    scenario = JSON.parse(fs.readFileSync(scenarioPath, 'utf8'))
  } catch (e) {
    return fail('scenario is not readable JSON - ' + e.message)
  }

  const budget = scenario.limits?.ms ?? DEFAULT_BUDGET_MS
  const worker = new Worker(fileURLToPath(import.meta.url), {
    workerData: { workflowPath, scenario },
  })

  let settled = false
  const timer = setTimeout(() => {
    if (settled) return
    settled = true
    worker.terminate().then(() =>
      fail(`the body did not terminate within ${budget}ms`)
    )
  }, budget)

  worker.on('message', (message) => {
    if (settled) return
    settled = true
    clearTimeout(timer)
    console.log(JSON.stringify(message, null, 2))
    process.exit(message.ok ? 0 : 1)
  })

  worker.on('error', (e) => {
    if (settled) return
    settled = true
    clearTimeout(timer)
    fail(e.message)
  })

  worker.on('exit', (code) => {
    if (settled) return
    settled = true
    clearTimeout(timer)
    fail(`the body exited without reporting (code ${code})`)
  })
}

function fail(error) {
  console.log(JSON.stringify({ ok: false, error }, null, 2))
  process.exit(1)
}

// --- worker: wrap the body the way the runtime does, and run it ---------------------------

function runBody() {
  const { workflowPath, scenario } = workerData
  const phases = []
  const dispatched = []

  const report = (message) => parentPort.postMessage(message)

  let src
  try {
    src = fs.readFileSync(workflowPath, 'utf8')
  } catch (e) {
    return report({ ok: false, error: 'workflow is not readable - ' + e.message })
  }

  // Same lift as check-workflow.mjs: meta is a pure literal the runtime reads separately, and
  // what remains is the body.
  const metaMatch = src.match(/export\s+const\s+meta\s*=\s*\{[\s\S]*?\n\}/)
  if (!metaMatch) {
    return report({ ok: false, error: 'no `export const meta = {...}` literal' })
  }
  const body = src.slice(0, metaMatch.index) + src.slice(metaMatch.index + metaMatch[0].length)

  const responses = scenario.agentResponses ?? {}

  // While a parallel() group is open, every agent dispatch joins it. Each thunk runs
  // synchronously up to its first await, and agent() records before awaiting anything, so a
  // group closes with exactly the labels that wave dispatched together.
  let openGroup = null

  const agent = async (prompt, options = {}) => {
    const label = options.label
    if (!label) {
      throw new Error('agent() was called with no label; the harness identifies dispatches by label')
    }
    if (openGroup) {
      openGroup.push(label)
    } else {
      dispatched.push([label])
    }
    if (!Object.prototype.hasOwnProperty.call(responses, label)) {
      // Deliberately not a default. A double that answers anything lets a body dispatch
      // something nobody expected and still pass.
      throw new Error(`the scenario has no response for agent label "${label}"`)
    }
    return responses[label]
  }

  const parallel = async (thunks) => {
    const group = []
    dispatched.push(group)
    openGroup = group
    let started
    try {
      started = thunks.map((thunk) => thunk())
    } finally {
      openGroup = null
    }
    return Promise.all(started)
  }

  // Bound for parity with the runtime rather than because this body uses it.
  const pipeline = async (items, ...stages) =>
    Promise.all(
      items.map(async (item) => {
        let value = item
        for (const stage of stages) {
          value = await stage(value)
        }
        return value
      })
    )

  const phase = (title) => {
    phases.push(title)
  }

  // Exactly the wrapper check-workflow.mjs parses, so the body that runs is the body that
  // was validated - top-level await and top-level return are legal inside it.
  let run
  try {
    run = new Function(...BOUND, 'return (async () => {\n' + body + '\n})()')
  } catch (e) {
    return report({ ok: false, error: 'body is not valid inside the async wrapper - ' + e.message })
  }

  run(scenario.args, agent, parallel, pipeline, phase).then(
    (result) => report({ ok: true, phases, dispatched, result: result ?? null }),
    (e) => report({ ok: false, error: e.message, phases, dispatched })
  )
}
