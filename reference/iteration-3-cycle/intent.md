---
id: 20260929_4e1a
type: solution
approved_by: Peter Almanzar
approved_at: 2026-09-29T01:05:00Z
---

# Players with no die to hand have no way to get an impartial roll

## Problem

A game needs a number from 1 to 6 and there is no die available — at a table, or with the players
apart. Nothing here answers that: this repository holds no solution at all, so the need is unmet by
absence rather than by something that works badly.

What is wanted is not the number by itself. Anyone can say "four". The point of a die is that nobody
chose the outcome, so what is missing is a roll the players can treat as impartial and get on with
the game. How often that has happened, or how much it has cost a game so far, nobody has measured —
see **Open**.

## Who

Players in a game who do not have a physical die, together or apart. How many of them, how often
they would use it, and whether more than one would roll at the same time were not established in
this interview and are recorded under **Open**.

## Done looks like

A player asks for the page and sees one number from 1 to 6 that was produced for that asking. Asking
again produces its own roll, decided independently of any roll before it. Two askings in a row may
therefore show the same number, and that is impartiality working rather than a fault: no criterion may
require consecutive rolls to differ. Nothing about the previous roll is still lying around.

## Fixed decisions

- **A web page the player asks for** is how the roll is delivered. Decided by Peter Almanzar,
  2026-09-29T00:48:08Z, in this interview.
- **The number is decided per request, on the serving side.** A cached or static page that shows a
  number decided earlier does not satisfy this, even if the number it shows is in range. Decided by
  Peter Almanzar, 2026-09-29T00:48:08Z, in this interview.
- **No departure from the stack `.sdlc/solution.md` declares.** Confirmed by Peter Almanzar,
  2026-09-29T00:48:08Z. That file owns the declaration; this intent only records that the answer was
  yes, so there is no `stack` key in its frontmatter.

## Must not change

Nothing exists here to preserve, so these are bounds on what the solution may become rather than
behaviour to keep:

- **Nothing about a roll outlives the response.** No database, no stored roll history, no files
  written.
- **No accounts or identity.** No sign-in, no users, no sessions, and no cookie that identifies
  anyone.

Two further bounds were offered in the interview and **not** taken, so they are not bounds and must
not be treated as any: staying strictly to one page with no other endpoint, and never being deployed
publicly. The second is unresolved rather than permitted — see **Open**.

## Open

1. **How often, by how many players, and how many at once?** No figure was given and none is
   established. A spec must not turn this into a throughput or latency criterion; there is nothing
   to derive one from.
2. **Where do players reach it from?** Only on the machine serving it, or from elsewhere. Public
   deployment was offered as a bound and declined, which leaves it neither ruled out nor decided.
   This is a scope question and it should be settled before a spec is written.
3. **Does impartiality have to be demonstrable?** It is settled that nobody chooses the number. It is
   not settled whether that has to be shown — whether the spec needs a criterion about the
   distribution of rolls, or whether it is enough that no person or stored value determines one.
4. **What counts as asking again?** The request says the roll is rerolled "each time the page is
   asked for", which does not say whether asking is only a fresh page request, or whether players
   expect something on the page to ask with.
