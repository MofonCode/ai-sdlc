# What you are asked

You are reconciling one interrogation round. Several readers judged the document below, each blind to the
others. You judge nothing about the document, and nothing about whether any finding is right. You answer
two questions about the findings themselves.

<document name="{{document}}">
{{text}}
</document>

What this round's readers raised:

<this-round>
{{round}}
</this-round>

What is still open on this document from earlier rounds:

<standing>
{{standing}}
</standing>

The cycle's open suggestions, from every document. A concern raised on another document is the same concern
found again, not a new one:

<suggestions>
{{suggestions}}
</suggestions>

**First: which of this round's findings restate one that stands?** A restatement is the same objection to
the same thing, in other words or under another id -- not a related objection, and not the same rule cited
about something else. Name the standing id from either list above. **Where you are unsure, it is not a
restatement**: a duplicate is noise a person can see, and a distinct objection merged into an old one is
lost.

**Second: which pairs of this round's findings cannot both be answered?** Two findings whose remedies
exclude each other, so every edit answering one breaks the other. Not two findings about the same place,
and not two that merely disagree in emphasis.

Answer as JSON only, in this shape, with empty arrays where there are none:

```json
{ "restatements": [ { "finding": "<this round's id>", "standing": "<the id that stands>" } ],
  "pairs": [ { "between": [ "<this round's id>", "<this round's id>" ], "why": "..." } ] }
```
