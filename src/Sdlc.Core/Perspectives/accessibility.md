---
name: accessibility
always: false
areas: ui
files: "*.razor, *.cshtml, *.css, wwwroot/"
---

# Accessibility

Whether everybody the change is for can use it: by keyboard alone, through a screen reader, at 400% zoom,
with low vision or with motion turned down. The target is WCAG 2.2 at level AA unless the solution's
recorded decisions name another. On whenever a change touches a user interface, because what a person
cannot reach is a defect nobody on the team will ever see by using it the way they do.

## Method

1. **Run the automated scan first, and treat it as the floor.** An engine such as axe-core finds missing
   names, broken ARIA, insufficient contrast and invalid structure, and it finds roughly half of what is
   there. Zero violations is where the method starts, not where it ends.
2. **Walk the change by keyboard only.** Every control reachable with Tab, in an order that follows what a
   sighted person reads, operable with Enter, Space and the arrow keys a pattern implies, with focus visible
   at every step and never hidden behind a sticky header or an overlay.
3. **Follow focus through every change of state.** Where it lands when a dialog opens and when it closes,
   after navigation, after an item is deleted, after an error. Focus left on a removed element is focus
   sent back to the top of the page.
4. **Read every control as a screen reader would announce it.** A name that says what it does, a role that
   says what it is, a state that says whether it is pressed, expanded, selected or invalid. Native elements
   before ARIA: a `button` is all three for free, and a `div` with a click handler is none of them.
5. **Check what changes without being asked for.** A result that appears, a toast, a value pushed from the
   server: a live region announces it, and nothing moves focus to it uninvited.
6. **Check the visual edges.** Text contrast of 4.5:1 and 3:1 for large text and for the boundaries of
   controls, including every role a theme declares; 3:1 for a graphic that carries meaning, and still drawn
   when forced colours replace the author's backgrounds; reflow at 320 CSS pixels wide with no horizontal
   scroll; pointer targets of at least 24 by 24 pixels; animation that respects a reduced-motion
   preference; meaning carried by more than colour. **The scan decides none of these but text contrast**,
   so each is a browser test of its own or it is unproven.

## Cover this

### intent

- who uses this and how: keyboard, screen reader, magnification, touch
- the conformance target, if it is not the solution's standing one

### spec

- a criterion for each new interaction stated as what a keyboard user can do and where focus lands
- a criterion that the touched pages have no automated violations at the target level
- what is announced when something changes without the user asking
- what the scan cannot decide, as criteria of their own: focus visible on every new control, the touched
  page usable at 320 CSS pixels wide, and 3:1 contrast for any graphic that carries meaning
- the failure path held to the same criteria as the success path: where focus lands and what is announced

### plan

- the automated scan, which pages it runs on, and in which test layer — it needs a rendered page
- keyboard tests for each new interaction, asserting where focus lands, after a failure as well as a success
- a browser test for each criterion the scan cannot decide — focus visible, reflow at 320 CSS pixels,
  non-text contrast — and forced colours emulated wherever meaning is drawn with a background colour
- any scan rule disabled or element excluded, each named with its reason

### build

- native elements before ARIA, and ARIA only where no element carries the meaning
- every input has a programmatic label, every image a text alternative or an empty one if decorative
- focus moved deliberately on every state change the plan names, and never trapped

### verify

- the scan ran on every page the change touched and recorded zero violations
- keyboard tests exercised each new interaction rather than only rendering it
- focus visible, reflow and non-text contrast were tested, rather than inferred from a clean scan

## Questions

### intent

- Does anybody this is for use it without a mouse or without sight, and does the intent say so?

### spec

- Does every new interaction have a criterion a keyboard user could be tested against?
- Is there a criterion for what is announced when content changes on its own?
- Could every criterion be met while the page is unusable by keyboard?
- Could every criterion be met while a low-vision user cannot see focus, cannot read the page at 320 CSS
  pixels, or cannot make out a graphic that carries the result?

### plan

- Does the scan run where the page is actually rendered, rather than over markup in a harness?
- Is every disabled scan rule or excluded element named, with a reason someone could disagree with?
- Is focus placement planned for each dialog, navigation and deletion, and for a failure?
- Which criterion does the scan decide, and which does the plan assume a clean scan settles when it
  cannot?

### build

- Is any interactive element a `div` or `span` carrying a click handler?
- Is any input without a label, any image without a text alternative, any icon button without a name?
- Is any colour, contrast or motion choice made outside the theme's roles?

### verify

- Does the evidence show a scan with zero violations on each touched page, at the target level?
- Does the evidence show keyboard interaction, or only that the component rendered?
- Does the evidence show focus visible, reflow and non-text contrast, or only the scan?

## Rules

None of its own yet. A finding this method makes that no rule forbids is a suggestion; one that keeps
recurring across cycles is what `sdlc propose constraints` turns into a rule.

## Evidence

- the automated scan's result for every page the change touched, at the target level, with any disabled
  rule or excluded element listed
- keyboard tests that drive each new interaction and assert where focus lands
- browser tests for what the scan cannot decide: a visible focus indicator, reflow at 320 CSS pixels, and
  3:1 contrast for a graphic that carries meaning
