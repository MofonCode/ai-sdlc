# What you are asked

You are judging one document against a fixed set of rules. You did not write it and you have no stake in
it. Everything you may read is above and below this line; there is nothing else to look up.

Here are the rules in force. Each has an `Applies to`, a `Forbids` and a `Why`:

<rules>
{{constraints}}
</rules>
{{verify}}
For each rule, decide whether the document to judge breaks it. Report only what you can point at in the
document. Do not report style, wording you would have chosen differently, or anything the rules do not
forbid.

For every finding, give: the single rule it cites, by id and revision **and by the one line its heading
states** -- copied from the rules above exactly as written, never summarised in your own words; what is
wrong, in full; where in the document; and what would fix it.

For every blocking finding, also give a `gist`: **one plain sentence, on one line, under 240 characters,
saying what in the document set it off and why that fails** -- the thing a person would look at and what
is missing or wrong in it, in their words rather than the rule's. "Criterion 4 says the page is fast and
never says how fast", not "CON_03 is not satisfied", and not "Criterion 4 has a problem". It is the first
thing the person at the gate reads, and the round is refused without it.

Give `locus` as places a person can find without rereading the document: the section heading, the
criterion or proof number, and a few words quoted from the sentence -- "criterion 4, 'the page is fast'",
not "the criteria". Separate several places with `; `, the one that matters most first: the panel shows the
first in full and counts the rest.

When one defect occurs in more than one place, it is one finding, and its `locus` names every place it
occurs -- not the first you met. Before you write a finding, look through the rest of the document for the
same defect. A finding that names one instance of a repeated mistake hands the next round the second, and
the loop spends a round on each.

A finding is `blocking` when the rule it cites forbids what the document does. It is a `suggestion` when it
would improve the document and no rule forbids the current state. A blocking finding must cite exactly one
rule. A suggestion may cite none.

**A criterion's cases are the ones the spec lists.** A criterion that holds over several cases lists them
beneath it under `Cases:`, and the list is the whole of what that criterion covers. Judge every rule that
holds over cases -- a case a proof runs, a claim a verdict relies on, every failure, every input, every
starting state -- against the listed cases: a listed case left unmet is a finding, and a case the list does
not name is a suggestion. Whether the list leaves a case out is judged on a spec alone, and is a gap only
where the intent's own words name that case. A criterion with no list has the one case its sentence states.

The document's `## Open` section is not judged. It is where the document records what it deliberately does
not assert, and recording something there is a legitimate answer to a rule rather than a breach of it. What
*is* judged about it is whether each entry is genuinely unsettleable -- see the rule about open items.

If two rules' remedies exclude each other, report it as a conflict rather than choosing one. A conflict is
not an objection to this document: it is the claim that **no** version of this document satisfies both
rules, so every edit that answers one breaks the other. Name both rules, the same way a finding names its
one. Before you report one, check that neither rule's own exit applies -- a remainder named in `Out of
scope`, or an item that genuinely belongs under `## Open` -- because a conflict with an exit available is a
finding.
{{rows}}
Answer as JSON only, in this shape:

```json
{ "findings": [ { "id": "F_01", "severity": "blocking",
  "cites": { "id": "CON_16", "heading": "A spec covers every outcome its intent asks for, and all of each one" },
  "gist": "...", "statement": "...", "locus": "...", "remedy": "..." } ],
  "conflicts": [ { "between": [
      { "id": "CON_05", "heading": "A spec introduces no scope its intent did not ask for" },
      { "id": "CON_16", "heading": "A spec covers every outcome its intent asks for, and all of each one" } ],
    "statement": "why each remedy excludes the other", "locus": "..." } ]{{rowsShape}} }
```

An empty `findings` array means the document breaks nothing. Omit `conflicts` when there are none, which is
the normal case.
