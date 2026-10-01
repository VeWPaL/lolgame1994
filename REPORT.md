# Report and development plan

Written during a working session, resumed after an unattended one ended in a power cut. Two parts:
what was done and found, then what to build next and why. Every claim here was measured; where
something is an estimate it says so.

> **On time spent.** The unattended session below was reported as spanning 00:03–02:39 by commit
> timestamps. That was a mistake in arithmetic dressed as a measurement: commits land at task
> boundaries, so the gap between the first and last one is a *lower* bound on the work, and it is not
> the session — the session began before the first commit, at the point the interrupted suite run was
> resumed. The user went to bed at about 01:00, so the true figure is **roughly one hour**, and the
> machine lost power at 02:39 with nothing uncommitted. Reports from here on state elapsed time from a
> start timestamp recorded at the beginning of the session (`.session-start`, gitignored), not
> inferred from commit times.

---

# Part 1 — what happened this session

## 1. The camera and world frame, ported to C#

`csharp/Depths.Core/Frame.cs`: `RoomBounds`, `RoomFrame`, `Camera`, `Frame.CameraTarget`,
`Frame.Update`, `Frame.RoomBoundsAt`. Nine C# tests.

The port is deliberately **not** shaped like the JavaScript. `00-balance.js` keeps `cameraTarget` as
one function reading a module-scope player, a module-scope bounds accessor and a module-scope
viewport — three hidden inputs. In C# those are parameters, so the clamp is directly testable, and
the shorthand is a `RoomFrame` struct with one `Sync` method rather than four static fields, which
makes "one writer" structural instead of a comment.

The test that earns its keep is **the oversized clamp**. That branch was wrong in JavaScript for as
long as the camera existed: `Math.min(b.l, …)` with the player's offset inside, which pinned the
view to the room's left edge whenever the player was right of centre, so it never moved at all. It
passed everything because every room in the game fits on screen and that line had never executed
once. The C# tests deliberately use a 1400-wide room.

## 2. The boss time-to-kill test was not measuring anything

The loop ran to 9,000 ticks — 42.9 seconds — and the window it checked was 18–70s. Anything slower
came back as 42.9s, so the upper bound could never fail and three of four guns were reporting the
loop cap as a measurement.

| gun | measured | note |
|---|---|---|
| Scatter | 25.2s | real |
| Bolt | 45.1s | **capped** |
| Voidball | 55.9s | **capped** |
| Arcane Beam | 87.2s | **capped by 2x** |

It is now a 200-second budget and asserts a **spread (×4) rather than absolute seconds**, because
"the slow gun should take longer" is a claim about the ratio between fastest and slowest, not about a
number that goes stale every time a weapon is tuned. Measured spread ×3.47.

That number is worth keeping on its own: `noCharacter()` does zero Strength (the class gives +3 flat
over a base of 0), so the test is every gun with *nothing* invested in it — the floor of the Arcane
Beam's canvas. **87.2s on an unbuilt character, 12.5s on the starting build.**

## 3. Lantern Friend ate its one charge for nothing

It wrote `run.companionPending`, which nothing ever read, and reported success — so pressing Q cost
the only charge in the item and produced no companion. It now **returns false**, so `use()` declines,
the charge is kept, and the reason is surfaced. The flag is **deleted** rather than left accumulating,
because a flag with no reader is the sort of thing someone reads in three months and assumes it means
what it says.

Two of my own tests broke on this, correctly: they built the whole roster, which leaves Lantern Friend
as the active, and were measuring the refusal while calling it the binding. Both now place a working
active explicitly.

## 4. A duplicate function declaration

`updateCamera` was declared **twice** in `00-balance.js` with identical bodies. Function declarations
hoist, so the second silently overwrote the first — harmless until somebody edits one of them, which
is the entire hazard. Removed, with a note.

## 5. Stale numbers in comments and conventions

- `10-art.js` and `CONVENTIONS.md` both still said the boss was `520*TOUGH = 702 HP` and quoted a
  28-second fight. It is now `343*TOUGH = 463.05` and the measured figures are 22.2s Scatter,
  29.1s Voidball, 29.9s Bolt, 12.5s Arcane Beam.
- `CONVENTIONS.md` also said **"No armour, deliberately"** for a boss that is now armoured at 0.66.
- `CONVENTIONS.md` claimed **79** C# checks when the suite runs **88**.

## 6. The bench row — three fixes, two of them wrong in a way the suite could not see

The row under the minimap is three plates: weapon, active item (Q), alt. Each carried a text label
centred under it. `Arcane Beam` is 73px in a 62px plate; eleven of thirteen item names are wider than
their plate, `Lantern Friend` by thirty pixels. They overlapped.

**Fix 1 — a budget per label, derived from its neighbours.** The arithmetic is correct: three labels
62px apart do not collide when `w2 <= 124 - max(w1,w3)`. I wrote it, tested it, took a screenshot:

```
ARCAN...   UNTER'S ...   BLAST
```

No overlap. Also no information. **It fixed a collision by shortening the text until a collision was
impossible, which is the one thing a collision is not.**

**Fix 2 — all three names as one line of text.** The right *shape*, still wrong: the row is **147px**
and the three names at 12px come to **210px** worst case. It passed 177/177 — because its test asserted
against a **hardcoded row width of 190**. It was checking a geometry the game does not have, and
passing comfortably. A test that cannot read the number it is about has to guess it, and a guess
larger than the truth fails nothing. This is the same shape as the camera lesson: the property that
let the test pass is what let it be wrong.

**Fix 3 — the item name alone, across the whole row. This ships.** The two weapons keep their icons
and cooldown sweeps, the footer already reads `LMB cast / RMB blast / SHIFT blink` every frame, and
the sheet (F1) lists both weapon names in full. The item name is the one that is new, unbounded in
length, and absent from the sheet until you go looking — so it is the one that gets the space. Every
name in the roster now fits **whole at 12px**; the longest has 47px spare.

### An infinite loop, found by a mutation that froze instead of failing

Removing the shrink loop made the test run **stop responding** rather than report a failure. The cause
was the truncation line:

```js
texts[1] = texts[1].slice(0,-1) + '...'   // never gets shorter
```

A hang is a worse defect than a stub, and no name in the roster reaches that branch, so it would have
shipped silently and the next item name added would have found it. It is built from a prefix and
re-measured every step now — monotonic by construction.

### Two things the suite now enforces instead of trusting

- **The row width is published (`BENCH_ROW_W`) and read by the test.** Hardcoding it is how fix 2
  passed.
- **The overflow branch is exercised by a name that overflows.** Every real name fits, so shrink and
  truncation never execute — and a mutation that deleted the shrink still passed 178/178 for exactly
  that reason. There is now a deliberately-too-long name and a just-over-the-line name that must shrink
  rather than be cut.

### The three bugs that were mine

- I mutated a call site and only **described** the mutation, then read the pass as evidence the test
  was sensitive. The first "mutation passed" result in this session was a test never run against
  anything.
- I checked for **overlap** (`gap < 0`) rather than the 6px of clear space the row promises. Setting
  the gap to zero passed, because touching is not overlapping — three words jammed together read as one
  word, which is the same defect more quietly.
- I wrote a test that **re-implemented** the budget formula beside the drawing code instead of calling
  it. It looked like coverage and caught nothing.

All three share one rule: **a check passes if it agrees with whatever it was written against.** Every
one was found by breaking the code on purpose and watching what failed.

## 7. Sweeps and performance — mostly negative, which is worth reporting

**Clean:** array mutation during iteration, raw `Math.random` in shipped code, division by a possibly-zero
expression (`msp>0.05`, `||1`, `d<0.001` all guard), duplicate declarations, dead functions, TODO/HACK.

**One false alarm, correctly diagnosed.** A `` appeared in my own console output. The bytes were
`C2 B7` — a correctly encoded middle dot — and the files contain zero U+FFFD. Same code-page trap as
earlier in the project, checked at byte level rather than assumed.

**Performance: no work warranted.** Measured, not assumed:

| bodies | ms/tick | % of the 4.76ms budget |
|---|---|---|
| 5 | 0.004 | 0.1% |
| 20 | 0.033 | 0.7% |
| 50 | 0.119 | 2.5% |
| 100 | 0.416 | 8.7% |

The friendly-projectile collision walks every enemy per projectile — **O(P×N)** — while the separation
pass already uses a uniform grid. That is a real inefficiency, but `DEPTH_BODY_CAP=28` means the game
never reaches counts where it matters, and at 100 bodies it still costs 8.7% of budget. **Refactoring
the collision path blind would have been optimising something that is not slow.** Recorded as a
documented opportunity instead.

**State at end of session:** JS 178/178 · C# 88/88 · `verify.ps1` exits 0 · LF-only, no BOM, no U+FFFD.
Commits: `5f64c85`, `01342ed`.

---

# Part 1b — the working session, resumed

**38 minutes, 2 commits** (`85d3ae0`, `d5df5c3`). It began by restoring the test harness: the dev
server died with the machine, `python` on this box is a Microsoft Store alias rather than an
interpreter, and the browser tool refuses `file://`. There is now a PowerShell static server in the
temp directory — loopback only, traversal rejected, and bytes served **exactly as on disk** with no
transcoding, because a server that "helpfully" converted CRLF would break an encoding canary the files
are correct on.

Then the plan's first item. **The report above was wrong about it**, which is the headline:

- It claimed no boss health bar existed. **One did** — a 56px sliver, twice a body's, riding the body
  as it walks. The measurement that "proved" its absence called `spawnEnemy(false, …)`, which returns
  a *lunger*. A grep for a function that does not exist and a body that does not exist look identical
  from outside, and I took the first as evidence about the second.
- What was missing was that the bar could not be **read**: no phase markers, so crossing 66% and 33%
  — which changes the move bag from 3 moves to 4 to 7 and adds the wall in phase 2 — was invisible; it
  moved with the boss; and the Warden had **no name field at all**.
- It is now full-width, fixed at the top of the room, with both thresholds notched on it at the same
  constants the tick loop reads, plus name and per-phase colour.

**Three defects found while building it**, none of which the source alone would have shown:

1. **Placed at y 84 — straight across the momentum row.** `MARGIN_Y 14 + HP_H 40 + ROW_H 30 = 84`.
   Only the screenshot showed it. Worse, the test asserted *"above the room"*, which was **true of the
   broken version and false of the fix** — a correct fix would have failed the test. It now asserts the
   collision: where the plates **end** versus where the bar **starts**.
2. **Sized for one room.** `BOSS_BAR_W`/`BOSS_BAR_Y` were baked from `ROOM_W`/`ROOM_TOP` at load time,
   so in the Lab — 1680×760 against a floor's 700×450 — it drew a floor-sized bar in a Lab-sized room.
   A constant that describes a room is wrong the moment there is more than one room. Now
   `bossBarRect()`, per draw.
3. **A fixture trap I had already hit twice.** `noticeTimer>0` makes the tick loop skip the body
   entirely, so a freshly spawned boss ignores several updates and its phase never advances. I read
   that failure as a bug in the phase logic before measuring it; the logic is fine.

There are now **three** documented fixtures that return something usable-looking instead of what you
asked for, and they are a table in `CONVENTIONS.md` because each has cost real time:

| call | gives you | you wanted |
|---|---|---|
| `spawnEnemy(false, …)` | a lunger | the boss — the flag is the **first** argument |
| `spawnEnemy(true, …)` | a body in nowhere | a body pushed into the room by hand |
| a body with `noticeTimer>0` | `continue`, skipped | a body the tick loop acts on |

**Six tests, all mutation-checked.** The most useful asserts the thing that is easy to get wrong: at
exactly the threshold health, the fill's edge **is** the notch's pixel, because both are
`Math.round(w*f)`. Comparing them as fractions gives 0.0006 and looks like a mismatch; it is not one.

**JS 184/184 · C# 88/88 · `verify.ps1` exits 0.**

---

# Part 2 — development plan

## Where the game actually stands

Working, tested, and honestly measured:

- **4 guns** — Bolt (7 dmg, single), Scatter (8 pellets × 2.6), Arcane Beam (0.5 dmg, very fast),
  Voidball (3.4, pierce 3). Plus 2 alts — Hook and Blast.
- **5 body types** — Brunch, Lunger, Shooter, Gunner, Boss.
- **13 items**, 9 passive sigils (uncapped, stackable), 4 active; exactly one active slot, bound to Q.
- **Exponential, unbounded difficulty ladder** — `1 + growth·(e^(rate·steps) − 1)`, rate capped 1.34,
  body count capped 28. Unbroken across floors.
- **Keycard intelligence**, every door opened at INT *n* stays open even if INT drops.
- **Speed + Momentum** as two currencies on one capped budget.
- Build-dependent enemy traits (done).
- **Lab** (`35-devlab.js`), **seed system**, run summary, character sheet, buy list.
- ~15,300 lines of JavaScript, 1,360 lines of C# across 7 ported modules.

## The honest gap analysis

What is **missing at the core**, ranked by how much it costs the player's experience:

### 1. ~~There is no boss health bar.~~ DONE, and the claim was wrong.

**Superseded.** I originally wrote that `bossBar`/`bossHealth`/`drawBossBar` had no hits in `src/`
and that the player "cannot see it move." The grep was accurate; the conclusion was not. The boss
**did** have a health bar — a 56px sliver, twice a regular body's, riding the body as it walks. I
measured its absence by calling `spawnEnemy(false, …)`, which returns a *lunger*, and a grep for a
function that does not exist looks identical to a body that does not exist.

What was actually missing, and is now built (`85d3ae0`): the bar could not be *read*. It showed no
phase markers — so crossing 66% and 33%, which changes the move bag from 3 moves to 4 to 7 and adds
the wall in phase 2, was invisible. It moved with the boss, so there was nothing fixed to watch. And
the Warden had no name field at all. It is now full-width, fixed at the top of the room, with both
thresholds notched on it at the same constants the tick loop uses.

**The lesson is recorded because it is the third time:** `spawnEnemy` takes the boss flag *first*, and
a fixture that returns something usable-looking instead of what you asked for is this project's
recurring debugging cost. There are now three of them, each documented where it is used.

### 2. There is no area concept. Every floor is the same floor.

I grepped for `area`, `AREA`, `actNumber`, `bossFloor`, `eliteFloor` — **no hits anywhere in `src/`**.
`depthFloor()` returns `run.floor` and everything else derives from it. `generateDungeon()` takes no
arguments and produces the same shape every time.

So "4 areas × 3 floors, randomised order, per-floor boss, mini-boss at 13" is not partially built — it
is **not started**, and the ambiguity in that phrase needs your answer rather than my guess. What it
costs right now: floor 30 plays like floor 5 with bigger numbers. A roguelite sold for hundreds of
hours cannot have one texture repeated indefinitely.

### 3. The C# port is 1,360 lines against 15,300. Roughly 9%.

Ported: RNG, balance constants, bodies, body, hit, intercept, frame. Not ported: world generation,
combat, items, enemies/AI, tick loop, view, UI, run flow, art.

This matters more than its size suggests, because **the port is the only path to a shipped `.exe`**, and
the JS and C# are already drifting — the armour rule, the per-shot strength fix, and the beam retune
all exist in one language only.

### 4. Feel work that wants a playtester, not more code.

Scatter recoil, hook rework, Brunch-arc AI. All three want hands on the thing. The Arcane Beam work
this session is the counter-example — it only landed because it was measured at 200px with 25 trials
and a human chose between two options with numbers attached.

### 5. Blocked on a visual decision.

HUD reshape (two columns, top band) and character stat sheet redesign. The standing instruction is to
ask before a HUD decision rather than be confidently wrong. A stale HUD is better than one you did
not choose.

## Recommended order, and the reasoning

**Done — the boss health bar** (`85d3ae0`). Full-width, fixed at the top of the room, both phase
thresholds notched on it from the same constants the tick loop reads, name and phase colour. Five
tests, mutation-checked. It is not yet drawn in the Lab, which is the one loose end: the Lab is how
every other tell gets checked without playing a run, and this one deserves the same treatment.

**Next, and it needs you — the area structure.** Before code. The phrase "4 × 3 + 2" is ambiguous in
two directions at once, and area identity touches floor generation, enemy mix, art palette, music, and
the difficulty ladder's shape. Guessing it unattended risks landing something coherent that is not what
you pictured — and unlike a bar or a recoil, it is expensive to unpick.

**Or, if you would rather I keep going without you — the boss bar in the Lab**, then the C# port in
dependency order: world generation, then combat, then items, then the tick loop, each with parity tests
against the JavaScript. That is what the existing 88 C# checks do for the seven modules already ported,
and the order is forced: nothing visual ships until the tick loop and view exist.

**Then — close the port gap in dependency order.** World generation, then combat, then items, then the
tick loop, each with parity tests against the JavaScript, which is exactly what the existing 88 C#
checks do for the seven modules already ported. The order is forced: nothing visual ships until the
tick loop and view exist.

**Then — feel work, together.** Recoil, hook, Brunch arc, in one playtest session with the numbers on
screen.

**Then — HUD and sheet, together, once you have seen the boss bar in motion.** Redesigning a HUD
before the thing it frames exists is how you end up redesigning it twice.

## What I did not do, and why

- **Did not build the area structure.** Scope and taste, both yours to decide.
- **Did not redesign the HUD or sheet.** Your instruction says ask first; you were away.
- **Did not refactor the O(P×N) projectile loop.** Measured as 8.7% of budget at a body count the game
  forbids. Optimising something that is not slow is how real slowdowns get introduced.
- **Did not add a second active item slot.** One active, bound to Q, is the design.
- **Did not touch the adaptive `Pressure` modifier.** Still `ADAPT`, value 0, still a placeholder —
  your call, and it stays a placeholder until you make it.

## One thing I would flag hardest

**The Arcane Beam's unbuilt floor is 87.2 seconds.** That is a minute and a half of shooting at a boss
with nothing invested in it. It is deliberate — the weapon's base damage is 0.5 precisely so Strength
sigils are worth 3.5× more to it than to any other gun — but it is the kind of number that should be
*chosen* rather than discovered by a test that had been silently capped for two sessions. If the first
real run to floor 10 with a fresh Beam feels like a wall, that is this number, and the fix is a
starting Strength floor rather than a nerf.
