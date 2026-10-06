# Status

The handoff file. Any session (including an unattended or scheduled one) starts here and updates
it before finishing. Keep it under a page; history goes in `docs/history/`.

_Last updated: 2026-10-06 (C# playtest bot; before that Brunch A+, `js-final`, placeholder items). The previous
session's write-up is `docs/report-2026-10-06.html` (its numbers are from mid-session: 380 C# tests)._

## Where things are

- **JS (`depths.html`, `src/`)**: frozen, **tagged `js-final`** (Brunch A+ is the default; A and B stay
  behind `?brunch=` for comparison). Suite 246/246 (headless Linux: 245, the known font test).
- **C# (`csharp/`)**: deterministic core, netstandard2.1, also a Unity package (`com.depths.core`).
  516/516, 148 parity rows. The whole run loop is ported and checked against JS recordings. After
  `js-final`, C# differs from the JS on purpose: whole-number HP, the regenerating heart and the
  placeholder items (below).
  `Balance.JsReference` restores the JS damage rules for parity tests that record player HP.
- **Unity (`unity/`)**: 6000.3.25f1. Main menu + Options (layout presets, rebinding, volume). Plays a
  real seeded run with placeholder shapes: pause (Esc), death summary, floor banner.
- **Playtest bot (C#)**: `csharp/Depths.Playtest` plays the real core (6 seeds x 3 profiles x 20 min
  in about 5 s) and writes `playtest/<label>.json`:
  `DOTNET_ROLL_FORWARD=Major dotnet run --project csharp/Depths.Playtest -c Release -- play --label X
  [--seeds 1,7] [--profiles novice,skilled] [--minutes 20] [--brunch A+]`, then `... -- report X [Y]`
  (Markdown, also saved as `playtest/report-X[-vs-Y].md`). A/B against another commit (one that
  contains the bot: this branch from 22ebe4c on): `git worktree add ../depths-base <commit>`, play
  `--label baseline` with `--project ../depths-base/csharp/Depths.Playtest` from THIS worktree's
  root (its JSON lands here and its header names its commit), play `current`, `report baseline
  current`, then `git worktree remove --force ../depths-base` (steps in CONVENTIONS "Gameplay changed?").
  Numbers (2026-10-06, after the exit-crash fix a264e45, which the bot found): at 20 min, 17 of 18 runs
  time out on floor 7 (about 3 min a floor), 1 death (average/42, floor 6), 0 stuck, 0 errors, damage
  9.8 vs healing 10.3 hp per floor. The 20-min default caps the median floor, so its A/B signal is
  deaths, damage/healing per floor and boss seconds; use `--minutes 60` for difficulty: median floor
  17 (novice 15.5), 6 deaths (5 novice, between floors 15 and 17), damage 14.5 = healing 14.5 per
  floor, boss 46 s. Q-when-low almost never heals: the bot takes every item, so Tin Cup is usually
  replaced (60 min: 24 presses, 3 heals). The JS bot (`tools/playtest.js`, baseline tag
  `pre-features-2026-10-06`) plays only the frozen JS.
- Linux cloud sessions: `apt-get install dotnet-sdk-10.0`, then `DOTNET_ROLL_FORWARD=Major dotnet test csharp/Depths.sln`.
  `verify.ps1` is Windows-only (backslash paths): under Linux `pwsh` it stops at step 2.

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
| 5 | Switch C# to 60 Hz, per-second units, re-baseline | Medium | next |
| 6 | New sound engine: event-based, sample assets, mixer | Medium | started: `SoundEngine.Play(name, pan)`, 12 voices, Sfx pool. Next: compressor, samples, music |

## Owner decisions

- **Brunch**: A+ (decided 2026-10-06).
- **Heart values** (done 2026-10-06, C# + Unity): whole HP, 1 HP = half a heart, 8 at the start.
  Shooter 2, gunner 4, contact 1, Warden contact 2, shell 2, sweep 3 (my rounding of 2 x 1.4 - owner
  to confirm). Armour takes 0.6x a normal hit, rounded down, min 1 (so contact still costs 1); the
  Warden hits armour at full weight; what armour cannot cover spills to hearts at full weight. Loot
  splits each band: 9% half heart, 9% heart, 4% half armour, 4% armour (a quarter less healing per
  kill, same drop rate). Unity: placeholder heart sprites for pickups and a heart row above the room.
  Not yet checked in the Unity editor (no Unity in the cloud session).
- **Regenerating heart** (done 2026-10-06, owner's request): the 4th starting heart is its own layer,
  6 red + 2 regen = 8. Damage order: armour, regen, red. Refills in a fight only: 4s without a hit,
  then +1 HP, then +1 HP per second; an empty room pauses the clock, only a hit resets it. Pickups,
  Tin Cup and Vigor touch red only. Rose-violet in the HUD. Dials: `Balance.RegenHp`, `RegenDelay`,
  `RegenStep`. Owner questions: should an empty room reset the clock instead of pausing it? Should
  it also refill between fights (much more healing)?
- **Difficulty / healing**: deferred until the mechanics are done. Owner's direction: a non-linear
  curve (hard start, easier-but-not-easy middle, hard end); scarce pickups; different enemies per
  area and buffed enemies later on; more complex rooms; every part of the kit (items, weapons,
  consumables) load-bearing. Bot data (C#, 60 min): healing = damage per floor (14.5 hp),
  median floor 17, novices die on floors 15-17.
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
