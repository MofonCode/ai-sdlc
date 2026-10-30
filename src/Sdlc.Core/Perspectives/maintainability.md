---
name: maintainability
always: true
areas:
files:
---

# Maintainability

Whether the next person can change this code safely: find what to change, change it in one place, and know
from the tests that nothing else broke. Always on, because every change is somebody's starting point for the
next one.

## Method

1. **Name the next likely change.** The one the intent or the backlog makes probable: another variant,
   another field, another rule of the same kind.
2. **Count what it would touch.** How many files, how many places restate the same knowledge, how many
   components need to know about it. A change that would touch many is coupling the plan could have avoided.
3. **Read it as somebody who has never seen it.** Does each new name say what it is, does each new unit do
   one thing, and is anything left in that nothing uses?
4. **Check the tests describe the behaviour.** Would a refactor that kept the behaviour keep the tests
   passing, and would a change to the behaviour make one fail?

## Cover this

### intent

- what is likely to change next in the same area, if the person knows

### spec

- criteria stated as behaviour a test can check, not as the structure that produces it

### plan

- where the new knowledge lives, once, and what depends on it
- the next likely change, and how many files it would touch under this shape
- tests that describe behaviour rather than mirror the implementation

### build

- one responsibility per new type; names that say what a thing is
- no dead code, no commented-out code, no copy of logic that already exists
- complexity and coupling within what the analyzers allow

### verify

- the tests named for each criterion check behaviour, so they would survive a refactor that kept it

## Questions

### intent

- Is there an obvious next change in this area the intent should mention, so the shape can leave room for it?

### spec

- Does any criterion describe how the code is built rather than what it does?

### plan

- Does the plan put one piece of knowledge in more than one place?
- How many files would the next likely change touch under this shape, and is there a shape that touches fewer
  without adding a layer nobody needs?
- Do the named tests check behaviour, or would they break on a refactor that kept it?

### build

- Is there duplicated logic, dead code, or a type doing two jobs?
- Do the new names say what each thing is?
- Did the analyzers report complexity or coupling above their limits?

### verify

- Would the tests that prove each criterion still pass after a refactor that kept the behaviour?

## Rules

None of its own yet; `CON_07.2` already forbids a change doing more than its plan declared. A finding this
method makes that no rule forbids is a suggestion; one that keeps recurring across cycles is what
`sdlc propose constraints` turns into a rule.

## Evidence

- the analyzers' complexity and coupling metrics, reported by the build that treats warnings as errors
- the diff, read for duplication and dead code
