# Status

The handoff file. Any session (including an unattended or scheduled one) starts here and updates
it before finishing. Keep it under a page; history goes in `docs/history/`.

_Last updated: 2026-10-06, autonomous session (sweep, playtest bot, Brunch variants, Unity skeleton + menu).
Full write-up: `docs/report-2026-10-06.html`._

## Where things are

- **JS (`depths.html`, `src/`)**: frozen reference, **not yet tagged** - `js-final` waits on the owner's
  Brunch choice (A / B / A+, playable as `?brunch=`). Suite 246/246. `.\verify.ps1` green (~3 min).
- **C# (`csharp/`)**: deterministic core, netstandard2.1, also a Unity local package (`com.depths.core`,
  build output in `obj~`/`bin~`). 286/286. Ported: RNG, balance, world gen, spawn plan, room phase +
  Descend, player phase, pack assembly, `BrunchArcSlot`. Not ported: `tickBodies`, `tickProjectiles`.
- **Unity (`unity/`)**: 6000.3.25f1 project. Main menu (Start / Options / Quit), Options = layout
  presets + per-key rebinding + Master/Music/SFX sliders on a generated mixer, all saved. Game scene
  is a placeholder that draws a Depths.Core floor. 9/9 EditMode tests. Built player verified by screenshot.
- **Playtest bot**: `tools/playtest.js` + `tools/playtest-report.js`; baseline worktree `ab/baseline`
  (tag `pre-features-2026-10-06`). See CONVENTIONS "Gameplay changed?".

## Unity commands (batch mode, no editor window needed)

- Regenerate scenes/mixer/settings: `Unity -batchmode -nographics -projectPath unity -executeMethod Depths.Unity.EditorTools.Bootstrap.Run -quit`
- Tests: `Unity -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults r.xml`
- Build: `... -executeMethod Depths.Unity.EditorTools.BuildTools.BuildWindows -quit` -> `unity/Build/Depths.exe`
- Screenshot a view: `unity/Build/Depths.exe -screen-fullscreen 0 -depthsShot out.png -depthsView menu|controls|audio|game`

## Plan

| # | Session | Effort | State |
|---|---|---|---|
| 1 | Cleanup and freeze the JS | Low | done; `js-final` after the Brunch choice |
| 2 | Unity skeleton: layout, Depths.Core as a package, Input System, AudioMixer groups | Medium | done |
| 3 | Main menu: Start / Options / Quit; key binds with presets + rebinding; volume sliders | Medium | done (gamepad + display options later) |
| 4 | Finish the port: `tickBodies`, then `tickProjectiles`; allocation-free hot paths | High | next, after the Brunch choice |
| 5 | Switch C# to 60 Hz, per-second units, re-baseline | Medium | |
| 6 | New sound engine: event-based, sample assets, mixer (groups exist) | Medium | |

## Open questions / known issues

- **Owner decisions**: Brunch A/B/A+; heart values (texts fixed, numbers unchanged); difficulty
  (bot never dies: healing ~ damage per floor); items whose effect is missing (Brass Compass INT,
  Hunter's Mark reveal). Details in the report.
- **Port hazards found by the sweep** (fix during session 4): `Intercept.cs` is an older aiming model
  (its test checks a stale hash); `TickRoom` will descend every tick once rooms exist (exit stays under
  the player); `DoorPassable`/`ClampPlayer` are stubs; `AssemblePacks` allocates per tick;
  `TickOrderTests` phase log and `ProjectileRecordTests` are tautologies.
- JS, left as found (reference behaviour): Brunch scan throttle is dead (`pickShield` every tick); stale
  `packSlot` skews a shrinking wall; boss can spawn on the player; body-contact hitbox is 20px off the
  shot hitbox; Lab eats the first S press.
- Node is at `C:\Program Files\nodejs`; shells opened before 2026-10-06 need it on PATH.
- Headless Chromium on Linux fails one drawing test (font); fine on the Windows gate.
