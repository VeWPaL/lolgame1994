# Conventions

Standing decisions for this project. **This file is the memory that survives context loss.** If a
rule matters and is not written here, it will be forgotten and then re-litigated. Add to it rather
than relying on a conversation.

---

## Hard constraints

**The game is never commercialised.** Shared with friends, possibly uploaded free. Never sold. This
settles engine licensing permanently: Unity Personal is free under any circumstance we could reach,
so the revenue threshold is irrelevant. Do not re-open this.

**`depths.html` is LF-only with no BOM.** Verified after every write. The middle dot is an encoding
canary: `Â·` must never appear. The `write` tool emits UTF-8 *with* a BOM and PowerShell's
`Set-Content -Encoding UTF8` adds one too — strip with
`[System.IO.File]::WriteAllText($p, $t, (New-Object System.Text.UTF8Encoding($false)))`.

**`core.autocrlf=false`, `core.eol=lf` in git.** A CRLF pass would silently break the canary. Do not
"fix" these.

**Two weapons are frozen: Scatter and Arcane Beam.** Do not touch them without being asked.

---

## Working rules

**Always work prototype graphics into anything the player actually sees.** No grey rectangles, no
"TODO art", no placeholder bars. If a player will look at it, draw it properly the first time —
the controls sheet and the bug list are the standard: real key caps, a drawn mouse, a wood-and-paper
card. A feature that is 95% done and looks like a spreadsheet reads as broken, not as unfinished.

**Debug and test; do not guess.** Measure, do not infer. A model of the right answer disagrees with
reality about as often as it agrees — when that happens, find out which one is wrong before
explaining the discrepancy. Prefer the smallest measurement that could possibly be wrong.

**Never let the harness lie to the game.** Fixtures that pin a body, freeze a clock, or re-seed the
RNG produce confident wrong numbers rather than failures. If a test needs the world arranged in an
impossible state, say so in the test and check the arrangement is what the number is about.

**Comments explain why, not what.** The measurement behind a number belongs next to the number. A
comment that restates the code is noise; a comment that records what was tried and measured is the
most valuable thing in the file.

**Correct the record.** When a test fails and the port/code is right, the expectation was wrong —
say so plainly and fix the expectation. When a comment turns out to state something false, fix the
comment in the same commit.

---

## Architecture

**The engine-independent rule.** If a line draws, reads input, or touches the platform, it is not
part of the simulation. Everything else is. This is the line the Unity port forces, and it is the
reason the JavaScript is split the way it is.

**`Depths.Core` targets `netstandard2.1`, never `net9.0`.** Unity 6 runs .NET Standard 2.1; a net9.0
library compiles, tests, and then fails to load in the editor. `LangVersion` is **9.0** for the same
reason — it is what Unity 6 compiles, so `dotnet build` is a real check on shippability. Consequences
already paid for: no file-scoped namespaces, and `Math.Round` is not a compile-time constant.

**`Math.hypot` is not `sqrt(x*x+y*y)`.** V8 scales by exponent; the two differ in the last ulp
(measured 1.7e-16 relative). Cross-language float comparisons must be quantised — 1e-6 — never
bit-exact. Same for `Math.round` vs `.NET Math.Round`, which is half-to-even.

**Parity over assertion.** When porting, generate expected values by running the *original*, and
compare against those. A plausible expectation is worth nothing: the mulberry32 test shipped an
invented value, failed, and proved the port was right all along.

---

## Current state

- `depths.html` — 9.5KB shell loading ten modules from `src/`. Playable, double-clickable.
- `src/99-tests.js` — 94 checks, seeded, deterministic. **All 94 must pass at every commit.**
- `csharp/Depths.Core` + `csharp/Depths.Tests` — 19 checks. `dotnet test` from `csharp/`.
- `src/` is the reference implementation and stays alive. Features are designed and playtested here
  first, because it is the only artifact the player can actually run, and then ported.

## Approved work

Tier 1 + the item system, built in this order:

1. **Content registry** — everything addressable by stable string id, validated, mod-overlaid.
2. **Seed system** — display, enter, share, replay. Plus **RNG stream separation**: run-defining
   randomness and cosmetic jitter draw from *different* streams, so adding an enemy behaviour cannot
   shift the dungeon and invalidate an existing seed. This is the part that is easy to skip and
   impossible to retrofit.
3. **Item system** — integral, not a bolt-on. Data-driven effects via a named-hook table, so a mod
   can add an item that changes a number without writing code.
4. **Mod support** — falls out of the registry. JSON definitions from a folder, isolated failures.
