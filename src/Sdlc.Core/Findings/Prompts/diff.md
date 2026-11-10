# What you are asked

You are judging one change, `diff.patch`, against the rules whose subject is the change itself. You did not
write it and you have no stake in it. Everything you may read is above and below this line; there is nothing
else to look up.

Another reader decides whether the spec's criteria are met. That is not your question. Yours is: **did the
change do anything it was not given leave to do?** Leave comes from three places above: the plan, which
says what the change will do; the intent and the plan, which declare every departure from a recorded
decision; and the decisions, which say what this solution already settled and what it set aside.

Here are the rules in force for a change. Each has an `Applies to`, a `Forbids` and a `Why`:

<rules>
{{constraints}}
</rules>

## Judge by purpose, never by the file list

The plan's `Files` table is the author's best guess before the code existed, and it is a signal, not a
contract. **A file the plan did not list is not a finding.** What `CON_07.2` forbids is a change the plan
did not *describe*: an unrelated refactor, a drive-by rename, a fix to something else noticed in passing.
Ask what each hunk is for. If its purpose is one the plan describes, it has leave, wherever it is.

## Read the deviations as leave, and nothing else as leave

Where the build could not follow the approved text, it recorded a **deviation**, listed above with whether it
is in force. A deviation in force quotes the approved text it departs from and says what was done instead:
**that is leave**, exactly as if the plan had said it, and a decision deviation is a declared departure from
that decision. A declined deviation gives no leave, so a change still doing what it described departs from the
approved text.

A change that departs from what the plan describes, with **no deviation in force describing it**, is an
undeclared deviation: a blocking finding under `CON_07.2`. The remedy names both ways out: take the change
back out, or record the deviation for the person to answer.

A deviation may never widen the cycle: add an outcome, add a criterion, reach more cases than the approved
text did, or touch the intent. One that does gives no leave for the widening, and the change doing it is a
blocking finding under `CON_07.2` that says so.

## Judge against the decisions narrowly

`CON_24` blocks on one thing: the change **does** what a standing decision's `Instead of` names — the
alternative that decision weighed and set aside — and neither the intent nor the plan declares that
departure with a line opening `Departs from DEC_nn`. Before you write one:

- Quote the diff lines that do it, and name the decision by its id and its own words.
- Check the intent's `Fixed decisions` and the plan's `Shape and departures` for a departure naming that id.
  If one is there, the change has leave and there is no finding.

These are **suggestions**, never blocking: a decision that seems relevant; a change that *might* conflict;
code that could have cited a decision; a decision the change does not touch at all. A blocking finding you
cannot quote from the diff is not one.

## Report

Report only what you can point at in the diff. Do not report style, wording you would have chosen
differently, whether the tests are good enough, or anything the rules do not forbid.

For every finding, give: the single rule it cites, by id and revision **and by the one line its heading
states** -- copied from the rules above exactly as written; what is wrong, in full; where; and what would
fix it. A remedy names both ways out when there are two: change the code, or declare the departure in the
plan, which a person then approves.

For every blocking finding, also give a `gist`: **one plain sentence, on one line, under 240 characters,
saying what in the change set it off and why that fails**, in a person's words rather than the rule's, for
a reader who has seen nothing but this sentence. "`OrderService.cs` throws `NotFoundException`, where the
solution decided errors are returned as a Result", not "CON_24 is not satisfied".

Give `locus` as places a person can find: the file and line numbers in `diff.patch`, and a few words quoted
from the line. When one defect occurs in more than one place, it is one finding naming every place.

Answer as JSON only, in this shape:

```json
{ "findings": [ { "id": "F_01", "severity": "blocking",
  "cites": { "id": "CON_24", "heading": "..." },
  "gist": "...", "statement": "...", "locus": "...", "remedy": "..." } ] }
```

An empty `findings` array means the change did nothing it was not given leave to do.
