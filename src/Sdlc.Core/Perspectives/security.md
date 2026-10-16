---
name: security
always: true
areas:
files:
---

# Security

Whether the change can be misused: by somebody it was not meant for, with input it did not expect, or to
reach something it should not. Always on, because every change that takes input or holds data has an
answer to this, and the cheapest place to find a missing one is before code exists.

## Method

1. **Name the trust boundaries the change touches.** Every place data crosses from somebody less trusted
   to somebody more trusted: a request into the service, a file into a parser, a message off a queue, a
   value read back from storage another party can write, a call out to a third party whose answer is
   believed.
2. **Follow every input from where it enters to where it is used.** Name what validates it on the way, and
   where it ends up: a query, a path, a command, markup, a log line, a redirect, a decision about access.
3. **At each boundary, ask the six questions.** Can somebody pretend to be somebody else (spoofing)? Change
   what they should not (tampering)? Deny having done it (repudiation)? See what they should not
   (disclosure)? Make it unavailable to others (denial of service)? Gain rights they were not given
   (elevation)?
4. **Name what the change holds.** Secrets, credentials, personal data: where each is kept, who can read
   it, and whether any of it reaches a log, an error message or a response.

## Cover this

### intent

- who may use what this changes, and who must not
- any data it handles that belongs to somebody: personal data, payment data, credentials

### spec

- a criterion for what an unauthorised or unauthenticated caller is told and is prevented from doing
- a criterion for malformed or hostile input at each new entry point
- what must never appear in a response, an error or a log

### plan

- the trust boundaries the change touches, and what validates each crossing
- where a new secret is kept, and that it is not in source
- which proof covers each abuse case the spec names

### build

- validate at the boundary, not deep inside; parameterise queries; encode output for where it lands
- no secret in source, configuration committed to the repository, or a log line
- record the vulnerable-package scan with the evidence

### verify

- the abuse-case criteria are met by tests that try the abuse, not only by the happy path

## Questions

### intent

- Who could misuse what this changes, and what would they gain?
- Does the intent handle data that belongs to somebody, and does it say whose?

### spec

- Is there a testable criterion for every way the intent's new capability could be misused — an
  unauthenticated caller, a caller acting for somebody else, hostile input?
- Does the spec say what a refused caller is told, without disclosing why in a way that helps them?
- Does any criterion require putting personal data or a secret into a response, an error or a log?

### plan

- Which trust boundaries does the plan cross, and what validates each crossing?
- Does any input reach a query, a path, a command or markup without validation or encoding named in the
  plan?
- Where does each new secret live, and could it reach source control?
- Does every abuse-case criterion have a proof that tries the abuse?

### build

- Does the change validate input where it enters, or trust it further in?
- Is any query built by concatenation, any path joined from input, any output written without encoding?
- Is there a secret in the diff, in committed configuration, or in a log statement?

### verify

- Does the evidence show each abuse-case criterion being attempted and refused, rather than only the
  intended use succeeding?
- Did the vulnerable-package scan run, and does it report a high or critical vulnerability?

## Rules

None of its own yet. A finding this method makes that no rule forbids is a suggestion; one that keeps
recurring across cycles is what `sdlc propose constraints` turns into a rule.

## Evidence

- `dotnet list package --vulnerable --include-transitive`, its output recorded with the build's evidence
- the analyzers' security rules, reported by the build that treats warnings as errors
- tests named for the abuse cases the spec states
