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

**Always work prototype graphics into anything the player actually sees.** No grey rectangles, no
"TODO art". If a player will look at it, draw it properly the first time. The controls sheet and the
bug list are the standard: real key caps, a drawn mouse, a wood-and-paper card. A feature that is 95%
done and looks like a spreadsheet reads as broken, not as unfinished.

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

## Current state

- `depths.html` — a shell loading fourteen modules from `src/`. Playable, double-clickable.
- `src/99-tests.js` — **128 checks**, every test seeded to an identical world. All must pass at
  every commit.
- `csharp/Depths.Core` + `Depths.Tests` — 19 checks, parity-verified against the JavaScript.
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
- **Boss** — a placeholder. Boss design is deferred; the fodder is the priority.
