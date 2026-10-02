---
id: 20260929_4e1a
intent: .sdlc/cycles/20260929_4e1a/intent.md
approved_by: Peter Almanzar
approved_at: 2026-09-29T01:06:00Z
---

# Spec — asking for the page returns one roll of a six-sided die, decided for that asking

Each criterion names what is observable and how it is checked, and cites the part of the intent it
answers to. Checks are made the way a player asks: requests to the page over its own interface, not
calls into internals. Criterion 2 and criterion 5 additionally require that the source the serving
code draws its value from can be substituted by a test; that is a testability requirement on the plan,
not a design for it.

## Acceptance criteria

1. **One roll, in range.** A request for the page produces a response presenting exactly one roll,
   whose value is an integer from 1 to 6 inclusive.
   *Check:* request the page and read the roll from the response. Assert exactly one roll is
   presented and its value is one of 1, 2, 3, 4, 5, 6. Fails on an absent roll, more than one roll, a
   non-integer, 0, or 7.
   *Answers to:* intent § Done looks like, first sentence — the value being one number from 1 to 6.

2. **The value is decided during the request that serves it.** No response presents a value obtained
   for an earlier request or fixed before the first request.
   *Check:* with the roll source substituted by one the test drives, issue three requests to the page
   with the source set to 3, then 3, then 5. The three responses present 3, 3 and 5, in that order.
   A value that is a constant, computed once at start-up, or replayed from a cached response fails,
   because the third response would not track the source. Requests are issued over the page's own
   interface, so a cache in front of the page fails this too.
   *Answers to:* intent § Fixed decisions, bullet 2.

3. **Nothing about a roll outlives the response that carried it.** Serving rolls creates, modifies or
   deletes no file, and records a roll in no database or other store.
   *Check:* snapshot the solution's run directory and any location it is configured to write to,
   issue a run of requests, then snapshot again — no file created, modified or deleted. The solution
   serves rolls with no data store configured and none reachable. Fails if any file appears or changes,
   or if serving a roll requires a store to be present.
   *Answers to:* intent § Must not change, bullet 1; § Done looks like, final sentence.

4. **No identity is established, requested or required.** A response sets no cookie and carries no
   session or user identifier, and the page asks nothing of the player before showing a roll.
   *Check:* inspect the raw response to a request from a client holding no prior state — no
   `Set-Cookie` header, no identifier-bearing field in the body — and confirm that request is served a
   roll. Fails on any cookie, any session or user token, or a page that withholds the roll pending
   sign-in, registration or identification.
   *Answers to:* intent § Must not change, bullet 2.

5. **No player-supplied input determines the value.** Nothing a player can send changes the rolled
   value.
   *Check:* with the roll source substituted and set to a known value, issue requests carrying
   attempted selections — a query parameter naming a different value, a header naming one, a request
   body naming one. Every response presents the value the source gave, not the one requested. Fails if
   any response presents a player-supplied value.
   *Answers to:* intent § Problem, second paragraph — that nobody chose the outcome. This covers only
   the settled half of impartiality; see **Blocked on**.

6. **The roll is delivered as a page a player can open.** A request a player can make with a browser
   returns a document the browser renders as a page, and the rolled value is visible in what it
   renders.
   *Check:* request the page the way a browser does. Assert the response is served as an HTML document
   — an HTML content type, and a body that parses as an HTML document — and that the roll's value
   appears in the text that document renders. Fails if the response is a bare data payload such as
   JSON or plain text, if it is anything a browser does not render as a page, or if the value appears
   only in metadata a reader does not see.
   *Answers to:* intent § Fixed decisions, bullet 1; § Done looks like, first sentence — that the
   player *sees* the number.

## Out of scope

- **Any requirement that consecutive rolls differ.** The intent rules it out directly (§ Done looks
  like): two equal values in a row are impartiality working, not a fault. Criterion 2's check sets two
  equal values deliberately, so a build that suppresses repeats fails it.
- **Throughput, latency, or concurrent use.** Intent Open 1 records that no usage figure exists and
  that no such criterion may be derived from it.
- **A limit on how many endpoints or pages the solution exposes.** The intent records that bound as
  offered in the interview and declined (§ Must not change, final paragraph), so no criterion here
  constrains it — and equally, nothing here licenses adding pages the criteria above do not need.
- **More than one die, dice of other sizes, modifiers, or dice notation.** No statement in the intent
  asks for them.
- **Roll history, re-showing a previous roll, or one player seeing another's.** Excluded by the
  no-persistence bound the intent sets (§ Must not change, bullet 1).
- **Accounts, profiles, or remembered preferences.** Excluded by intent § Must not change, bullet 2.

## Blocked on

- **A criterion for demonstrable impartiality — intent Open 3.** Criterion 5 covers the half the
  intent settles: that no person determines the value. Whether the *distribution* of values must be
  shown is not settled, and a distribution criterion needs both a sample size and a tolerance. Neither
  exists in the intent, and inventing either would produce a test that passes or fails on a number
  nobody chose. Answering Open 3 either adds a sixth criterion or records the question as closed.
- **A criterion for where players reach the page from — intent Open 2.** Whether the page is served
  only on the machine running it or is reachable from elsewhere is undecided, and the intent records it
  as a scope question to settle before a spec. None of the five criteria above depend on the answer,
  but no criterion about reachability, hosting or exposure can be written until it exists.
