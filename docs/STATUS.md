# Status

The handoff file. Any session (including an unattended or scheduled one) starts here and updates
it before finishing. Keep it under a page; history goes in `docs/history/`.

_Last updated: 2026-10-06, session 1 (cleanup and freeze)._

## Where things are

- **JS (`depths.html`, `src/`)**: frozen reference, **not yet tagged** - `js-final` is set after the
  Brunch guard-leash fix. Playable. Tests and the parity page load only with `?test` / `?parity`.
  Suite: 239/239. `.erify.ps1` is fully green (about 3 minutes).
- **C# (`csharp/`)**: deterministic core, netstandard2.1. Ported: RNG, balance, world gen, spawn plan,
  room phase + Descend, player phase, pack assembly, `BrunchArcSlot`. Not ported: Brunch/enemy
  movement in `tickBodies`, `tickProjectiles` (14 cases banked in `tools/proj-parity.js`).
- **Unity**: no project yet. Target Unity 6.3 LTS.

## Plan

| # | Session | Effort | State |
|---|---|---|---|
| 1 | Cleanup and freeze the JS | Low | done (committed); `js-final` tag waits on the Brunch leash fix |
| 2 | Unity skeleton: project layout, Depths.Core as a package, Input System, AudioMixer groups | Medium | next |
| 3 | Main menu (Start / Options / Quit); options = key binds with layout presets (QWERTY, AZERTY, QWERTZ, Dvorak) + volume sliders | Medium | |
| 4 | Finish the port: `tickBodies`, then `tickProjectiles`; allocation-free hot paths | High | |
| 5 | Switch C# to 60 Hz, per-second units, re-baseline | Medium | |
| 6 | New sound engine: event-based, sample assets, mixer | Medium | |

## Open questions / known issues

- **Brunch guard leash**: a comment in `tickBodies` describes `BRUNCH_GUARD_LEASH` (packs stop
  guarding a shooter the player is far from), but the constant does not exist and the code never
  checks distance. Reported bug "Brunch don't chase" may still be live. Needs a Medium-effort check.
- Gate fixed 2026-10-06: one port (8791) for the gate and every `tools/` script; Playwright
  `waitForFunction` options passed third; node stderr no longer aborts the gate before it reports.
  The audio hang (238/239) was `whenAudible`/`whenIdle` never polling - fixed in 0cf88f4.
- Unity 6000.3.25f1 runs in batchmode on this machine (licence resolves; a new project takes ~10 min).
- Node is installed at `C:\Program Files\nodejs` (2026-10-06); shells opened before that need it
  added to PATH.
- Headless Chromium on Linux fails one drawing test (descent banner legibility), most likely a font
  difference; it is not a game change. Re-check on the Windows gate.
