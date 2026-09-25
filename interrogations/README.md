# Interrogations

**Provisional location.** These are interrogation outputs that have no cycle to belong to.

The charter says a charter change comes from a `charter-review` intent, so a charter
interrogation normally runs *inside* a cycle and its findings live at
`.sdlc/cycles/<id>/`. `schemas/findings.schema.json` agrees: `cycle-id` is required and
must match `^[0-9]{8}-[0-9a-f]{4}$`, with no null permitted.

The first run of `interrogate` was a bootstrap run against the charter with no cycle open,
so it emitted `cycle-id: null` and the document does not validate. That is the schema
correctly rejecting an interrogation the artifact model does not provide for — not a
schema defect. Resolving it is a design decision, recorded in `PLAN.md`.

Files here are kept outside `.sdlc/` deliberately: `.sdlc/` is governed and schema-checked,
and a knowingly invalid document does not belong in it. When the decision is made, these
move into their cycle and this directory goes away.
