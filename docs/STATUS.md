# Status

The handoff file. Any session (including an unattended or scheduled one) starts here and updates
it before finishing. Keep it under a page; history goes in `docs/history/`.

_Last updated: 2026-10-06 (Brunch A+ chosen, `js-final` tagged, placeholder items). The previous
session's write-up is `docs/report-2026-10-06.html` (its numbers are from mid-session: 380 C# tests)._

## Where things are

- **JS (`depths.html`, `src/`)**: frozen, **tagged `js-final`** (Brunch A+ is the default; A and B stay
  behind `?brunch=` for comparison). Suite 246/246 (headless Linux: 245, the known font test).
- **C# (`csharp/`)**: deterministic core, netstandard2.1, also a Unity package (`com.depths.core`).
  394/394, 148 parity rows. The whole run loop is ported and checked against JS recordings. After
  `js-final`, C# may differ from the JS on purpose: so far only the placeholder items (below).
- **Unity (`unity/`)**: 6000.3.25f1. Main menu + Options (layout presets, rebinding, volume). Plays a
  real seeded run with placeholder shapes: pause (Esc), death summary, floor banner.
- **Playtest bot**: `tools/playtest.js` + `tools/playtest-report.js`; baseline tag `pre-features-2026-10-06`.
- Linux cloud sessions: `apt-get install dotnet-sdk-10.0`, then `DOTNET_ROLL_FORWARD=Major dotnet test csharp/Depths.sln`.

## Unity commands (batch mode, no editor window needed)

- Regenerate scenes/mixer/settings: `Unity -batchmode -nographics -projectPath unity -executeMethod Depths.Unity.EditorTools.Bootstrap.Run -quit`
- Tests: `Unity -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults r.xml`
- Build: `... -executeMethod Depths.Unity.EditorTools.BuildTools.BuildWindows -quit` -> `unity/Build/Depths.exe`
- Screenshot a view: `unity/Build/Depths.exe -screen-fullscreen 0 -depthsShot out.png -depthsView menu|controls|audio|game`

## Plan

| # | Session | Effort | State |
|---|---|---|---|
| 1 | Cleanup and freeze the JS | Low | done, `js-final` tagged |
| 2 | Unity skeleton | Medium | done |
| 3 | Main menu: Start / Options / Quit, key binds, volume | Medium | done (gamepad + display options later) |
| 4 | Finish the port | High | done |
| 5 | Switch C# to 60 Hz, per-second units, re-baseline | Medium | next (heart values first, see below) |
| 6 | New sound engine: event-based, sample assets, mixer | Medium | started: `SoundEngine.Play(name, pan)`, 12 voices, Sfx pool. Next: compressor, samples, music |

## Owner decisions

- **Brunch**: A+ (decided 2026-10-06).
- **Heart values**: proposal awaiting the owner. Integer HP, 1 HP = half a heart, 8 HP base; round
  enemy damage (shooter 1.8->2, gunner 3.6->4, Warden shell 1.44->1, sweep 2.02->2; contact 1, Warden
  contact 2 unchanged). Heart pickup and Tin Cup stay a fixed +2 HP. Best done together with session 5.
- **Difficulty / healing**: deferred until the mechanics are done. Owner's direction: a non-linear
  curve (hard start, easier-but-not-easy middle, hard end); scarce pickups; different enemies per
  area and buffed enemies later on; more complex rooms; every part of the kit (items, weapons,
  consumables) load-bearing. Bot data: healing ~ damage per floor, bots reach floors 13-18.
- **Items**: overhaul later. Placeholders (C# only) until then: Hunter's Mark marks the room for 5s
  (+50% damage taken); Brass Compass opens the fake wall when carried into its room. Lantern Friend is
  still `unimplemented` (not loot).

## Known issues

- C# leftovers: `Intercept.cs` is unused (older aiming model, stale test hash); `Pickup.shown` is set
  by Hunter's Mark and read by nothing; `run.unlocked` from the compass is not ported (nothing read it).
- JS, left as found (reference behaviour): Brunch scan throttle is dead (`pickShield` every tick); stale
  `packSlot` skews a shrinking wall; boss can spawn on the player; body-contact hitbox is 20px off the
  shot hitbox; Lab eats the first S press.
- Node is at `C:\Program Files\nodejs` on the owner's machine; shells opened before 2026-10-06 need it on PATH.
