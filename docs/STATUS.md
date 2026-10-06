# Status

The handoff file. Any session (including an unattended or scheduled one) starts here and updates
it before finishing. Keep it under a page; history goes in `docs/history/`.

_Last updated: 2026-10-06 (C# switched to 60 Hz with per-second units; before that: the won-fight regen refill, the C# playtest bot, Brunch A+, `js-final`). The previous
session's write-up is `docs/report-2026-10-06.html` (its numbers are from mid-session: 380 C# tests)._

## Where things are

- **JS (`depths.html`, `src/`)**: frozen, **tagged `js-final`** (Brunch A+ is the default; A and B stay
  behind `?brunch=` for comparison). Suite 246/246 (headless Linux: 245, the known font test).
- **C# (`csharp/`)**: deterministic core, netstandard2.1, also a Unity package (`com.depths.core`).
  556/556, 148 parity rows. The whole run loop is ported and checked against JS recordings.
  **The game ticks at 60 Hz** (`Balance.TickHz`); every time-based dial is in seconds or per-second
  units. The suite runs at 210 Hz (assembly `[TickRate]` in `TickRate.cs`), where every dial is
  bit-identical to the JS one, so the parity rows are untouched; `RateTests` (20) compares key
  behaviours at both rates. At 60 Hz a shell moves in 4 sub-steps (`ShellSubsteps`). After
  `js-final`, C# differs from the JS on purpose: whole-number HP, the regenerating heart and the
  placeholder items (below).
  `Balance.JsReference` restores the JS damage rules for parity tests that record player HP.
- **Unity (`unity/`)**: 6000.3.25f1. Main menu + Options (layout presets, rebinding, volume). Plays a
  real seeded run with placeholder shapes: pause (Esc), death summary, floor banner.
- **Playtest bot (C#)**: `csharp/Depths.Playtest` plays the real core at 60 Hz (6 seeds x 3 profiles x
  20 min in about 2 s; 4.8 s at `--hz 210`) and writes `playtest/<label>.json` (header `tickHz`):
  `DOTNET_ROLL_FORWARD=Major dotnet run --project csharp/Depths.Playtest -c Release -- play --label X
  [--seeds 1,7] [--profiles novice,skilled] [--minutes 20] [--brunch A+] [--hz 60]`, then `... -- report X [Y]`
  (Markdown, also saved as `playtest/report-X[-vs-Y].md`). A/B against another commit (one whose
  `csharp/Depths.Playtest` has the `StampGitCommit` target): `git worktree add ../depths-base <commit>`,
  play `--label baseline` with `--project ../depths-base/csharp/Depths.Playtest` from THIS worktree's
  root (its JSON lands here and its header names its commit), play `current`, `report baseline
  current`, then `git worktree remove --force ../depths-base` (steps in CONVENTIONS "Gameplay changed?").
  A baseline from before the 60 Hz switch (8f322e6 and older) plays at 210: give the current side `--hz 210`.
  Default matrix at 60 Hz: 18 of 18 time out on floor 7, 0 deaths, damage 9.7 vs healing 10.3 hp a
  floor, boss 35 s. The numbers below are the 210 Hz baseline. Default-matrix numbers (2026-10-06, with the won-fight refill): at 20 min 16 of 18 runs time out on
  floor 7 (about 3 min a floor), 2 deaths, 0 stuck, 0 errors, damage 9.8 vs healing 10.3 hp per
  floor. The 20-min default caps the median floor; use `--minutes 60` for difficulty: median floor 17
  (novice 15), 7 deaths (5 novice), damage 14.2 vs healing 14.1 per floor, boss 45 s. 18 runs are too
  few for deaths: the wide matrix (seeds 101-300) is the reference, see the regen heart entry below.
  Q-when-low almost never heals: the bot takes every item, so Tin Cup is usually replaced. The JS bot (`tools/playtest.js`, baseline tag
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
| 5 | Switch C# to 60 Hz, per-second units, re-baseline | Medium | done (branch `hz60-a`, see below) |
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
  then +1 HP, then +1 HP per second; an empty room pauses the clock (owner: "stop the clock after the
  fight"), only a hit resets it. Winning a fight (clearing a room whose wave spawned bodies,
  `Room.Fought`) refills it fully (owner's call);
  start, item and secret rooms never do. Pickups, Tin Cup and Vigor touch red only. Rose-violet in
  the HUD. Dials: `Balance.RegenHp`, `RegenDelay`, `RegenStep`. Bot A/B of the clear refill (seeds
  101-300, 600 runs a side, paired sign test): deaths 35 -> 14 at 20 min (23 saved, 2 newly died,
  p = 2e-5) and 155 -> 105 at 60 min (75 / 25, p = 6e-7). Significant overall and for novices (-35%,
  p = 2e-5; median floor 16 -> 17); average and skilled point the same way (-27%, -24%) but are not
  significant alone (p = 0.06, 0.29). It pays ~2 HP a floor, mostly HP the clock
  would have refilled early in the next fight, so net healing barely moves (+0.1 to +0.4 HP a floor),
  but every fight starts with the shield up. A real easing, clearest for novices.
- **60 Hz switch** (2026-10-06): wide matrix, seeds 101-300 x 3 profiles, 210 vs 60 Hz. 20 min:
  min/floor 3.1 / 3.1, damage 10.5 / 10.0, healing 11.0 / 10.5, boss 37.1 / 37.8 s, accuracy 84 / 85%,
  kills/min 28.3 / 28.0, deaths 14 / 10. 60 min: min/floor 3.6 / 3.6, damage 15.2 / 14.5, healing
  15.2 / 14.6, boss 46.5 / 47.3 s, accuracy 85 / 86%, kills/min 28.0 / 27.9, deaths 105 / 74 (novice
  68 / 56, average 24 / 7, skilled 13 / 11). Runs diverge between rates, so deaths are not paired; the
  drop follows hostile shells landing less often (shot damage about 9% lower an hour; contact 3%):
  per-tick hit sampling with a player that moves 4.9 px a tick. Sub-stepping shells halved that gap;
  bodies (a lunge moves 14.7 px a tick) and the player are not sub-stepped. Owner: decide whether
  60 Hz deaths need a retune or body sub-steps (measure with the wide matrix).
- **Difficulty / healing**: deferred until the mechanics are done. Owner's direction: a non-linear
  curve (hard start, easier-but-not-easy middle, hard end); scarce pickups; different enemies per
  area and buffed enemies later on; more complex rooms; every part of the kit (items, weapons,
  consumables) load-bearing. Bot data (C#, 60 min, seeds 101-300, with the won-fight refill):
  healing = damage per floor (15.2 hp), median floor 17, 105 of 600 runs die (68 of them novice).
- **Items**: overhaul later. Placeholders (C# only) until then: Hunter's Mark marks the room for 5s
  (+50% damage taken); Brass Compass opens the fake wall when carried into its room. Lantern Friend is
  still `unimplemented` (not loot).

## Known issues

- 60 Hz in Unity is unverified (no editor in the cloud): `GameView` steps at `Balance.TickHz`, the
  death clock and floor banner are in seconds; the screenshot demo's script still counts frames.
- C# leftovers: `Intercept.cs` is unused (older aiming model, stale test hash); `Pickup.shown` is set
  by Hunter's Mark and read by nothing; `run.unlocked` from the compass is not ported (nothing read it).
- JS, left as found (reference behaviour): Brunch scan throttle is dead (`pickShield` every tick); stale
  `packSlot` skews a shrinking wall; boss can spawn on the player; body-contact hitbox is 20px off the
  shot hitbox; Lab eats the first S press.
- Node is at `C:\Program Files\nodejs` on the owner's machine; shells opened before 2026-10-06 need it on PATH.
