# Conventions

The standing rules. Short on purpose: every agent reads this file first, so it holds only what must
not be forgotten. The reasoning and the measurements behind each rule live in `docs/history/`
(`conventions-archive.md` is the full previous version of this file, verbatim).

## Objective

**A game that is fun, fluid, optimised, and feels great in the hand and on the screen.**

- Feel is a tuning problem before it is a content problem: a number, a curve, a telegraph, a frame of latency.
- Optimised means measured, not assumed. A profile is evidence; a hunch is not.
- Difficulty comes from rate and density, never from a threat the player cannot read in time.

## Direction (decided 2026-10-06)

- **C# / Unity is the main game.** Target: Unity 6.3 LTS.
- **The JavaScript is frozen** at git tag `js-final`. It is the reference the remaining port is
  checked against, and nothing new is built in it. Once the port matches it fully it moves to `legacy/`.
- The port is complete. **C# runs at 60 Hz** (`Balance.TickHz`, `GameHz`); the parity rows stay at
  the JS rate (`JsHz` 210), where every converted dial is bit-identical to the value they were
  recorded with. See "Time is in seconds" below.
- Sound, key binds and menus are built in Unity, not in the JS.
- Status and next steps: `docs/STATUS.md`. Port boundary: `PORTED.md`.

## Hard constraints

- **Never commercialised.** Shared free with friends. Unity Personal licensing is settled; do not reopen.
- **`depths.html`, `src/*.js` and the docs are UTF-8, LF-only, no BOM.** The canary is a U+00B7
  MIDDLE DOT in the user-visible strings of `70-view.js` / `80-ui.js`. If it is ever preceded by a
  U+00C2 the file was double-encoded. (Do not write that pair literally anywhere, including here.)
  PowerShell: write with `New-Object System.Text.UTF8Encoding($false)`.
- **git: `core.autocrlf=false`, `core.eol=lf`.** Do not "fix" these.
- **`Depths.Core` targets `netstandard2.1`, `LangVersion` 9.0** — what Unity compiles. No file-scoped
  namespaces; `Math.Round` is not a compile-time constant.

## Working rules

- **Gameplay changed? Playtest it A/B before committing.** The C# bot measures any commit whose
  `csharp/Depths.Playtest` stamps its commit (its `.csproj` has the `StampGitCommit` target); older
  baselines are not supported. `<baseline commit>` is the commit to compare against, usually the one
  before the change. From the change's worktree root (Linux: prefix each `dotnet` with
  `DOTNET_ROLL_FORWARD=Major`):
  0. A baseline from before the 60 Hz switch plays at 210 Hz: add `--hz 210` to step 3 as well, or the
     report mixes the two rates (it warns when they differ).
  1. `git worktree add ../depths-base <baseline commit>`
  2. `dotnet run --project ../depths-base/csharp/Depths.Playtest -c Release -- play --label baseline`
     (the JSON lands in THIS worktree's `playtest/`; its header names the commit it was built from)
  3. `dotnet run --project csharp/Depths.Playtest -c Release -- play --label current`
  4. `dotnet run --project csharp/Depths.Playtest -c Release -- report baseline current` (also saved
     as `playtest/report-baseline-vs-current.md`; it warns if seeds, minutes, tick rate or Brunch differ)
  5. `git worktree remove --force ../depths-base`

  Same `--seeds/--minutes/--brunch` on both sides. The bot is deterministic, so any difference is the
  change, but one changed HP point reroutes a whole run: deaths and floors on the default 18 runs
  cannot tell an effect from chance. For those, play 100+ seeds on both sides (PowerShell:
  `--seeds ((101..300) -join ',')`; bash: `--seeds $(seq -s, 101 300)`) and read the report's
  "Deaths, paired by seed and profile" table (p under 0.05). Use `--minutes 60` for difficulty (20 min caps the
  median floor at 7). It answers "harder, fairer, longer?"; "more fun?" still needs a person. The JS
  bot (`tools/playtest.js`) plays only the JS.
- **Drawing changed? Verify pixels, not data.** Assert on meaning ("the lit heart is at x 43"),
  sample the framebuffer, and pin directions and orders explicitly.
- **Write the test from the specification, never from what the code currently does.**
- **Measure, don't model. When a measurement surprises you, check the fixture first** — it has been
  the fixture far more often than the game.
- **Never let the harness lie.** A test that pins a body, freezes a clock or re-seeds the RNG must say so.
- **Randomised loops must seed per iteration and assert their iterations differ.**
- **Check the polarity of every sampled number** (counts down vs counts up).
- **Presentation clocks tick above every early return** in `update()`.
- **A swinging measurement is not a rate** — raise repetitions until it settles, then assert the rate.
- **Geometry needed by more than one file lives in exactly one place** (e.g. `doorRect`).
- **Test the states code deliberately runs in** (title screen, transitions), not only mid-run.
- **No placeholder-looking UI.** Anything the player sees gets real prototype art the first time.
- **Comments: one or two lines, the why not the what.** Long reasoning goes in `docs/history/`; in
  `src/` a moved comment leaves a gist tagged `[h:<file>-N]` — search `docs/history/src/<file>.md`.
- **Correct the record in the same commit.** One system per commit, green at every commit.
- **A ported behaviour changes in both languages in the same commit** until the JS is retired.

## Architecture

- **Engine-independent line:** if a line draws, reads input or touches the platform, it is not
  simulation. Simulation goes in `Depths.Core`; Unity code only presents and feeds input.
- **No `Math.random` / `UnityEngine.Random` in simulation.** Three seeded streams: `run`, `jitter`, `art`.
- **Cross-language floats compare quantised to 1e-6.** `Math.hypot` ≠ `sqrt(x²+y²)` in the last ulp;
  JS `Math.round` is floor(x+0.5), .NET is half-to-even.
- **Parity over assertion:** expected values are generated by running the original, never written by hand.
- **One-way data flow:** state → systems → view. A draw never mutates simulation state.
- **Stats are derived, additive only:** `value = min(cap, base + Σflat + earned)`. Rates go in named hooks.
- **Content is data; behaviour is a named hook.**
- **Overlays must be exempted from the input suppressor by name**, keys included.
- **No per-tick allocations in C# hot paths** (Unity GC hitches). Reuse lists/arrays.
- **Time is in seconds.** `Balance.TickHz` is the one rate (60 in the game). A duration is `Sec(s)` (or
  `SecF` for a fractional counter), a speed `PerSec(px/s)`, a multiplicative per-tick factor is written
  at the JS tick and converted with `Decay`/`Ease`/`EaseK`, a per-tick roll with `Chance`, a knock
  impulse is scaled by `KnockScale` (its coast distance holds). No literal tick counts in `Depths.Core`,
  and nothing rate-derived in a `static readonly` or `const`. The test suite runs at `JsHz` through the
  assembly `[TickRate]`; a test that wants 60 says `[TickRate(60)]`. `RateTests` compares behaviours
  at both rates and pins the 210 Hz values.

## Routine cleanup pass (when asked)

1. Every number quoted in a comment, re-derived from the code. 2. Dead constants. 3. `Math.random`
in game code (tripwire test). 4. Comments contradicting code. 5. Unused locals/params. 6. Every null
guard: can it actually be null? 7. Tests that compare a value with itself — verify stats by effect.
8. File hygiene: LF, no BOM, no U+00C2.

## Design principles (proposed, open to argument)

1. Items bend the existing verbs; they do not add a third button.
2. Every item must change a decision, not a number you never think about.
3. Tension beats stacking: items should exclude each other.
4. Rarity is a cost, not a size.
5. Nothing goes on the pause screen unless a decision depends on it.

## Current state

- JS suite (`depths.html?test`, frozen reference): **246 checks**.
- C# `Depths.Tests` has **585 checks**, parity-verified against the JavaScript except the 197 in
  `PlaceholderItemTests`, `HeartTests`, `RegenHeartTests`, `ExitPickupTests`, `PlaytestBotTests`,
  `PlaytestToolTests`, `RateTests` and `CurveTests` (C#-only, after `js-final`; their fixtures carry `Category("csharp-only")`, which
  `verify.ps1` skips when it counts parity rows). Parity tests whose recordings take player damage run
  with `Balance.JsReference = true` (the JS damage rules); the whole suite runs at 210 Hz and with the difficulty curve off (assembly `[CurveOn(false)]`; `CurveTests` turns it on).
- `verify.ps1` asserts both numbers above; keep them current.

## Archived design decisions (`docs/history/conventions-archive.md`)

The Warden (boss) · Brunch packs are a wall, not a crowd · the map is a branching tree · floors and
the depth ladder · blink grace · the Scatter falloff (`fNear`) · `startGame()` does not start the game ·
lunger HP 20.25 · parked items · bodies that answer the held gun · areas = enemy mix + palette ·
rooms bigger than the screen and the camera · the bench row · the Warden's bar · HUD top band · the
Lab · player-facing numbers · the Flame · F5 is not a lab key · one frame · world-derived values ·
the separation pass order · performance findings · the port boundary and its drift.
