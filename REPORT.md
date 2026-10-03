# Report and development plan

**This file was rewritten on 2026-10-03.** The previous version was written on 2026-10-01 and every
open item in it has since been closed, so leaving it would have been a document that lies. What it
claimed, and what is actually true:

| REPORT.md said | Reality |
|---|---|
| "There is no boss health bar" | Wrong when written; fixed, then redesigned to be readable |
| "There is no area concept. Every floor is the same floor." | **Done** — 3 themed areas + Final, by enemy mix *and* palette |
| "The C# port is 1,360 lines against 15,300. Roughly 9%." | 3,045 lines against 21,422 JS (13 ports, 163 checks) |
| "88 C# checks" | 163 |
| "~15,300 lines of JavaScript" | 21,422 across 17 modules |
| `verify.ps1` clean, as the high-water mark | It could not run the JS suite at all. It can now |
| Arcane Beam 87.2s unbuilt TTK "should be chosen rather than discovered" | Still true, still undecided — see Open |

It also recommended an order. That order was followed, with two items reordered for cause.

---

# Part 1 — what this project is

**Depths** is a canvas top-down roguelite. No build step: double-click `depths.html`. `src/` is the
authoritative implementation; `csharp/` is a parity port of the deterministic core, kept in step for a
Unity ship.

| | |
|---|---|
| JavaScript | 21,422 lines across 17 modules, ~213 checks. 16 are game + tests; `97-parity.js` is a maintenance page that loads only under `?parity` |
| C# | 3,045 lines in `Depths.Core`, 163 checks, all parity-verified against the running JS |
| Content | 4 guns + 2 alts · 5 body types · 13 items (9 passive, 4 active, one Q slot) · 4 areas · unbounded exponential ladder · seeds · dev lab |
| Spec | `CONVENTIONS.md` is the real document. This file is the status. |
| Port boundary | `PORTED.md`, checked by `verify.ps1` step 6b |

**The gate runs the suite.** `verify.ps1` executes `depths.html?test` headlessly at **three
viewports** and asserts the count `CONVENTIONS.md` claims. It could not do this before, and every gap
in the project traced back to that.

---

# Part 2 — what was found and fixed on 2026-10-03

The unifying lesson: **three of the four defects were invisible at 1280×720**, which is the window a
developer gets. They only appeared at 960×600 — the canvas's own design size.

1. **Pointer mapping aimed off the left of the screen.** `clientLeft`/`clientWidth` are integers;
   `getBoundingClientRect()` is not. At 960×600 the nearest position a cursor can physically occupy
   mapped to −0.678. A `MouseEvent` cannot carry a fractional `clientX`, so the old test asserted
   something no cursor can do — and its 1px tolerance was wider than the bug.

2. **The character sheet clipped its head, not its foot.** A centred flex item taller than its
   container is pushed above the scroll origin and can never be scrolled to. The assertion meant to
   catch it compared `card.height <= sheet.scrollHeight`, and `scrollHeight` *is* the content height,
   so it failed by construction whenever a scrollable box worked correctly.

3. **The descent scrim fixed the type and buried the player.** The camera clamp pins the player to
   the exact centre of the canvas on every descent, and the banner is centred there. The 0.90 scrim
   dropped the sprite from 236,232,245 to 46,44,46 — 17% of its brightness.

4. **A misspelled `FIXES` category took the whole bug panel down.** `byCat[it.cat]` was `undefined`
   for a name not in `CAT_ORDER`, so `.push` threw and the panel — whose entire job is reporting
   failures — rendered nothing.

5. **`verify.ps1` could not start its own server**, because the generated script derived its project
   root from `%TEMP%`. It reported a server fault while the fault was three lines of path arithmetic.

Also closed from the previous session's unfinished work: the run-scoped art caches (`clearArtCaches`
is now wired into `startGame`, after `Rnd.set`), the `FIXES[0]` guard, `hexRgb` shape validation, and
the descent banner naming the area (`areaForFloor` now takes an optional floor, matching C#).

Every fix was **mutation-checked** — deliberately broken, confirmed the suite goes red, restored. Two
new capabilities exist because of this session's work: `depths.html?parity` prints the C# parity table
from the running game (13/13 signatures match the committed literals exactly), and `verify.ps1` runs
the suite at three viewports.

---

# Part 3 — Open, and honestly so

### 1. The C# port is at the tick boundary

`Tick.cs` is a 203-line skeleton; the ~868-line `update()` body is unimplemented. This is the only
path to a shipped `.exe`.

**The measurement that decides how to resume it:** `update()` contains **zero** references to `ctx`,
`document`, `performance.now` or `Date.now`. The simulation is already free of the host, so porting it
is not tangled with rendering — the expensive version of this problem, which this codebase does not
have. Slice it in tick order (projectiles → bodies → player → room), a parity test per slice. The
`?parity` emitter exists to make the row refresh cheap enough that nobody skips it.

### 2. Five parked visual items, needing a human eye

Framebuffer-measured, deliberately not guessed at: minimap palette neutrality (paper's warm bias
swamps the per-area ink by +14.4), mouse glyph badges straddling slot corners, the Lab's middle
specimen name occluded by the player, the Lantern Friend caption colliding with the Warden's plinth,
and the Warden readout clipped at the right canvas edge.

### 3. Feel work, and it wants a player not more code

Scatter recoil, hook rework, Brunch-arc AI. The Arcane Beam is the counter-example: it landed because
it was measured at 200px with 25 trials and a human chose between two options with numbers attached.

### 4. The Arcane Beam's unbuilt floor is 87.2 seconds

A minute and a half of shooting at a boss with nothing invested in it. Deliberate — 0.5 base damage is
precisely what makes Strength sigils worth 3.5× to it — but it should be *chosen*. If the first real
run to floor 10 with a fresh Beam feels like a wall, this number is why, and the fix is a starting
Strength floor rather than a nerf.

### 5. Accessibility: AZERTY movement

Movement reads `e.key`, so on a French layout the key where QWERTY keeps A fires the **item**. The fix
is four lines, deliberately not applied: a keybind screen is coming, and a half-migration now would
mean writing the physical-key path and then replacing it.

---

# Part 4 — recommended order

**First, keep the gate green.** It is what makes everything else trustworthy.

**Then, in order:**

1. **The five visual items**, in one sitting with screenshots. They are measured, they are small, and
   they are the only items that need nothing but your eye.
2. **Feel work**, one playtest session with the numbers on screen.
3. **Resume the C# port at `update()`**, sliced by tick order. Not before — the port is insurance, and
   the game is not yet the thing being insured.
4. **Keybinds**, when the input layer is next touched for any other reason.

**Not recommended, and why:** refactoring the O(P×N) projectile collision. It is measured at 8.7% of
frame budget at a body count (`DEPTH_BODY_CAP=28`) the game forbids. Optimising something that is not
slow is how real slowdowns get introduced.

---

# Queued fixes, in the order they were raised

Two of these were raised after the audit above and are not in it.

1. **The boss volley has no tell.** `stepBoss` solves the intercept and sets `castAim` for the volley
   exactly as a gunner does, but a boss is not a gunner: at Warden scale a shot you cannot read is a
   shot you cannot answer. The gunner's tell is the model to copy — aim committed at cast start,
   visible before the shell leaves. Measured first, like everything else here: how long the volley
   takes to arrive, and whether the player has any window at all at the range a Warden is fought at.
   (This was item 2; it is now item 1 because item 1 turned out not to exist.)

2. **Enemy mix per floor, and therefore how often an escort encounter happens.** 32.8% of generated
   rooms contain both a Brunch pack and a ranged enemy, which is the shield mechanic appearing in real
   play roughly one room in three. That is a floor-1 number on the current ladder, and the ladder has
   no per-floor enemy roster to speak of — every floor draws from the same `areaMix`. When the
   per-floor enemy types are elaborated, decide this factor deliberately rather than letting it
   emerge: the number to choose is what share of rooms *should* pair a pack with something worth
   guarding, per floor. Higher on floors where the new types are ranged, lower where they are not —
   a pack escorting a lunger is a crowd, not a wall.

3. **`saveRecords`' empty `catch`.** A failed write leaves records half-written with no report. Low
   severity — localStorage rarely fails — but a swallowed exception here means a lost run record
   with nothing in the log to explain it.

4. **Three tests that cannot fail.** From the audit, not yet re-verified line by line.

5. **Stale comment/code mismatches.** Approximately eight sites where a comment quotes a number the
   code no longer uses. Several were fixed in passing during the Brunch and hit-rate work; the rest
   are unaudited.

**There is no audio in this project.** Worth stating plainly, because it was nearly queued as a bug.
An earlier version of this list carried "pitch-jitter feedback: `run.pitch` is written and never read,
the audio layer sets a pitch multiplier on damage and the momentum meter". There is no audio layer,
`run.pitch` has never existed in any commit in this repository's history (`git log -S"run.pitch" -- src/`
returns nothing), and the project contains no `AudioContext`, no oscillator and no sfx table of any
kind. The item was invented, and it was invented by this file's own author while tidying the list —
which is the strongest argument for the rule at the bottom of this section. A queue is not a place to
park a plausible-sounding thought. If audio is wanted it is a feature to design, not a defect to fix.

**On the Brunch commitment, since it has been through three shapes in one session.** The rule is now
the user's and it is short: a pack holds its escort until the escorted body is dead, and sprints at the
player only when there is nothing left to shield. A distance-based leash was implemented first, on the
reasoning that a pack should not guard a fight the player has walked out of. That was wrong — it made
the wall conditional on the player's habits, so the mechanic was only present when they happened to be
nearby. If a future change makes the pack conditional on anything other than target death, it is
probably making the same mistake.

**The rule for all of these: measure first, and check that the thing exists at all.** Every real item
was found by measurement rather than by reading, and roughly half of what looked like a bug during
this session turned out to be a fixture that was lying — a body in a room the tick never walks, a
player teleported instead of driven through the real input path, a probe comparing against the sprite
origin rather than the hitbox, and a commitment test that passed vacuously because the alternative
candidate was further from the pack. A probe that cannot fail is worse than no probe, because it
produces confident nonsense; and a queue item that was never real is worse still, because it costs a
future session the time to disprove it.