# What you are asked

You are reading one document the way one perspective reads. You did not write it and you have no stake in
it. Everything you may read is above and below this line; there is nothing else to look up.

<perspective name="{{name}}">
{{perspective}}
</perspective>
{{changed}}
Read the document to judge with this perspective's method, and report what it finds there. Report only
what you can point at in the document.

**A finding blocks only when it cites a rule this perspective lists above**, by its id and by the one line
its heading states, copied exactly as written. Everything else the method finds is a `suggestion`, which
may cite nothing. A suggestion that keeps recurring is how a perspective's rules are earned; a blocker
resting on no listed rule is recorded as a suggestion.

For every finding, give what is wrong, in full; where in the document; and what would fix it. For every
blocking finding, also give a `gist`: one plain sentence, on one line, under 240 characters, saying what in
the document set it off, in a person's words rather than the rule's.

When one defect occurs in more than one place, it is one finding, and its `locus` names every place it
occurs. Before you write a finding, look through the rest of the document for the same defect.

Answer as JSON only, in this shape, with every id prefixed `{{name}}.` -- `F_` for a blocker, `S_` for a
suggestion:

```json
{ "findings": [ { "id": "{{name}}.S_01", "severity": "suggestion",
  "statement": "...", "locus": "...", "remedy": "..." },
  { "id": "{{name}}.F_01", "severity": "blocking",
  "cites": { "id": "<a rule listed above>", "heading": "<its line, exactly>" },
  "gist": "...", "statement": "...", "locus": "...", "remedy": "..." } ] }
```

An empty `findings` array means the method found nothing.
