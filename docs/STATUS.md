# Status

The handoff file. Any session (including an unattended or scheduled one) starts here and updates
it before finishing. Keep it under a page; history goes in `docs/history/`.

_Last updated: 2026-10-06, session 1 (cleanup and freeze)._

## Where things are

- **JS (`depths.html`, `src/`)**: frozen reference, **not yet tagged** - `js-final` is set after the
  Brunch guard-leash fix. Playable. Tests and the parity page load only with `?test` / `?parity`.
  Suite: 238/239 (see known issues).
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
- **JS test hang (pre-existing at 2f516b2)**: "a suspended audio context can always be brought
  back" never settles in headless Edge and fails on its 240s watchdog. Same result on HEAD and
  the cleanup tree (238/239). Most likely from the AudioContext-on-gesture change. Fix: Low/Medium.
- **verify.ps1 tooling bugs** (the gate cannot go green until fixed, Low):
  `p.waitForFunction(expr, {timeout})` passes options as `arg` (Playwright takes them third), so
  the 30s default applies and step 6 always times out; the same bug is in `tools/parity-audit.js`.
  Steps 6c/6d need a server on 8791 but the gate starts one on 8731 (`constant-audit.js` hard-codes
  8791; `parity-audit.js` honours `DEPTHS_URL`). Run by hand on 2026-10-06: parity 0 differences
  across 5 tables, constants 47 compared / 0 mismatch.
- Node is installed at `C:\Program Files\nodejs` (2026-10-06); shells opened before that need it
  added to PATH.
- Headless Chromium on Linux fails one drawing test (descent banner legibility), most likely a font
  difference; it is not a game change. Re-check on the Windows gate.
