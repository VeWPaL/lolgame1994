# Conventions

Standing decisions for this project. **This file is the memory that survives context loss.** If a
rule matters and it is not written here, it will be forgotten and then re-litigated.

---

# The objective

**A game that is fun, fluid, optimised, and feels great — in the hand and on the screen.**

That is the whole job. Everything below is a means to it, and a rule that stops serving it should be
cut rather than defended. Three consequences worth stating, because they decide arguments:

- **Great feel is a tuning problem before it is a content problem.** Four guns and two right-clicks
  is a complete verb set. Almost every improvement worth making is a number, a curve, a telegraph,
  or a frame of input latency — not a new mechanic.
- **Optimised means measured, not assumed.** A profile is evidence; a hunch is not. The gunner was
  "fixed" once by arithmetic before anyone measured it, and stayed broken through four plausible
  corrections.
- **Fun is not the same as engaging, and not the same as fair.** Difficulty should come from rate and
  density, never from a threat the player cannot read in time. Reaction time is sacred.

---

## Hard constraints

**The game is never commercialised.** Shared with friends, possibly uploaded free. Never sold. This
settles engine licensing permanently — Unity Personal is free under any circumstance we could reach,
so the revenue threshold is irrelevant. Do not re-open this.

**`depths.html` and `src/*.js` are LF-only with no BOM.** The canary is a **U+00B7 MIDDLE DOT**
(bytes `C2 B7`): if it appears preceded by `C3` — a U+00C2 LATIN CAPITAL LETTER A WITH ACUTE — the
file was decoded as UTF-8 and re-encoded as Latin-1, and it is double-encoded. That sequence must
never appear in a source file.

*This file deliberately does not contain the sequence itself.* It used to, because it named the
canary literally, which destroyed the check: a grep then matched the one file that explains it, and
real corruption in this file became indistinguishable from the note. Describe the canary; do not
reproduce it.

The `write` tool emits UTF-8 *with* a BOM and PowerShell's `Set-Content -Encoding UTF8` adds one
too — strip with
`[System.IO.File]::WriteAllText($p, $t, (New-Object System.Text.UTF8Encoding($false)))`.

**`core.autocrlf=false`, `core.eol=lf` in git.** A CRLF pass silently breaks the canary. Do not
"fix" these.

---

## Working rules

**When the DRAWING changes, verify the PIXELS. Never the data.**

Three regressions in one session, all the same shape, all found by the user rather than by the suite:

| what changed | what I checked | what went wrong |
|---|---|---|
| heart plate fill, capped row | `drawHeart` fills | the bar drained **right to left** |
| halo position after that fix | `drawHeart` fills | the pulse sat on an **empty** heart |
| the same halo block | halo drawn once | it was drawn **twice** — duplicate `if` |

Every one passed a data-level assertion, because the DATA was right: fills were computed, slots were
counted, positions were recorded. The mistake was in what got drawn and where. `drawHeart(x, fill)` being
called with sensible arguments proves nothing about which way the bar reads — only sampling the
framebuffer, or asserting on the *relationship* between what is drawn and what is meant ("the lit heart
must be at x 43"), catches that.

So the rule for anything in `70-view.js`:

1. **Assert on meaning, not on calls.** "The halo is on the last LIT heart" is a claim about the screen.
   "The halo is drawn once" is a claim about the code, and a duplicated block satisfies it.
2. **Sample pixels when the question is "does it look right".** `pixelsAtWorld` exists for this. The
   flame needed it. The bar should have needed it.
3. **A direction or an ORDER is a property worth pinning explicitly.** Left-to-right was never written
   down; both bugs were reversals of it, and both were invisible to a suite that never said which way it
   was supposed to go.

**And never write the assertion after the code to match what the code does.** Twice this session I fixed
a drawing bug and then wrote a test asserting the new behaviour — which is how the reversed bar got a
test that passed. The test is written from the *specification* ("empties belong at the right end") and
only then checked against the code; if the code and the specification disagree, the code is wrong.

**Always work prototype graphics into anything the player actually sees.** No grey rectangles, no
"TODO art". If a player will look at it, draw it properly the first time. The controls sheet and the
bug list are the standard: real key caps, a drawn mouse, a wood-and-paper card. A feature that is 95%
done and looks like a spreadsheet reads as broken, not as unfinished.

**A loop that says `startGame()` N times is not N trials. Assert the trials differ.**

`Rnd.fresh` is stubbed to a single `TEST_SEED` at the top of the suite — it has to be, so a bare
`startGame()` is reproducible. So a fuzz loop written as

```js
for (let trial = 0; trial < 140; trial++) { startGame(); /* fight */ }
```

builds the **same dungeon 140 times**. The suite's zero-health fuzz was in exactly that state and had
been passing for a wrong reason: the `FLANK_CURSOR` / `PACK_CURSOR` module counters leaked across
`startGame`, so iteration 87 fought differently from iteration 1, and those accidental differences
were where its deaths came from. Removing the leak — a real fix, so the same seed replayed — turned
the fuzz red, and the honest diagnosis was that the fuzz had only ever been testing one fight.

The lesson generalises past this suite: **variety that a test does not ask for is variety it cannot
claim.** Anything randomised must be seeded explicitly inside the loop, and the loop must assert that
its iterations were actually distinct. It now asserts `worlds.size > 120` over 140 seeds. That check
would have caught this immediately, and it cannot itself pass by accident — a fixture that collapses
to one world is a fixture that has stopped testing anything, and it is *never* the fixture's job to
notice.

Diagnosing it was cheap only because the fix could be switched off again: neutering the reset made the
fuzz go green and the new test go red, which named the cause in one run. When two changes interact,
bisect by disabling the fix, not by reading harder.

**Measure, do not model.** A model of the right answer disagrees with reality about as often as it
agrees. When they disagree, find out which is wrong before explaining the discrepancy. Prefer the
smallest measurement that could possibly be wrong.

**Never let the harness lie to the game.** Fixtures that pin a body, freeze a clock, or re-seed the
RNG produce confident wrong numbers rather than failures. If a test needs the world in an impossible
state, say so in the test and check the arrangement is what the number is about.

**Comments explain why, not what.** The measurement behind a number belongs next to the number. A
comment restating code is noise; a comment recording what was tried and measured is the most
valuable thing in the file.

**Correct the record in the same commit.** When a test fails and the code is right, the expectation
was wrong — say so plainly and fix it. When a comment turns out to state something false, fix the
comment.

**Check the polarity of every number you sample.** Two numbers that both mean "how much of the thing
is left" can count in opposite directions, and a shared formula applied to both will be right about
one and exactly backwards about the other — with no error message, because the arithmetic is valid.
The refill flash got this wrong: `player.cooldown` counts *down* from its maximum, so it is already
the fraction spent, while `blinkCharges` counts *up* to two and is the fraction still held. The
first version inverted both, so the flash was exactly as loud for a player who had lost nothing as
for one who had lost everything. A test asserting only "the flash happened" would have passed it.

**Presentation clocks tick above every early return.** `update()` bails out on a door transition, on
the ready window, and on death. A countdown living in the playing branch never advances during
exactly the transitions the countdown exists to cover. `restoreFX` sits beside `tickFX()` for this
reason and should not be moved down.

**A measurement that swings is not a rate.** If the same settings return 10%, 21% and 92%, each
repetition is a coin flip rather than a sample, and a threshold on twelve of them asserts a
coincidence. Raise the repetition count until it settles into a rate, then assert the rate — and
assert the number, not merely that something happened.

**Geometry that more than one file needs belongs in exactly one place.** The door rectangle was
written out three times: once drawing the frame, once for the boss gate, once for the unlock sweep.
The gate's copy had the bottom wall's y baked into its vertical case, so the E and W portcullises
were drawn 254px below the doors they seal — on the floor, in the corner — while those doors showed
nothing but a padlock. The unlock sweep's copy had the mirror fault, drawing a horizontal band
through the thickness of a vertical wall.

Two copies agreed with each other and with the collision code, so the broken one was the odd one out
of three and readable from no single file. It only appeared when all four sides were drawn at once,
which a layout almost never does, because the feature is one door in a room. One copy now lives in
`doorRect(d,wt)` and is read by all three, and a test asks about **all four sides** rather than the
one that happens to be reachable.

**Test the states your code deliberately runs in.** The refill-flash clock lives above
`if(state!=='playing') return` because the flash fires during a room transition. That also makes it
run on the title screen, where `player` is `undefined` — and 128 tests missed the resulting
per-frame throw because every one of them called `startGame()` first. When a line is placed outside
the state machine on purpose, a test must stand in the state that placement exposes it to.

---

## Architecture

**The engine-independent line.** If a line draws, reads input, or touches the platform, it is not part
of the simulation. Everything else is. This is the line the Unity port forces and the reason the
JavaScript is split the way it is.

**`Depths.Core` targets `netstandard2.1`, never `net9.0`.** Unity 6 runs .NET Standard 2.1; a net9.0
library compiles, tests, and then fails to load in the editor. `LangVersion` is **9.0** for the same
reason — it is what Unity 6 compiles, so `dotnet build` is a real check on shippability. Consequences
already paid for: no file-scoped namespaces, and `Math.Round` is not a compile-time constant.

**`Math.hypot` is not `sqrt(x*x+y*y)`.** V8 scales by exponent; they differ in the last ulp (measured
1.7e-16 relative). Cross-language float comparison must be quantised to 1e-6, never bit-exact. Same
for JavaScript's `Math.round` (floor of x+0.5) versus .NET's half-to-even.

**Parity over assertion.** When porting, generate expected values by running the *original*. A
plausible expectation is worth nothing: the mulberry32 test shipped an invented value, failed, and
proved the port was right all along.

---

## The routine cleanup pass

The user asks for one of these every so often. It is a fixed list, not an invitation to look around,
and every item on it is something that has actually been found this way.

1. **Every number quoted in a comment, re-derived from the code.** This is the item that matters most,
   because a stale figure in a comment is invisible until somebody tunes the thing it describes. The
   worked example, which is what to look for: `tickBlink` said `BLINK_FILL_CLEAR` was 7 (it is 9),
   that a charge came back in 1.1s in a quiet room (0.39s), and that `BLINK_RECHARGE` was 8s (6.5s).
   Three wrong numbers in three lines, none of them load-bearing, all of them confidently wrong. A
   second pass over the *same* comment then quoted only the fill and missed that a cleared room also
   jumps the bar to 50% first, so the honest answer needed both figures (0.39s mid-fight, 0.20s after
   winning). **A number in a comment is a claim about the running game, not about the line below it,
   and a comment describing a derived quantity must quote every path through that derivation.**
2. **Dead constants** — defined, never read.
3. **`Math.random()` in game code** — the tripwire test already covers this; confirm it is still green.
4. **Comments that contradict the code beside them**, in either direction.
5. **Unused locals and unused parameters**, especially `const p=...` left behind after its last read.
6. **Every `null` guard**, and whether the value can actually be `null` (as opposed to `undefined`).
7. **Tests that only compare a value against itself** — the failure that produced the inert Strength
   and Vigor stats. A stat is verified by its *effect*.
8. **File hygiene** — LF only, no BOM, no U+00C2. The encoding check is copy-pasteable below.

---

# PROPOSED - awaiting the user's decision

Everything below is a **proposal, not a rule**. Strike what does not earn its place.

## Code structure

1. **Modifiers are derived, never written back — and additive only.** A stat is recomputed from base
   plus the active item set on every change: `value = min(cap, base + sum(flat) + earned)`. Nothing
   ever does `player.speed *= 1.1` in place. This is *the* classic stacking bug in the genre: after
   twenty items the value has been multiplied in an order nobody can reproduce, removing one item
   does not remove its effect, and the build cannot be explained or saved.

   **No multiplicative stat kind. This is a correction, and it cost a real bug.** The usual form is
   `(base + flat) * product(1 + mult)`, and it has a silent degenerate case: a stat whose base is 0
   multiplied by anything is still 0. Speed's base *is* 0, because the real base is `PLAYER_MOVE` and
   it lives on the player — so a Speed item was equipped, named on the character sheet, and did
   literally nothing. Nothing threw; the bar just never moved. A rule with a silent failure mode is
   worse than a rule that is merely limited, and here the limit costs nothing: a genuinely
   multiplicative effect is a *rate*, and rates belong in named hooks.

2. **Content is data; behaviour is a named hook.** A definition is numbers, flags and hook *names*
   resolved through a table. Never behaviour in the definition. A hundred items must stay a hundred
   lines of JSON, and a mod must be able to add a working item with no code at all.

3. **No `Math.random` in game code, and the tripwire that keeps it that way.** Three named streams
   (`run` / `jitter` / `art`), sorted by what each draw *decides*. The suite asserts the count is
   zero. A rule you cannot check is a rule that decays.

4. **One-way data flow: state → systems → view.** A draw function never mutates simulation state.
   Cheap in JavaScript, and in Unity it is the difference between a presentation layer and a second
   copy of the game that drifts.

5. **Every balance constant is named and carries its measurement.** Not every constant — the ones
   that a player can feel. If a number was tuned by trying it, say so; if it was derived, say from
   what.

6. **One system per commit, green at every one.** A commit that mixes a refactor and a behaviour
   change cannot be bisected, and bisecting is how you find out which of your four "fixes" broke the
   gunner.

7. **A new mechanic is measured before it is balanced.** Build the measurement first, tune second.
   This session's whole value came from doing it the other way round and paying for it. Concretely:
   the first measurement of Momentum against the gunner said the mechanic was catastrophic (100% →
   50% at 200px). It was the *fixture* — a wall filter tighter than the player's own radius. Measure
   the fixture before believing the number.

8. **Anything an overlay needs to receive must be exempted from the input suppressor by name.** The
   suppressor runs in the capture phase on `window`, ahead of every element, so its
   `stopPropagation` is total. It has eaten three separate things: every keystroke aimed at a text
   field, the click that dismisses the pause sheet, and the key that closes it. There is no way to
   write it so new UI is safe by default, which is the real lesson — and the same applies to keys: a
   key that opens an overlay must be in the allowed list or that overlay is a one-way door.

## Game design

1. **Items bend the existing verbs; they do not add a third button.** Four guns plus Blast and Hook
   is tight and complete. Adding a verb changes the hand position, the muscle memory and the
   difficulty budget all at once. The interesting space is *which* body you shoot first, *when* you
   spend a charge, *where* you stand — not another key. *This is the proposal I am least sure of and
   most want argued about.*

2. **Every item must change a decision, not a number you never think about.** +5% damage is invisible.
   An item that makes the room's problem different is a build. If a player cannot describe what an
   item makes them *do* differently, it is not an item — it is tax.

3. **Tension beats stacking.** The real reason twenty items feel like two hundred is that they
    exclude each other: strong with Bolt, poor with Scatter; a build that wants to stand still
    against one that wants to blink. Additive-only items are a slider, and a slider is played once.

4. **Rarity is a cost, not a size.** A legendary should be build-defining and slightly awkward.
    Otherwise "rarer" only means "a bigger number", and the roll stops mattering.

5. **Nothing is added to the pause menu unless a decision depends on it.** Six stats and a build
    list is a lot of screen. Every element there is a thing the player must learn to read.

6. **Difficulty comes from rate and density, never from an unreadable threat.** Keep human reaction
    time intact. The gunner at 200px is 100% lethal and that is *correct* — it is a commitment you
    must respect, not a bullet to dodge.

---

## The Warden - the boss, and what a boss is allowed to do

It was 50*TOUGH = 67.5 HP, no ranged kit at all (the tick explicitly excludes the boss from firing),
and it walked at the player. Measured against the four guns, it died in **2.20s / 2.66s / 3.81s /
5.24s**. That is not a fight.

**Built out of the existing vocabulary, not a new one.** Every move is one the player has already
learned to read from a smaller body:

| move | what it is |
|---|---|
| **VOLLEY** | three committed shells, gunner's muzzle tell, re-aimed between each |
| **SWEEP** | a lunger lunge, from a body four times the mass |
| **WALL** | the Brunch formation, called as cover for the boss |

The wall is the one new idea, and it is new *because* the player has just learned that Brunch eat
shells. The fight where that matters most is the boss, and the boss gets its own wall to stand
behind — the cover the player has been building is now something the fight takes away.

**Two phase thresholds, and each opens a MOVE rather than raising a number**, so a phase is a
different fight rather than the same one with different dice. They are fractions of max HP, not
absolute, because the depth ladder scales body health.

**343*TOUGH = 463.05 HP, at `ARMOUR`.** Re-measured whenever a weapon changes; on the starting build
at 400px, mean of 25 trials: 22.2s Scatter, 29.1s Voidball, 29.9s Bolt, 12.5s Arcane Beam, a spread
of x2.39. The spread is left alone on purpose, the slow gun *should* take longer, and flattening it
would mean tuning to the median weapon and telling the player their choice does not matter.

**HP was 520*TOUGH until the armour rule was unified.** The boss used to take *full* damage where
every other body took 66%, so its effective health was 19x a lunger's rather than the 29x its HP
implied — and because the ladder multiplies health without limit, that gap *grew with depth* rather
than staying a property of the body. HP absorbed the factor so the fight length held.

**The suite asserts the boss fight as a SPREAD (x4), not as a window in seconds.** It used to assert
18-70s while its loop could only reach 42.9s, so three of four guns were reporting the loop cap as a
measurement and the ceiling could never fail. Absolute seconds go stale every time a weapon is
tuned; a ratio between the fastest and slowest gun is the actual design claim. The floor of that
spread is the Arcane Beam with zero Strength — 87.2s — which is the price of a weapon whose base
damage is low so that Strength sigils are worth 3.5x more to it than to any other gun.

### The number that decides whether any of it works

| player | length | moves | phases | wall | hits | outcome |
|---|---|---|---|---|---|---|
| **reads the tells** | 46.0s | sweep, volley, wall | P2@16.0s P3@30.7s | 5 | **1** | killed it |
| **reads nothing** | 33.9s | sweep, volley | P2@16.8s P3@31.0s | 0 | **6** | DIED |

That gap **is** the boss. As first written it fired every 0.55–0.85s for 2.88 a shell — 46 shells in
fifteen seconds against an 8 HP player, so the player had to dodge 94% of what was fired, and the
reader and the non-reader finished on the *same* number of hits. The tells were doing nothing, which
is the one thing a boss may not be.

---

## A Brunch pack is a WALL, not a crowd

Every Brunch used to steer straight at the player, so a pack of eight arrived as a loose mob you
could walk into the middle of and pick off one at a time. That made the most numerous enemy in the
game the least interesting one, and wasted the thing a pack already is: a body of bodies.

**The mechanism is the frame, not the target.** A Brunch now steers at a **slot** — a position in
the pack's own frame, across the approach vector and back along it — so the pack is a rigid body that
turns to face the player as one thing. Steering at the player instead produces a crowd every time,
because eight bodies converging on one point from eight directions *is* a crowd by geometry.

Two ranks, offset by half a column, so a shot that finds the gap in the front does not find a body
behind it. A single rank is a wall you shoot down lengthwise.

**Both spacings sit above `r+r` on purpose** (19 and 17 against a 16px overlap threshold).
`bounceEnemies` is a hard positional push, not a force, so it fires whenever two bodies overlap and
it will fight a slot asking for less than 16px — the wall shivers in place forever and reads as a bug
rather than a formation. Letting separation have nothing to do is what lets the shape hold.

The pack centroid is computed **once per tick**, before the body loop. The expensive mistake is the
subtle one: if each body recomputed the centroid *after* the earlier bodies had moved, the wall would
rotate a little every pass and the formation would creep.

### Measured — the difference between a wall and a crowd is that a wall stops growing

| tick | live | wall across | wall depth | centroid to player |
|---|---|---|---|---|
| 50 | 8 | 56px | 30px | 526px |
| 150 | 8 | 48px | 22px | 425px |
| 250 | 8 | 48px | 22px | 304px |
| 350 | 8 | 47px | 23px | 178px |
| 450 | 8 | 52px | 27px | 53px |
| 500 | 8 | 83px | 57px | 69px ← contact |
| 600 | 6 | 58px | 25px | 40px |

It breaks only at contact, which is the existing Brunch rule — a body spends itself when it reaches
you — and is not what this change touches.

### Fairness, measured rather than assumed

- **Reaction time** 2.1s–3.0s to cross the room. A human reacts in about 0.25s, so there is an order
  of magnitude of margin. Density and rate are allowed to punish; arriving faster than a reaction is
  a coin toss, not a difficulty.
- **Flanking** a player walking the long way round a pack of 8 loses **0 bodies, 0 damage**. If that
  stops being true, a wall has exactly one answer and it is a blink — which turns a spatial problem
  into a resource problem.

Both are in the suite, because "it looks like a wall" is a claim about a picture and neither number
is visible in a picture.

---

## The map is a branching tree, and roles go to its ends

The generator used to lay out three named runs by hand: a silver spine of three fights, an arm off its
first room for the upgrade, a gold run off its second, boss hanging off the end of that. It produced
the **same 15-room shape every time** — three corridors, a fixed junction at each fork, a route you
could learn in one run and never think about again. A dungeon you can learn in one run is a corridor
with monsters in it.

What replaced it grows a **tree** and then decides what the ends are *for*. Two trunks leave the start
in different directions, each cuts a side run partway out, the trunk tips become the two rooms worth
fighting towards, and the branch ends are where the keys go. Which trunk is the boss and which is the
upgrade is a coin toss, so "left is the boss" is not learnable.

**The keys go in branch ends rather than on the trunk.** A key on the trunk is a toll you pay by
walking forward, and a toll that is unavoidable is not a decision. At the end of a side run the player
chooses between two ways to spend a floor: go deep on a trunk, or turn off early and come back with a
key. Both are correct play and they cost different amounts of the thing the player cares about.

**Endpoints, not coordinates.** A room is an endpoint if it has exactly one door. Nothing in the
generator knows where anything is on the grid; it asks the graph what its dead ends are.

### Measured, 400 dungeons

| | |
|---|---|
| total rooms | 16: 134 · 18: 178 · 20: 87 · 21: 1 |
| fight rooms | 12: 134 · 14: 178 · 16: 87 · 17: 1 |
| distinct layouts | **395 of 400** |
| unreachable required rooms | **0 of 500** |
| boss room a clean single-door endpoint | **500 of 500** |

### The bug the tree caught

The first version forked from `path[path.length-1]` without excluding the last step, so a branch
could be cut from a **trunk tip** — and a tip is the room that becomes the boss. The result put the
gold key *behind the locked door the gold key opens*: **113 of 300 dungeons dead on arrival.**

```
 K  o  .  .  .  .  .        K silver key      G gold key
 o  .  .  .  .  .  .        I item room       B boss
 o  .  .  .  .  .  .        S start           ? secret
 I  o  o  S  .  .  .
 .  .  .  o  ?  .  .
 .  o  B  o  .  .  .
 G  o  .  .  .  .  .      <- the branch runs east out of the BOSS room
```

A dead run is not a hard run, so this was correctness, not tuning. It is now impossible by
construction: `growForked` refuses to fork at the last step and `tryBuild` draws its fork index from
interior steps only.

`trunkLen` 4–5 and `forkLen` 3–4, raised from 3–4 and 2–3. The first tree put a floor at **8 fight
rooms** in the worst case, and eight is not a floor — it is a corridor with two decisions in it.

---

### Verified end to end, on the real generator

Twenty dungeons, walked start to the way down: silver key, item gate, gold key, boss gate, the
boss, the way out, floor 2. **20 of 20 succeeded**, and the health in the bank carried down (6hp in,
6hp out) on every one.

Getting that number took six versions of the harness and every single failure was the harness, not
the game. In order: the pathfinder walked through locked doors, so the bot routed toward the gold key
via the boss room and stood in a doorway; it froze on arrival at an objective room because the key
spawns at the room centre and it had stopped moving; it re-triggered a room transition without
waiting out FADE_OUT; it cleared the boss inside the arrival fade, where `update()` correctly does no
gameplay at all; it never cleared the rooms it walked into, which `doorOpen()` correctly refuses; it
walked to the boss before collecting the silver key, which does not open the item gate; and finally
its pickup helper deleted the way out of the boss room and then reported that the boss had not
opened one.

Worth writing down for the obvious reason: at no point in those six attempts was the game wrong. The
result that mattered took a harness that behaves like the suite's own `walkTo`, because that one
already knew about the lock, the transition, the fade and the clear.

---

## Floors: the run descends, and difficulty reads depth and nothing else

The boss dies, the way out appears, and walking into it takes you **down**. A run now ends in exactly
one way, which is dying, and the weapon, the items, every stat and the health in the bank all carry
down. Nothing about the build is rebuilt.

**The rule the whole system exists to enforce:** difficulty is a function of the floor number and of
nothing else. Not health, not the build, not item count, not how the last floor went. A no-item run
to floor 5 meets the fight a five-item run to floor 5 meets.

The consequence that actually costs something: the game cannot quietly reward a good run by easing
off, so every good-run reward has to come from somewhere the ladder cannot reach — a better weapon, a
tighter route, more health in the bank when it matters.

### The ladder — linear, in three places

| lever | per floor | why this one |
|---|---|---|
| `DEPTH_HP_STEP` | +30% body HP | a fight takes longer |
| `DEPTH_BODY_STEP` | +0.34 of a body | there is less space to answer in |
| `DEPTH_RATE_STEP` | +16% cadence | bodies answer sooner |
| `DEPTH_PACK_STEP` | +3.5% pack chance, capped 85% | the *shape* of a room changes, not just its size |

Scaling only HP would make deep floors slow and empty, which is a worse game than either of the other
two. `depthBodies()` adds to the roll rather than moving the roll itself, so a deeper floor is a
fuller room of **the same fight** — the shapes a player learns on floor 1 are still the shapes on
floor 12.

### The discipline, and why these are functions

Nothing in the ladder may read the player. It is very easy to add a small mercy — a hair less HP
when the player is hurting — and it would feel like good design and it would quietly destroy the
thing the system is for, because **a difficulty that reads the player is a difficulty that measures
the player.**

The suite asserts it directly: a player stripped bare and a player carrying every stat at its cap,
on the same floor, must produce byte-identical enemy stats.

### Floor seeds are a pure function of (root, floor)

`Rnd.floorSeed(root, N)`. Deliberately **not** chained from the previous floor — chaining would make
floor 3 depend on how many draws floor 2 happened to make, so two players typing the same seven
characters would get different dungeons and the seed would be decorative. Floor 1 keeps the root
exactly: the seed a player typed should be the dungeon they get.

### Measured

```
floor 1  lunger 24.30 hp   rooms average 5.45 bodies
floor 2  lunger 31.59 hp (1.30x)   5.65 bodies
floor 3  lunger 38.88 hp (1.60x)   5.50 bodies
floor 4  lunger 46.17 hp (1.90x)   7.58 bodies
floor 5  lunger 53.46 hp (2.20x)   7.38 bodies
floor 6  lunger 60.75 hp (2.50x)   7.35 bodies
```

Exactly linear, seed different every floor, build carried every time, room count stable at 15.

### The projection, stated plainly

30% **compounds**, and nothing on the player's side scales with it:

| floor | body HP | TTK vs floor 1 |
|---|---|---|
| 5 | 2.20x | x2.2 |
| 10 | 3.70x | x3.7 |
| 20 | 6.70x | x6.7 |
| 30 | 9.70x | a single body is a siege |

A no-item run meets that wall, because the rule says it does. This is the right shape for a roguelite
and it was the explicit instruction — but 0.30 is a wall by floor 20. **The step is one constant.**
If a hundred floors is the target, sub-linear HP with a separate asymptote will hold up far longer
than compounding, and the rate lever compounds on top of it.

### Presentation

- **Depth plate** in the HUD, in the same wood-and-inset vocabulary as health, key and blink — a
  struck numeral plus a tally of notches (eight, then `+N`). A bare number beside the plates reads
  as a leftover debug readout.
- **Descent banner** over the room fade, during the one beat in a run where nothing is trying to kill
  you. States the three levers as ratios. `FADE_DESCEND` is the single source of that duration —
  read by `descend()`, by `drawDescent()` and by `update()`, because three numbers for one duration is
  how a banner ends up outliving the fade it was drawn on.

### Records

`deepest floor` is the headline, on the title screen and the summary. `rooms explored` is now
**per-floor**: summing across floors produces a number that grows without meaning anything, since
floor 12 alone has more rooms than floor 1. `run.ticks` deliberately does not reset — that is the run
— and `floorTicks` is the per-floor clock, printed beside it.

---

## The blink grace — forgiveness, not invulnerability

The user asked for a 0.1s window to press blink and avoid a hit. The game already grants **0.40s**
after a blink (`BLINK_IFRAMES` 36 ticks + `DASH_TRAIL` 48 ticks, 84 together, set *after* the
teleport), so 0.1s would have been strictly inside immunity already in force and would have changed
nothing at all. Those are ticks, not the `0.17` and `0.23` in the `sec()` calls — `sec()` rounds to
whole ticks, so the real values are 0.1714s and 0.2286s, and their sum happens to be 0.40 either
way, which is precisely why the stale figures survived a cleanup pass. Measured
what the window was actually being asked to cover, by firing a shell to arrive N ticks after a
blink:

```
i-frames cover to        84t (0.40s)
a slow shell still connected up to  196t (0.93s)
```

So the real complaint was never *blinked too late*. It was: dodged the first thing, then took the
second while the meter was still down. `BLINK_GRACE` is **0.6s**, and the measured edge of
forgiveness is 0.62–0.66s across 1.0–2.0 px/tick shells — it does not depend on projectile speed,
because the window is measured in time and a shell is judged on when it *arrives*.

**The distinction is the entire safety argument.** The grace is not a longer i-frame window. The hit
is allowed to land and is then handed back:

- knockback stays — the shell arrived, the body still feels it
- the Momentum meter still takes it — the player spent a charge and did not win the trade
- **no post-hit i-frames are granted.** Granting `IFRAMES` there would have made one blink into
  0.6s of grace *plus* a full second of immunity the moment it was spent. That is a second health
  bar, and it is exactly how the grace would have made a room of gunners toothless.
- one hit only, spent on use. `graceSpent` resets per blink, never per room.

Measured: **two shells 40 ticks apart after a blink cost 1.80 hp, identical to no blink at all.**
The grace buys the first; the second still has to be answered.

**Known gap:** the 0.93s tail is not covered. Closing it means a longer window, or per-lunge rather
than per-blink — a different design, not a tuning number.

## The Scatter is buckshot, and the wave was a spawn-line bug

The report was that the Scatter felt like a wave shot. It was one, and the cause was one line:

```js
off = (i-(count-1)/2)*spread      // eight evenly spaced rays from ONE origin
```

That is a diffraction pattern, not shot, and because the divergence was purely **angular** it opened
into a V that widened with range — the same shape as the Arcane Beam, which is why the two read as
the same weapon. Three numbers now replace the one:

| field | value | what it does |
|---|---|---|
| `muzzleJitter` | 5px | random muzzle **position**. Real shot leaves the barrel from across the bore, not a point. This term is constant with range, which is the whole mechanism: a column cannot become a cone. |
| `pelletAngle` | 0.008 rad | random aim error per pellet, small enough to stay a column. 3.6px at the far corner. |
| `pelletSpeedVar` | 0.25 | per-pellet speed variance. **The chaos.** |

Measured, old vs new, per single 8-pellet volley:

| range | old width | new width | old evenness | new evenness |
|---|---|---|---|---|
| 60px | 19px | 8px | 1.01 | 2.61 |
| 120px | 38px | 8px | 1.01 | 2.60 |
| 200px | 64px | 8px | 1.01 | 2.59 |
| 300px | 95px | 9px | 1.01 | 2.54 |
| 450px | 143px | 10px | 1.01 | 2.49 |

**Evenness** is max gap / mean gap across a volley: 1.00 means all seven gaps identical, which is a
ruler. The old cone is 1.00 *by construction* — that is the control, not a coincidence. Along the
aim the new pattern spans 112px against the old 77px, and that longitudinal spread is the
de-synchronisation.

**A per-tick drag was written first and removed.** The reasoning was that it would let fast pellets
fall behind slow ones and open the column with range. It cannot: a drag shared by every pellet
scales all their velocities by one factor, so the ratio between fastest and slowest is exactly what
it was at the muzzle, forever. It amplifies nothing and only slows the shot down — paying range for
a cosmetic that speed spread already provides for free (0.6px/tick of difference, 50px apart by
200px out). The measurement is in the code so it does not get tried again.

**Damage at the muzzle is unchanged**: 8 × 2.60 = 20.80, exactly as before — the pattern work was a
redistribution of where the damage lands, not a buff. The old `spread*(count-1)` cone assertions were
deleted rather than adjusted: they measured an angle that no longer decides anything and would have
kept passing while describing a weapon that is not in the game.

### The falloff band, and why 175 was a quarter of a room

The pattern above is a **column** 8px wide, and the damage band it sits in was `fNear 80, fFar 300`.
Those two facts contradicted each other. A gun that holds a column this tight for its whole length
was being charged for accuracy at 80px — already down to **76%** by 175px — so there was no distance
at which the Scatter was simply good. Inside a buckshot gun's range is exactly where it must not be
trading damage.

`fNear` **is** the radius of the effective range, because `falloffMult` is already radial (it measures
`hypot` from the shot origin), so no new machinery was needed.

**A room is 700 × 450.** Measured over five seeds, every room in every type is exactly 700 wide —
there is no variance to average. So a third is **233**, a half is **350**, and the band is stated as
fractions of the room in the test rather than as pixel counts, so it cannot rot if the room changes.

| | original | 175px pass | 233px pass | now |
|---|---|---|---|---|
| `fNear` | 80 | 175 | 233 | **180** — a quarter of a room |
| `fFar` | 300 | 400 | 350 | **320** |
| `fMin` | 0.45 | 0.45 | 0.30 | **0.22** |

The 175px band was wrong in a way that only showed up when the room was measured rather than
assumed: 175 is a **quarter** of a 700px room, not the half the comment beside it claimed. The gun
one-shot every normal body out to 350px — half a room — and the gunner, shooter and Brunch all the way
to the far wall, because even at the 0.45 floor a volley was worth more than their health. **A shotgun
that clears a room from the far corner has no range, and no room in which another gun is right.**

233 with a 0.30 floor fixed that, and was then reported as **still too easy at medium range**: a lunger
at 350px — a body in the middle of the left of the room, the player in the middle of the right — still
died in **2 volleys / 2.2 seconds**. So the threshold came back **down** to a quarter of a room and the
floor down to 0.22.

Measured, 8 real volleys, player backed into a corner (584px of usable range):

| body | 180px | 233px | 280px | 300px | 350px | 400px | 450px | 500px |
|---|---|---|---|---|---|---|---|---|
| lunger 20.25 | 1 | 2 | 2 | 2 | **3** | 3 | 3 | 4 |
| gunner 10.80 | 1 | 1 | 1 | 1 | 3 | 4 | 5 | 5 |
| shooter 7.56 | 1 | 1 | 1 | 1 | 2 | 2 | 2 | 3 |
| brunch 2.70 | 1 | 1 | 1 | 1 | 1 | 1 | 2 | 2 |

### A lunger walks toward you while the volley is in the air

The most important thing found while tuning this, and it is invisible to any assertion written against
the damage table. A lunger **closes 30-odd px during a volley's flight** — 350px when fired, about
318px when the last pellet arrives — and `falloffMult` is read **at the moment of the hit**, from the
projectile's own origin to where it touches. So the eight pellets of one volley are taxed at eight
different distances, and the multipliers run:

```
hit 1  dmg 1.19  range 350  mult 0.302
hit 4  dmg 1.37  range 337  mult 0.376
hit 8  dmg 1.68  range 318  mult 0.491
```

The static table says a lunger takes **5** volleys at 350px under this band. It takes **3**. That is
not a rounding detail — it is the difference between "the gun is weak at range" and "the gun is fine",
and no table can see it. **Fairness claims about a gun have to be measured in the simulation.**

### A range assertion is only useful if the value cannot satisfy it

`fMin` 0.22 → 0.30 was a mutation that **survived**, twice, for two separate reasons:

1. The test called `noCharacter()`, which strips the Wyrd's +3 Strength. Strength is added once per
   shot, so stripping it takes 34.2 raw to 31.2 — a 9% loss that is exactly enough to drop a lunger
   from 3 volleys back to 2. **The test was measuring a character nobody plays.**
2. The assertion read `v >= 2 && v <= 3.5`. At 350px the two floors give 2 and 3, and **2.0 satisfies
   both**. A bracket wide enough to sound reasonable pins nothing.

The assertion now names the exact case: **3 volleys at 350px, and not fewer.** The fix that was asked
for *is* the difference between 2 and 3 at that one distance. Trials are also reseeded per iteration —
without that, six trials replay one identical fight and a reported mean of 3.00 is a single sample.

One volley to a third of the room; two volleys at half a room against everything but the Brunch; and it
keeps degrading past that. The Brunch stays one pellet at close range, which is deliberate and
unchanged — they are 2.70 hp with no armour and a shotgun should not have to work for them.

**A 350px shot cannot be placed from the middle of the room.** From `MIDX` 400 the furthest legal
target is 274px away, because spawns are clamped inside `SPAWN_MARGIN` 76. Any fixture that measures
range has to back the player into a corner to reach the far half, or it silently measures 328px and
reports the wrong answer. That one cost a wrong table here.

### The damage had to rise to 3.9, and 3.9 is the whole trick

The contract is **eight pellets kill a lunger and seven do not**. With `ARMOUR` 0.66 that is a narrow
window, and it is the reason the number looks arbitrary:

| dmg | 8 pellets land | 7 pellets land | contract |
|---|---|---|---|
| 3.6 | 19.01 | 16.63 | eight do not kill |
| **3.835** | **20.25** | 17.72 | exact knife-edge, zero margin |
| **3.9** | **20.59** | 18.02 | **holds, by 0.34** |
| 4.5 | 23.76 | 20.79 | seven kill — requirement *deleted*, not met |

3.835 is the mathematical minimum and 3.9 is the smallest tidy value above it. 4.5 is the trap worth
naming: it satisfies "eight pellets kill a lunger" on its own and quietly removes the reason for the
rule. **Both halves have to be asserted** or the test passes for the wrong weapon.

This was forced, not chosen. At 2.6 the gun could not kill a lunger at *any* cone angle, at any range,
including point blank — 0 kills in 24 trials from 0.92° to 22.92°. A shotgun whose pattern has to be
tuned to reach its own damage threshold is not a shotgun with a range, it is a shotgun with a puzzle.
Brunch are unaffected: one pellet is 3.9 against 2.70hp and no armour.

---

## `pelletAngle` is not what opens the pattern. `fNear` is.

This is the opposite of what the cone work assumed, and it is worth writing down because the sweep
was run specifically to find a cone angle and the answer was that **there is nothing there to tune**.

Sweeping `pelletAngle` against pellets landed on a lunger, 12 trials each:

| `pelletAngle` | cone | landed @233px | landed @350px |
|---|---|---|---|
| 0.008 (was) | 0.92° | 8.00 | 5.50 |
| 0.03 | 3.44° | 8.00 | 5.40 |
| 0.05 | 5.73° | 8.00 | 4.70 |
| 0.07 | 8.02° | 8.00 | 4.70 |

A **nine-fold** widening of the cone moves landed pellets by 0.8. Sweeping `pelletSpeedVar` over
0.05 → 0.45 moves them by 0.35. Neither knob decides anything, and the reason is visible in a single
traced volley at 350px:

```
tick  alive  spreadY
    0     8    351..358
  100     8    351..360
  120     5    352..360
```

The column is **9px wide** at 350px, against a lunger 28px across. Every pellet lands; the pattern is
simply narrower than the target. Instrumenting the damage per tick confirms it — 8 damage events of
1.82–2.01 each, where full falloff would be 2.82:

```
raw/shot 34.2   per pellet raw 4.275   after armour 2.822
t108 d1.82   t109 d1.80   t115 d1.83   t122 d1.88   t133 d1.94   t139 d1.93   t147 d2.01
total 15.21 = about 5.39 pellets worth
```

**Eight pellets hit. They are just taxed by range.** So the weapon's range identity is created entirely
by the falloff band, and `pelletAngle` is free to stay a small column — which is what makes it read as
shot rather than as a wave. Widening it would only blur the pattern without changing what it hits.

Kill boundary under the current band (`fNear 233, fFar 350, fMin 0.30`), all 8 pellets landing:

| range | mult | 8-pellet total on a lunger | kills |
|---|---|---|---|
| 233 | 1.000 | 22.57 | **yes** |
| 300 | 0.599 | 13.52 | no |
| 350 | 0.300 | 6.77 | no |

---

## Brunch packs are no longer a one-volley wipe, and that is a change of identity

The pack sizes are 4–8. Measured, gun aimed **at** the knot (best case, not a sweep):

| pack | @175px left standing | @350px left standing |
|---|---|---|
| 4 | 1 | 2 |
| 6 | 2 | 4 |
| 8 | 4 | 6 |

At dmg 2.6 the volley was worth 20.8 raw and a whole pack died to it. At 3.9 it is worth 31.2, but the
pack is spread over ~40px and the column is 9px wide, so most pellets pass between bodies. The
Scatter now clears a knot in **two or three volleys** rather than one. That is the honest buckshot
behaviour and it is worth knowing it changed — a room where the Scatter used to erase a pack
instantly now takes a beat.

---

## `startGame()` does not start the game

Three separate measurements in one session returned **zero damage at every range**, and the obvious
reading — that friendly projectiles do not collide — was wrong. `startGame()` leaves `state` on the
title; `update()` then runs the title branch and simulates nothing. A fixture needs the room as well:

```js
startGame();
var n=Object.values(rooms).filter(q=>q.type==='normal')[0];
enterRoom(n.x,n.y,'W'); readyT=0; fadeT=0; roomFade=0;   // <- this is the part that matters
```

The tell is that a projectile placed **by hand** directly on a body does damage, while the same
projectile fired through `fireWeapon` does not. Collision cannot be the suspect if the hand-placed
one lands.

Two more things in the same family, all of which produced confident wrong numbers:

- **`mouse` is screen space.** `mouse.x=e.x` is wrong; the suite's own helper is
  `pointAt(x,y)`, which writes `x-cam.x`. `mouseWorld()` converts back with `screenToWorld`.
- **`Stats.reset()` does not clear `value`.** It zeroes `flat` and `earned` and then re-applies the
  class, so `Stats.value('strength')` still reads the **class base** — 3 for the Wyrd. Strength is not
  leftover loot and cannot be zeroed from a fixture; a Wyrd always carries +3, and a test that
  forgets this measures a build that does not exist.
- **Deleting pellets to count shots destroys the measurement.** Counting live pellets and clearing
  them mid-flight prevents the hit you are trying to count. Count shots from `player.cooldown`
  going *up*, and let the pellets land.

**Discrete shot counts absorb HP changes.** A 16.6% HP cut left the Bolt's time-to-kill at exactly
0.0%, and that was not a broken fixture: 24.30/6.60 = 3.68 → 4 shots and 20.25/6.60 = 3.07 → 4 shots.
Both round up. "An HP cut changed nothing" is a real outcome, not always evidence of a bad harness.

---

## The lunger was 24.30 HP and is now 20.25, because a test could not be written

`hp:18*TOUGH` → `hp:15*TOUGH`, with no other body touched. `TOUGH` scales every body, so moving it
was not an option — the lunger's own factor is the only thing that changes.

The reason is worth more than the number. The task was to find the cone angle at which the Scatter
stops reliably killing a lunger at half a room. **That measurement was impossible**, and the reason
is a real property of the weapon rather than a broken fixture:

- 8 pellets × 2.6 = **20.80** raw, so a lunger under 20.8 raw could in principle be one-shot.
- `ARMOUR` is **0.66**, so 8 pellets actually land **13.73**. A lunger needs **~30.7 raw** to die in
  one — `dmg` 3.835 per pellet — which is not a value anyone would ship.
- So the Scatter **cannot** one-shot a lunger at *any* angle, including point blank. Kill rate was
  **0/24 at every angle from 0.92° to 22.92°**. The threshold the task asked for could not exist.

A target the weapon can never one-shot makes the weapon's defining property untestable. That, and not
"the gun feels too strong", is what moved the number. Armour is why the margin is thin, and why the
new test asserts "inside one Scatter shot" rather than "one shot kills" — a claim that would break
on the next buff to either number.

TTK across the roster, live, at 150px, Wyrd base +3 and `ARMOUR` applied. Shots to kill:

| body | hp | Bolt | Scatter | Arcane Beam | Voidball |
|---|---|---|---|---|---|
| lunger | **20.25** | 4 | 2 | 11 | 5 |
| gunner | 10.80 | 2 | 1 | 8 | 3 |
| shooter | 7.56 | 3 | 1 | 6 | 2 |
| brunch | 2.70 | 1 | 1 | 4 | 1 |

Two things fall out. The Scatter already one-shots **three of the four** normal bodies — the gunner
crosses 1.0 first as the falloff bites past 175px, which is what makes *it* the right target for the
cone sweep rather than the lunger. And the lunger cut moves the Beam 12→11 and the Voidball 6→5 while
leaving the Bolt and Scatter untouched, because discrete shot counts absorb a 16.6% HP cut.

### A shape test cannot see a value change

Both edits went in with the suite **195/195 green**, which is exactly the problem. The existing test

```js
ok(ratio>1.6, ...); ok(ttk(250)<7, ...); ok(ttk(wp.fFar)<11, ...);
```

is a **shape** test. Feeding it the old band (`fNear 80, fFar 300`, hp 24.30) and the new one
(`fNear 175, fFar 400`, hp 20.25) gives the same ratio 2.222, and both pass all three assertions —
because a band starting at 80 and one starting at 175 are both "a usable falloff band". A suite of
only shape tests cannot distinguish a deliberate retune from no retune at all.

So each value now has an assertion that states **why** it is that value. Mutation-checked, each
revert caught by exactly the intended line:

| mutation | caught by |
|---|---|
| Scatter `fNear` 175 → 80 | `the Scatter starts losing damage inside its own effective circle` |
| lunger 20.25 → 24.30 | `the lunger health factor moved: expected 15, got 18` |
| Scatter `dmg` 2.6 → 2.0 | `a lunger at 20.25 cannot be killed by one Scatter shot (16 raw)` |

**Write the descending form of the falloff.** `falloffMult` computes `1-(1-fMin)*min(1,t)`. The older
table tests use the ascending `fMin+(1-fMin)*max(0,1-...)` form for the same curve and both are
correct — but writing the wrong one produced `1.4278` at the muzzle instead of `1`, which is the tell
that a test has stopped describing the thing it claims to check.

### A test I wrote passed against a mutation it should have caught

The seed-replay check fingerprints a played fight: where every body ended up, its flank angle, and its
pack id. Its first version had only the first two, because those are what the bug report named. I
added `packId` on the reasoning that "both cursors should be in the fingerprint" — and then nearly
left it there as a comment-level improvement.

The mutation check says whether it was load-bearing:

| | `PACK_CURSOR` leaked | caught? |
|---|---|---|
| fingerprint without `packId` | yes | **no — 200/200 green** |
| fingerprint with `packId` | yes | yes, by the intended assertion |

So the original fingerprint was blind to half the bug. It would have been satisfied by a
`resetRunCursors` that reset the flank walk and silently left the pack-id counter running, which is
exactly the half-revert a well-meaning "let me tidy this up" commit produces. It caught the mutation
I thought about and not the one it actually had to catch.

The general form, which is the third time this has happened here (see the boss-phase tally and the
Brunch pack generator): **a test written about one symptom covers one symptom.** Two cursors were the
cause; one of them was the story. Enumerate the *cause* — here, "every module counter a run owns" —
and assert each one, then confirm by mutating each one separately. Four mutations, each expected to
fail on a different assertion:

| mutation | fails on |
|---|---|
| `PACK_CURSOR` not reset | replay fingerprint |
| `FLANK_CURSOR` not reset | replay fingerprint |
| `descend()` also resets | pack ids unique within a run |
| fuzz back to bare `startGame()` | `worlds.size > 120` — reports "got only 1 distinct dungeons" |

The fourth is the useful one, because it is the only one that catches a fixture quietly ceasing to be
a test. All four are asserted to fail; none of them is a claim about what the code looks like.

---

## Parked, with the measurements that produced it

**AZERTY, and why the obvious fix is not the fix.** Movement reads `e.key`, which is the CHARACTER the
layout produces rather than the key's position — so on a French keyboard the key where QWERTY keeps A
produces `q`, and since **Q is the item button here**, a player pressing "left" spends their item while
"up" does nothing. Reading `e.code` instead would fix exactly that in about four lines.

It is deliberately **not** done. A keybind system is coming later and will make movement remappable for
the same reason, so a half-migration now would mean writing the physical-key path and then replacing it,
and shipping a state where `A` moves left on one layout and does nothing on another. Accessibility is a
real gap and this is a fair note to have raised; the answer is the keybind screen, not a patch in the
input layer. Left as-is deliberately, not missed.

---
layout produces rather than the key's position — so on a French keyboard the key where QWERTY keeps A
produces `q`, and since **Q is the item button here**, a player pressing "left" spends their item while
"up" does nothing. Reading `e.code` instead would fix exactly that in about four lines: `KeyA` is that
key on every layout, and the item, the sheets and the bench could stay on the character because those
are labelled on screen in words.

It is deliberately **not** done. A keybind system is coming later, and it will make movement remappable
for the same reason — so a half-migration now would mean writing the physical-key path, then replacing it,
and shipping a state where `A` moves left on one layout and does nothing on another. Accessibility is a
real gap and this is a fair note to have raised; the answer is the keybind screen, not a patch in the
input layer. Left as-is deliberately, not missed.

---

**The Arcane Beam.** The user reported its "raw TTK is way below the Bolt". Measured over 10 seconds
of held fire against a dummy, it is not — it is *slower* at every range, and strongest past 250px:

| weapon | pulls/s | dmg/pull | TTK @100px | @250px | @380px |
|---|---|---|---|---|---|
| Bolt | 2.40 | 7.00 | 1.45s | 1.86s | 2.69s |
| Scatter | 1.40 | 14.40 | 1.27s | 2.10s | 2.68s |
| Arcane Beam | 16.20 | 0.84 | 1.79s | 2.00s | 2.48s |
| Voidball | 3.80 | 3.40 | 1.88s | 2.23s | 2.92s |

So the perception is not dps. The two real distortions, both measured:

- **It is the only sub-1-damage gun in the game.** A lunge does 2, a shell 1.8. The Beam does 0.84, so
  a 24 HP body takes **29 hits**, each independently dodgeable or interruptible. That is the "no chunk"
  feel, and it is what item interactions (knockback, stun, on-hit procs) multiply against.
- **`dmg = w.dmg + Stats.value('strength')` is flat, so a low base is pathological under buffs.** A
  flat +4 Strength is +57% on the Bolt and **+476%** on the Beam. Raising the Beam's damage without
  fixing this makes the problem worse, not better.

The proposed fix, held until the user has playtested: raise the base *and* lower the rate together
(`dmg` up, `cooldown` up), so dps holds, per-hit chunk rises, and the Strength multiplier shrinks.
Narrow the cone from 0.16 rad — the widest in the roster, and it scales with Precision. Leave the
falloff alone; the best long-range retention in the game is the Beam's identity.

**Undecided:** buff the Beam or nerf the Bolt, and whether to put a floor under weapon damage
(no hit below 1.0) rather than special-casing one gun.

---

## A body that answers the gun you are holding

Enemies that read the player's build. The rule that governs all of it is one sentence, and it is the
depth-only rule pointed the other way:

> A trait may change **where** a fight happens. It may never change **how hard** it is.

The ladder is the only thing in the game permitted to raise a number, and nothing in `applyTrait` is
allowed to read it. If a trait made a body tougher, the floor would get harder for the player who
happened to pick the wrong gun — which is the exact failure the depth rule exists to prevent. So
there is no trait here that touches health, damage, speed, cadence, radius, accuracy or aim error.
Every one of them is a change to a body **geometry**.

That is not a promise, it is an assertion. The suite spawns sixty identical shooters under each of
the four guns and requires the combat columns to come out byte-identical, asserting on the *set of
distinct values* per column rather than on a value read back from a field the test just wrote:

| gun | traits | maxHp | dmg | armour | r | speed | pspd | cdMin | band |
|---|---|---|---|---|---|---|---|---|---|
| Bolt | 0, 2 | 7.56 | 1.8 | 1 | 14 | 0.648 | 2.2 | 70 | 180/108 |
| Scatter | 0, 1 | 7.56 | 1.8 | 1 | 14 | 0.648 | 2.2 | 70 | 250/150 |
| Arcane Beam | 0, 1 | 7.56 | 1.8 | 1 | 14 | 0.648 | 2.2 | 70 | 250/150 |
| Voidball | 0, 2 | 7.56 | 1.8 | 1 | 14 | 0.648 | 2.2 | 70 | 180/108 |

**Combat drift: none.** Only the standoff band differs, and only where the table says it should.

### The mechanism, and why there is only one

A ranged body derives its standoff fresh every tick:

```
standoff = far - (far - close) * roomPress * PRESSURE_CLOSURE
inside close -> walk TOWARD the player
outside far  -> walk AWAY from the player
```

`far` and `close` are per-body and set once at spawn, so shifting them moves the whole band. Shifting
**both by the same factor** is the only safe version: the band keeps its width, so a body does not
start dithering, and the change is a place rather than a personality. Two clamps keep it honest —
`far` under `sense * 0.78` (a body that notices you at 600 and holds at 700 stands in a corner and
never fires, which reads as broken rather than as an answer) and a 40px band floor (a band that
collapses to a point makes the body twitch on the spot instead of standing somewhere).

### The pairing is the design

Each gun has exactly one real weakness, and the trait is that weakness answered.

- **Scatter** — a knife gun, `fFar` 300. It wants to be standing next to something. `TRAIT_HOLD`
  stands further out than the gun is worth: 165px → 239px under pressure, **24% of the Scatter's
  damage per shot**, past its falloff floor entirely. The strongest answer in the game, and the
  highest weight, because the falloff is the most punishing in the roster and there is nothing
  subtle about it.
- **Beam** — a mid gun, `fFar` 470. The same answer for a different reason: not the knife, but the
  middle, where its own falloff starts costing it. 9%.
- **Bolt** — one committed shot on a 38-tick cooldown, aimed at where a body *was*.
- **Voidball** — `TRAIT_CLOSE` walks in, which is the worst thing that can happen to a long reload:
  the player has to hold a moving body at knife range on a cadence chosen for a target across the
  room. Two of the four guns are single-target, so they get the same answer, and that is the honest
  answer rather than a coincidence — both are aimed at one body, so both are answered by a body that
  is not standing where it was aimed.

The player is never told which gun a room dislikes. A visible counter is a puzzle with the answer
printed on it; an invisible one is a fight the player solves by noticing which gun keeps working.
What *is* visible is the consequence, because the player has to be able to act on it.

A trait is rolled **once, at spawn**, from the weapon actually held, and written onto the body.
Reading it per-tick would mean a body changed its mind the moment the player swapped guns mid-room.
The suite asserts that stability directly: spawn a traited gunner, swap the player's weapon, run 180
ticks, and require its band to be untouched.

### The bug this section exists to prevent

The first version had a "close faster" trait that set `e.walkSpeed`, intending to make a shooter
press in. **It did not make a shooter press in.** The tick decides what kind of body it is holding
with `e.walkSpeed !== undefined` — the walker branch has the field, the ranged branch does not — so
writing that field onto a body that lacked it *converted the shooter into a lunger*. The shell, the
cadence, the muzzle prediction and the standoff rule all stopped running, and the room became a
silent, worse version of itself with nothing anywhere reporting why.

It was caught by noticing that "make it close faster" is a speed change, and a speed change is
difficulty. The trait system was written *before* the suite, so the suite caught nothing. The
assertion exists now — on both signs, for every body type, under every gun — and it was
mutation-checked by reintroducing the exact bug: **8 violations, precisely the ranged bodies, on all
four guns.**

**Never add or remove `walkSpeed` in a trait.** Only `far` and `close`.


## Rooms bigger than the screen, and a camera

A room is DATA. `bounds` sits on the room record — `{l,t,r,b,w,h}` written out rather than derived,
so a room with an asymmetric inset or a corridor stub has somewhere to go — and `boundsOf(room)`,
`roomL()`, `roomR()`, `roomT()`, `roomB()`, `roomW()`, `roomH()`, `roomCX()`, `roomCY()` read it.
Each takes an **optional room**, which is the escape hatch: the day something needs a room that is
not the current one, it passes that room in rather than reaching for a global.

### The shorthand, and why 169 call sites were not edited

`ROOM_LEFT/RIGHT/TOP/BOTTOM` and `MIDX/MIDY` used to mean "the room", and 169 sites in seven
gameplay files read them as the walls. They are now `let`, written by exactly one function —
`syncRoomBounds()`, called from `enterRoom` and the two places that build a run. All 169 sites now
read the current room with **zero of them edited**.

The alternative was rewriting all 169 to call the accessors, which is the cleaner end state on
paper and also a change too wide to land in one sitting and possible to leave **half done** — which
is the state where some sites read the room and some read a 700x450 constant, every one individually
plausible, and the bug only appearing in the first room of a different size. A half-migrated
geometry is worse than an unmigrated one, because an unmigrated one is at least uniformly wrong.

**The cost, stated plainly:** a `let` read before the sync has run is a stale number, where a
function call could not be. A reader who wants certainty calls `roomL()`, which reads the room every
time. `roomBounds()` deliberately anchors on `STD_ROOM`, never on `ROOM_LEFT` — a room built while
standing in a big room would otherwise be positioned relative to that big room.

### The camera is a render-time transform, not a change of coordinates

Nothing moved to accommodate it. Every distance, body, projectile and wall reference stays in world
space; the view is one `ctx.translate(-cam.x, -cam.y)` around the room draw **only**. The HUD, the
minimap and every overlay are outside it, because a HUD that scrolled off the corner of a big room
would be a HUD nobody can read. It is computed immediately before use, at the point of draw.

### THE LESSON, which cost a bug to learn

The clamp on the big-room branch was `Math.min(b.l, …)` with the bounds transposed. It pinned the
view to the room's left edge and **the camera never moved at all**. It passed the whole suite,
because every room in the game fits on screen and so that line had never executed once.

> The property that made the camera safe to add to a green suite — it is the identity transform for a
> room that fits — was the same property that hid a total failure inside it. **A feature that is a
> no-op everywhere it is actually used is not a tested feature; it is an untested one wearing a
> passing disguise.**

So the big-room checks assert the *moving* case, and reverting the clamp makes them fail with both
numbers in the message.

## The bench row — why only the item is named

The row under the minimap is three plates: the weapon, the **active item** (Q), and the alt. For most
of this project's life each carried a text label centred under it, which overlapped — `Arcane Beam` is
73px in a 62px plate, and eleven of the thirteen item names are wider than their plate, `Lantern
Friend` by thirty pixels.

Three layouts were tried and **two of them passed the suite while being wrong**, which is the reason
this is written down:

1. **A budget per label, derived from its neighbours.** Correct arithmetic — three labels 62px apart
   do not collide when `w2 <= 124 - max(w1,w3)`. The screenshot read
   **`ARCAN...   UNTER'S ...   BLAST`**. It fixed a collision by deleting the information.
2. **All three names as one line of text**, which is the right *shape* and still does not fit: the row
   is **147px**, and the three names at 12px come to **210px** worst case. Its test asserted against a
   hardcoded row width of **190**, so it passed while checking a geometry the game does not have. A
   test that cannot read the number it is about has to guess it, and a guess comfortably larger than
   the truth fails nothing.
3. **The item name alone, across the whole row** — what ships. The two weapons keep their icons and
   their cooldown sweeps, the footer already reads `LMB cast / RMB blast / SHIFT blink` every frame,
   and the sheet (F1) lists both weapon names in full. The item name is the one that is new,
   unbounded, and absent from the sheet until you go looking — so it is the one that gets the space.
   Every name in the roster now fits **whole at 12px**, the longest with 47px to spare.

Two things follow from this that the suite enforces rather than trusts:

- **The row width is published (`BENCH_ROW_W`) and read by the test.** Hardcoding it is how version 2
  passed.
- **The overflow branch is exercised by a name that overflows.** Every real name fits, so the shrink
  and truncation paths never run — and a mutation that deleted the shrink passed 178/178 for exactly
  that reason. A test that only walks current content cannot see whether the paths that protect the
  *next* item name work at all.

One trap worth naming: truncating by dropping the last character and re-appending `...` is an
**infinite loop**, because the string never gets shorter. It surfaced as a frozen test run rather
than a failure, and no current name reaches it, so it would have shipped. The truncation is built
from a prefix and re-measured each step, which is monotonic by construction.

> The defect that reached the screen was never the overlap. It was a collision fixed by shortening the
> text until the collision could not happen — and the shortening was invisible to every check that
> asked only whether anything overlapped.

## The Warden's bar, and the third fixture trap

`drawBossBar` in `70-view.js`, drawn from `drawHUD` only while a live boss is in the current room.

**The bar was never missing.** An earlier commit reported that `bossBar`/`bossHealth`/`drawBossBar` had
no hits in `src/` and that the player "cannot see it move." The grep was right and the conclusion was
wrong: the boss **did** have a bar — a 56px sliver, twice a body's, riding the body as it walks. The
measurement that "proved" its absence called `spawnEnemy(false, …)`, which returns a **lunger**. A grep
for a function that does not exist and a body that does not exist look identical from outside.

Three fixtures in this project now return something usable-looking instead of what was asked for,
and each is documented where it is used:

| call | what it gives you | what you wanted |
|---|---|---|
| `spawnEnemy(false, …)` | a lunger | the boss — the flag is the **first** argument |
| `spawnEnemy(true, …)` returns; you must `room.enemies.push` it | a body in nowhere | a body in the room |
| a body with `noticeTimer>0` | `continue` — skipped entirely | a body the tick loop acts on |

What the bar had to add, none of which "a bar exists" would have tested for: it **shows what is left**
rather than whether the last shot landed; it **marks both phase thresholds**, so crossing 66% and 33%
is visible instead of merely having happened; it is **fixed** while everything else in the fight moves;
and the Warden has a **name** at all — it had none, and was `THE WARDEN` in a dozen comments.

**THREE PLACEMENTS, ALL WRONG, and none of them visible in the source.** It started at
`ROOM_TOP-46` = y 84, which is exactly `MARGIN_Y + HP_H + ROW_H` — the momentum row, caught only by a
screenshot. The test for it asserted *"above the room"*, which was **true of the broken version and
false of the fix**, so a correct fix would have failed it. It moved inside the room's top edge, which
cleared the HUD plates and then sat **in the playing area** — reported as disruptive. It is now in the
20px strip **below** the room, at the bottom of the screen.

That position forced the one genuinely structural decision: **the bar is anchored to the CANVAS, not
to the room.** A room-anchored bar at the bottom works on a floor, where the room ends 20px above the
canvas edge and the camera is pinned — but the Lab is 1680×760, its room runs to y 890, and the camera
scrolls, so a room-anchored bar lands 290px **off-screen** in the one place the bar exists to be
looked at. The tests now assert the two properties that survived every version — *on the canvas* and
*out of the room* — rather than a description of where it is.

`BOSS_BAR_MARGIN` is **23**, not the 14 it started at: the bar carries a 3px frame, and at 14 that
frame straddled the room's bottom wall (wall at y 580, frame ending at 589), so the bar looked bolted to
the masonry. The labels sit **above** the bar, because below is the canvas edge.

**ONE BAR, NOT TWO.** The boss also had the floating sliver every other body gets, on top of this one.
Two bars for one health disagree the moment both are on screen — the floating one is 4px and rounded
to whole pixels, the fixed one 10px and notched — and it was redundant with the player's own bar: the
fight had two health bars and one belonged to a third party. `drawBossBar` is now the only place the
boss's health is drawn. Every other body keeps its sliver: at 4px over a 14px body it is a glance,
not a reading, which is the right amount of attention for a Brunch.

**THE FIRST TWO PLACEMENTS, for the record.** The bar was first put at
`ROOM_TOP-46` = y 84, which is exactly `MARGIN_Y + HP_H + ROW_H` — the momentum row. The test for it
asserted "above the room," which was true of the broken version and false of the fixed one, so the
correct fix would have failed it. It now asserts the collision: where the plates **end** versus where
the bar **starts**. Second, the rectangle was baked from `ROOM_W`/`ROOM_TOP` at load time, so in the
Lab — 1680×760 against a floor's 700×450 — it drew a floor-sized bar in a Lab-sized room. A constant
that describes a room is wrong the moment there is more than one room, and the Lab exists to prove
those are not the same thing. It is `bossBarRect()`, worked out per draw.

> The notch and the fill's edge are both `Math.round(w*f)` on the same `w`, and that is the whole
> claim: **at exactly the threshold health, the fill's edge IS the notch's pixel.** Comparing them as
> fractions — `round(w*th)/w` against `th` — comes out 0.0006 apart, which reads as a mismatch and is
> not one. Dividing a rounded pixel back out and comparing it to a raw fraction manufactures a
> disagreement that does not exist on screen.

## The lab — four defects that only walking into it could find

`src/35-devlab.js`. **F2** in and out, **F3** freeze the row, **F4** arm the dropper, **F5** drop a
dozen, **F6** refill the shelf. A 1680x760 room, so the camera genuinely moves.

The room in this file is a 1680x760 room and the canvas is 960x600, so the camera is not optional here —
it is the only way to see most of the room at all. That makes the lab the project's one testbed for
anything about a moving camera, and it is worth knowing that when judging a HUD that has to work while
the world scrolls under it.

Four things were wrong with it, and **none of them are findable by reading the module.** They are what
you get when a view is built to be looked at and the things that only happen when you *use* it are
never exercised — which is the standing lesson of this project, happening inside its own house.

1. **The flame was on the floor beside the brazier, not in it.** Two separately baked sprites, each
   centred on its own middle, so their features sit at offsets: the bowl at `o-21` inside the brazier,
   the flame's base at `o+25` inside the flame. Drawn at two unrelated numbers those put the fire
   **166px below the dish** — a fifth of a brazier's height. The only relationship between the two
   numbers that means anything is that the flame's base must land on the bowl's centre, and the code
   now says exactly that.
2. **The floor was a grid of white squares.** The lab drew a 120px lattice over a floor that is
   otherwise a seamless speckle; 14 verticals in a 1680px room, which reads as tiles. **A dungeon room
   has no grid at all** — which is the whole reason the dungeon floor looks like ground and the lab
   floor did not. The lab was carrying a visual element the game does not have, over the element the
   game does have. The minor lattice is gone; only 3 majors remain.
3. **Items could not be picked up.** The shelf was lab-owned data, deliberately not pushed into
   `r.pickups`, on the reasoning that it is a thing to look at. In a room with a roster on a rail, the
   first thing anyone does is walk up and take one — and this was the one place in the game where that
   was impossible. A dev view that refuses to do what the game does is testing something else. Each
   alcove is now a real pickup through the game's own `Items.give` path, so the lab exercises the
   displacement-and-drop logic on every walk past the rail. **F6** refills them at once.
4. **Leaving the lab leaked thirteen items into the next run.** Two owners of one field: the shelf
   rebuilt `r.pickups` by *reassigning* it while the pickup loop in `60-tick` spliced the old array in
   place. Leaving filtered the new array, leaving the old one intact. Both sides now splice in place.

> **`const` does not hoist, and an object literal reads its values when it is built.** Publishing the
> brazier geometry as `const BRAZIER_GEOM={bowlY:BRAZIER_BOWL_Y,…}` near the top of the file, from
> constants declared seventy lines lower, threw a `ReferenceError` the moment the module loaded — `Lab`
> never existed and **32 tests failed for reasons that had nothing to do with any of them.** It is a
> function now, so the order cannot matter.

**Three things the tests for these got wrong first**, each the same shape as the ones before them:

- The flame test computed from the constants and so **could not see a wrong call site** — a mutation
  putting the flame back at `y-48` passed it. It measures the `drawImage` calls now.
- The first version of that measurement had the bowl's sign backwards and **failed against correct
  code**, reporting the fixed drawing as 42px wrong. The screenshot agreed with the code. A test
  written to catch a geometry bug, getting that geometry backwards, is the same failure it was
  written to catch.
- The shelf test **stood the player on the alcove**, so the refill was consumed again the instant it
  appeared — correct behaviour that looks exactly like a shelf that never refills. Two wrong diagnoses
  came out of that before measuring tick by tick: `gone=2,1,0` with the pickup back, then `525` again
  because the player was still standing there.

## THE NUMBERS THE PLAYER READS, and the arithmetic accident underneath them

Three separate defects, worth keeping apart because only one looks like what it is.

**1. `BOSS_SHELL_DMG = SHOT_DMG * 0.8` evaluated to `1.4400000000000002`.** Seventeen significant
digits for a tuning number, and it propagates into everything derived from it. The intent was `1.44`;
that is now what is written. A constant that is a product evaluated in binary floating point is not a
design decision, it is an accident that has been copied forward.

**2. `fmtHearts(21.25)` printed `"10.625 hearts"`.** A real total, in a unit the interface cannot draw.
Health is *measured and displayed* in half-hearts, so anything finer is noise — and a number the player
cannot reconcile by eye invites them to check the arithmetic instead of reading the result. Now rounded
to the nearest **half** heart: `10.5 hearts`, `17.5 hearts`, `1 heart`.

**3. The dev panel printed `toFixed(2)` on derived products**, so `0.5+0.66` rendered as `1.16` or
`1.1600000000000001` depending on the day. All of it goes through `showNum(v, dp)`, precision chosen per
quantity rather than as a blanket default.

> **The simulation is NOT rounded — only the display is.** A lunge costs `LUNGER_PAY 0.96 × ARMOUR 0.66
> = 0.6336` half-hearts. Rounding *that* at the source makes every sub-half hit worth a whole heart,
> which is a balance change wearing a formatting costume. This ladder is fractional on purpose —
> `ARMOUR 0.66`, `LUNGER_PAY 0.96`, `TEMPO.rate`. There is a test that asserts the fraction survives
> into the player's health bar, precisely so "tidy the numbers" cannot quietly become "rebalance the
> game".

Two fixture traps cost a wrong diagnosis each, both recorded at the test: `startGame()` leaves the
player in invulnerability and `damagePlayer` returns false on iframes *before* subtracting anything (so
the first version reported "the lunge did no damage" as though the model were broken); and taking a hit
*grants* i-frames, so a second `damagePlayer` in the same test is refused with no obvious reason.

## THE FLAME, four attempts and a lesson about where to look

Both sprites are baked centred on their own middle and drawn by their **top-left** corner, so a feature
at sprite-row *F* appears on screen at `spot − R + F`. Every error below came from getting one letter of
that wrong:

| version | error | what went wrong |
|---|---|---|
| original | **2px** | it was nearly right, and I called it 166px |
| fix 1 | **−168px** | read the bowl as 21px *below* centre, and subtracted the radius again |
| fix 2 | **+11px** | read a *distance-from-centre* as an *absolute sprite row* |
| now | **0px** | `FLAME_BASE_ROW` is a named row, and the drawing cannot misuse it |

`flameFrame` bakes `moveTo(o, o + FLAME_BASE_Y)` where `o` is already `S/2 = FLAME_R`, so the base row
is `FLAME_R + FLAME_BASE_Y = 61` in a 72-row sprite. **The 25 is a distance from the sprite's middle, not
a row.** Reading it as a row put the fire one whole half-size too low — which is exactly why it kept
landing on the base of the stand.

> Three tests asserted this by computing from the constants, and **all three passed while the flame was
> in the wrong place**, because the formula and the mistake shared the same wrong idea. A test written
> from the implementation checks that the implementation agrees with itself.
>
> The one that works reads the **framebuffer**: it finds the warm pixels of the fire and the cool pixels
> of the metal it sits in, and requires the fire to be inside the bowl rather than beside it. That is a
> property of the picture, which is the thing that was wrong. It is a property of the output rather than
> of the code, which is the only kind of check that cannot share a blind spot with the bug.
>
> Its first version sampled 60×120 and reported the fire spanning −43..58 — below the bowl — because it
> was measuring the brazier's **radial light pool**, a warm gradient baked 336px across. The sample is now
> the stand's own width and a 70px window.

**The fire was also running at 14Hz.** The cycle was 5 frames every 3 ticks at `TICK_HZ 210`: a full loop
in **71ms**, changing **70 times a second**. That is a strobe, not a flicker — and because consecutive
frames differed *only* in height (19 → 24, the same shape at five sizes) the eye had nothing to track
and read it as a buzz. Now `BRAZIER_FRAME_TICKS = 18`: a **429ms** loop, **11.7 changes/second**, inside
the range a real flame flickers at, with each frame also varying in **lean** and **waist** so there is a
shape change to follow rather than only a size one.

## F5 IS NOT A LAB KEY

**F7 drops a dozen.** F5 was, and it is *reload in every browser* — so "drop a dozen" reloaded the page
instead, throwing away the room you were setting up to test. There was nothing to intercept: the page
reloads before the handler runs. F7 is unbound in every major browser. The whole lab block is F2–F4/F6/F7
for the same reason (F5 reloads, F6 focuses the address bar in some, F11 fullscreen, F1 the weapon bench),
and `Lab.key('f5')` now explicitly returns false so the omission reads as a decision rather than a gap.

### The lab itself

`src/35-devlab.js`. **F2** in, **F2** out, **F3** freeze/release the row, **F4** arm the dropper,
**F5** drop a dozen. A 1680x760 room, so the camera genuinely moves.

It is not a run, and each of these is asserted rather than assumed: `state==='dev'`, so nothing
counts; no doors, so there is nothing to progress through; death **cannot** reach `endRun`, so no
record is written; leaving goes to the **title**, not back into a half-remembered floor.

**The lab cannot die, enforced in one place.** It was first enforced at the two death checks in
`update()` that a first reading suggested were the only ones. There are **four**. The other two were
left live, a drove killed the lab player through one of them, and the run recorded a death that never
happened. The rule now lives in `damagePlayer` — the single funnel — and floors health at 1, so the
four checks need no knowledge of the lab. Knockback and the momentum cost are deliberately *not*
skipped: a body that cannot be hurt still gets shoved, and a lab that protects you from consequences
is a lab that will lie to you about momentum.

The row is **still and silent**; the drove is awake and does neither. Damage numbers are **derived**
by comparing each body's health with the previous tick, not reported by the damage path — a second
place that knows how much a weapon hits is a second place that drifts when a trait or a falloff
changes. A rise prints in green. The number is a **delta from the last tick**, not a total.

### Resolved: the drove "does not approach" was a measurement error

This was carried as an open bug and was never one. The check measured **40 ticks**, which is almost
entirely the **lunge windup** — a lunger plants itself and stops to telegraph before it commits, and
that pause is the design. The diagnostic that called it a bug printed `lungeState='wind'` and read
past it.

Measured over 120 ticks a drove body closes **279px → 83px** and lands a hit. The check now runs a
window that clears the windup and measures **path length** rather than net displacement — the second
time that check measured the wrong quantity, and the first time it measured a deliberate pause in an
animation and reported it as a stall.

## One frame, and knowing which one you are in

The single most productive bug class in this project, and it has one shape: **two things that were
true by accident, agreeing.**

- The **cursor** is in screen space; the player, bodies and walls are in world. The aim was
  `atan2(mouse.y - player.y, …)` — a vector from a screen point to a world point — so every shot was
  off by the camera offset, **up to 21.28 degrees**, changing sign across the frame. It read as "a few
  degrees off, counter-clockwise" from any one seat. 163 checks passed throughout because every
  fixture wrote a **world** position into the cursor and the game read a **world** position out of
  it. Fixed with `mouseWorld()` / `screenToWorld()`, which call `updateCamera()` rather than trusting
  `cam` — baking the world position in on `mousemove` would make the aim drift as the camera scrolls.
- Fixtures now say which frame they mean: **`pointAt(x,y)`** means "put the cursor over this world
  point". A world **delta** needs no conversion (`world = screen + cam`, and `cam` is constant across
  a loop) — only an absolute position does.
- **A test that derives its expectation from the same helper the game uses is not a test.** The first
  version of the aim check computed its expected angle with `mouseWorld()`. Mutating the helper back
  to the identity failed twelve checks and passed that one, because the expectation and the game were
  wrong identically. The expected value must come from something the game does not use.

## Values derived from the world are only correct until the world changes shape

`AGGRO_RANGE` and `SWERVE_DEADZONE` were both derived from the room size **once, at parse time**,
back when there was one room shape. `AGGRO_RANGE` read 707 where a 1680-wide room wants 1567, so a
lunger 900px away stood still; `SWERVE_DEADZONE` stayed at 300, so a gunner read a reversing player
at full strength from across the room — the counter to the whole mechanic, silently off.

They are functions of the current room now. **In a standard room they return exactly what they always
did** (707 and 300), which is the property that makes this a fix and not a retune — every number
measured in this project's history was measured in a 700x450 room.

The question that found them is the one worth asking again:

> What else is captured at module load, from something that now varies?

## The separation pass, and the order it visits pairs in

Cost per body used to climb with room size: 1.30us at five bodies, 8.35us at a hundred and sixty, with
the doubling ratio reaching 3.96 where linear is 2.00. A quadratic wearing a linear costume, invisible
only because a room tops out near 23 bodies. Now a uniform grid; per-body cost is flat, and 160 bodies
went 1.336ms → 0.644ms a tick.

**The grid is only faster if it visits pairs in the same ORDER.** `bounceEnemies` mutates both
bodies, so the order decides the arrangement a pack ends up in. A grid naturally yields candidates
*grouped by cell*, which is a different order from the double loop's ascending index. The first
version did not sort them, and the equivalence check caught it: 1.01px on a forty-body room, which I
first wrote off as a boundary approximation, then **76px on sixty-four bodies**, which is not a
boundary anything. Sorting the neighbourhood back into index order fixed it.

Two checks, and the first earned its keep: an **equivalence** check that runs both passes on
identical copies of a crowded seeded room and compares body for body — it catches a missed pair, a
reordered pair and a wrong cell size, none of which a screenshot would. And a **shape** check that
asserts per-body cost stays flat across a 4x range, rather than a millisecond budget: absolute
timings mean nothing off the machine that wrote them, and a test with a hardcoded ms figure gets
deleted rather than fixed.

## Performance: what was actually slow

Measured, and the first two stories were wrong.

| | before | after |
|---|---|---|
| normal frame, 30 bodies | 0.44ms, **0 gradients allocated** | unchanged |
| lab frame, 41 bodies | 0.94ms, 29 gradients | 0.92ms, **1 gradient** |
| `update()`, 160 bodies | 1.336ms/tick | **0.644ms/tick** |
| `update()`, 20 bodies | 0.036ms/tick | 0.063ms/tick |

The lab allocates nothing per frame now — braziers, plinth shadows, alcove glows and the survey grid
are all baked sprites, the way the floor and the sprites already were. **That bought 0.02ms.** The
gradients were not the cost, and neither was the next guess: a full-room 1680x760 `drawImage` of the
floor and the grid measures 0.0015ms, the same as a clipped 960x600 one, because the compositor does
it. What is left is many small draws with no hot spot — 93 `fillText` against a normal frame's 6,
because the lab engraves every label twice.

The real win was not a hot spot at all. It came from asking **what shape is the cost** rather than
**what is slow** — and the honest summary is that the game was never slow, and the one number worth
having is the one that says what happens when the content outgrows the code.

## Current state

- `depths.html` — a shell loading sixteen modules from `src/`. Playable, double-clickable.
- `src/99-tests.js` - **200 checks**, every test seeded to an identical world. All must pass at
  every commit. The change history (`FIXES`, in `80-ui.js`) is **107** entries and is itself checked.
  `verify.ps1` counts that table from a regex and prints the number; it does **not** assert it, and
  this paragraph used to claim the suite did. What the suite actually checks is the direction that
  matters: every pinned fix must have a test carrying its name, and an entry with no matching result
  is reported **UNVERIFIED** in amber rather than scored as a pass. Neither number is derived from
  the other, so they drift apart when either is edited — read both, trust neither alone. The panel
  prints both and names each, because they are genuinely different: the table is bugs found and
  pinned, the suite is every standing guarantee.
- `csharp/Depths.Core` + `Depths.Tests` - **88 checks**, parity-verified against the JavaScript.
  Still no world state; see the section on the port boundary above.

### The port's own drift, and the test that was defending it

The port is where the hand-duplication of numbers bites, and it had already bitten:

| | game | port (was) |
|---|---|---|
| `SwerveDeadzone` | 300 | **350** |
| `SwerveFull` | 430 | **570** |

`Intercept.SwerveReach` reads both, so the port's gunners read a reversing player differently from
the game's, in every build, silently. It survived because there was a test called
**`TheDeadzoneIsHalfTheRoomWidth`** asserting 350 and 570 — the name states the *rule*, the game's rule
is half the width **less a margin of 50**, and the constant had been written from the prose. The test
then re-derived the same prose and passed.

**A test that re-derives the rule it is auditing cannot find the rule being wrong, and will defend
the wrongness for ever.** That is the same shape as the cursor bug (fixture and game agreeing on a
frame) and the separation grid (candidate order). Three instances, one cause.

`Balance.Room` is now a struct (`l/t/r/b`, `W`, `H`, `Cx`, `Cy`, `Standard`), and `SwerveDeadzone`,
`SwerveFull` and the new `AggroRange` are **functions of a room** — methods, not properties, because
C# will not let a property and a method share a name. Expected values are read out of the browser;
the port's own docs already record that hand-computing one produced three wrong numbers out of nine.

**Not ported, deliberately:** the HUD, boss bar, character sheet, Scatter recoil, hook rework, Brunch
arc AI, the camera, and the world/screen frame. The first group is cheapest to iterate in JavaScript
and the port has no presentation layer to keep in sync; the second is the next step, and it should be
next *because* the foundation now models a room as data.
- `src/` is the reference implementation and stays alive. Features are designed and playtested here
  first, because it is the only artifact the player can run, then ported.
- Unity 6 LTS and VS2022 are installed. The port resumes at `60-tick.js` (`update()`), then the
  world generator, then the presentation layer against the finished core.

### The fodder, and what each one is for

Four enemy types, deliberately unequal in what they ask of the player. The lunger is the one that
teaches; the other three exist to make a room's answer depend on which of them is in it.

- **Lunger** — steers, closes, lunges. The baseline body and the only one that reads the player's
  position directly. (Renamed from "chaser" throughout; nothing named chaser remains.)
- **Shooter** — keeps its distance and fires a committed shell. It now **walks while it charges**,
  which was the point: holding the ground was the gunner's trick and made a shooter a statue at a
  fixed spot for half a second every cast. Measured: it moves on 54 of 162 charging ticks, the
  gunner on 0 of 125. The cost is accuracy against a reversing player — 38% at 200px against a
  straight runner's 100% — which is recorded as a floor in the suite rather than tuned away.
- **Gunner** — the heavy committed shot. Roots itself to charge, so its accuracy is measured to be
  exactly what it is. The type test means the movement change costs it nothing.
- **Boss** — the Warden. Not a scaled lunger: a phased fight with a TELL, a COMMIT and a RECOVER for
  every move, and a wall it can call. See the section above.

### The C# port, and exactly where its boundary is

`csharp/Depths.Core` has `Balance` (all the tuning, plus the depth ladder), `Hit` (the player hit
test), `Intercept` (lunge and gun solutions), `Mulberry32` and `Rng` (the three streams, the floor
seed, the base36 codec). `csharp/Depths.Tests` has **88 checks**, all parity-verified against numbers
read out of the running JavaScript.

**There is no world in the port yet.** No player, no body, no projectile, no room, no tick. Everything
ported so far is a function of its arguments, which is exactly why it could be ported honestly — the
moment a type needs to hold mutable state that another type also mutates, "translate it mechanically"
stops being available and `update()` has to be ported against the tests rather than by reading it.

Two things to know before adding to the port:

- **A port that agrees with its own source is not parity.** Every expected value in
  `RngParityTests` and `DepthLadderParityTests` was read out of the browser. When a value was
  hand-computed instead, three of nine were wrong and the *test* was what failed.
- **C# is not JavaScript where it looks identical.** `(uint)someDouble` above `uint.MaxValue`
  saturates; `>>> 0` wraps. The original's `Decode` relies on the wrap, so the largest seed the game
  can display could not be typed back in. That was a real bug in the port, found by a parity test,
  and it is the kind of thing that would have shipped.

