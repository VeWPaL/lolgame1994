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

# PROPOSED — awaiting the user's decision

Everything below is a **proposal, not a rule**. Strike what does not earn its place.

## Code structure

1. **Modifiers are derived, never written back.** A stat is recomputed from base values plus the
   active item set on every change — `effective = base + sum(flat) * prod(1 + mult)` — and nothing
   ever does `player.speed *= 1.1` in place. This is *the* classic stacking bug in the genre: after
   twenty items the value has been multiplied in an order nobody can reproduce, removing one item
   does not remove its effect, and the build cannot be explained or saved. Cheap to state, expensive
   to retrofit, and it is the foundation the whole item system stands on.

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
   This session's whole value came from doing it the other way round and paying for it.

## Game design

8. **Items bend the existing verbs; they do not add a third button.** Four guns plus Blast and Hook
   is tight and complete. Adding a verb changes the hand position, the muscle memory and the
   difficulty budget all at once. The interesting space is *which* body you shoot first, *when* you
   spend a charge, *where* you stand — not another key. *This is the proposal I am least sure of and
   most want argued about.*

9. **Every item must change a decision, not a number you never think about.** +5% damage is invisible.
   An item that makes the room's problem different is a build. If a player cannot describe what an
   item makes them *do* differently, it is not an item — it is tax.

10. **Tension beats stacking.** The real reason twenty items feel like two hundred is that they
    exclude each other: strong with Bolt, poor with Scatter; a build that wants to stand still
    against one that wants to blink. Additive-only items are a slider, and a slider is played once.

11. **Rarity is a cost, not a size.** A legendary should be build-defining and slightly awkward.
    Otherwise "rarer" only means "a bigger number", and the roll stops mattering.

12. **Nothing is added to the pause menu unless a decision depends on it.** Six stats and a build
    list is a lot of screen. Every element there is a thing the player must learn to read.

13. **Difficulty comes from rate and density, never from an unreadable threat.** Keep human reaction
    time intact. The gunner at 200px is 100% lethal and that is *correct* — it is a commitment you
    must respect, not a bullet to dodge.

---

## Current state

- `depths.html` — a shell loading twelve modules from `src/`. Playable, double-clickable.
- `src/99-tests.js` — **109 checks**, every test seeded to an identical world. All must pass at
  every commit.
- `csharp/Depths.Core` + `Depths.Tests` — 19 checks, parity-verified against the JavaScript.
- `src/` is the reference implementation and stays alive. Features are designed and playtested here
  first, because it is the only artifact the player can run, then ported.
- Unity 6 LTS and VS2022 are installed. The port resumes at `60-tick.js` (`update()`), then the
  world generator, then the presentation layer against the finished core.
