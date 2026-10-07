# Status

The handoff file. Any session (including an unattended or scheduled one) starts here and updates
it before finishing. Keep it under a page; history goes in `docs/history/`.

_Last updated: 2026-10-07 (the difficulty curve, `Curve.cs`; before that: C# switched to 60 Hz with per-second units; before that: the won-fight regen refill, the C# playtest bot, Brunch A+, `js-final`). The previous
session's write-up is `docs/report-2026-10-06.html` (its numbers are from mid-session: 380 C# tests)._

## Where things are

- **JS (`depths.html`, `src/`)**: frozen, **tagged `js-final`** (Brunch A+ is the default; A and B stay
  behind `?brunch=` for comparison). Suite 246/246 (headless Linux: 245, the known font test).
- **C# (`csharp/`)**: deterministic core, netstandard2.1, also a Unity package (`com.depths.core`).
  582/582, 148 parity rows. The whole run loop is ported and checked against JS recordings.
  **The game ticks at 60 Hz** (`Balance.TickHz`); every time-based dial is in seconds or per-second
  units. The suite runs at 210 Hz (assembly `[TickRate]` in `TickRate.cs`), where every dial is
  bit-identical to the JS one, so the parity rows are untouched; `RateTests` (23) compares key
  behaviours at both rates and checks every rate dial by unit. At 60 Hz shells and body contact are
  cut into 4 sub-steps (`Substeps`). After
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
  Default matrix (60 Hz, 2026-10-06): at 20 min 17 of 18 runs time out on floor 7 (3.02 min per
  floor left by the exit), 1 death, 0 stuck, 0 errors, damage 9.8 vs healing 10.3 hp a floor, boss
  34 s, accuracy 84%. The 20-min default caps the median floor; use `--minutes 60` for difficulty:
  median floor 17, 3 deaths (one per profile), damage 14.9 vs healing 14.9 a floor, boss 46 s. 18 runs
  are too few for deaths: the wide matrix (seeds 101-300) is the reference, see the 60 Hz entry below.
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
| 5 | Switch C# to 60 Hz, per-second units, re-baseline | Medium | done (see the 60 Hz entry below) |
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
- **60 Hz switch** (2026-10-06). Wide matrix, seeds 101-300 x 3 profiles, 600 runs a cell, 210 (the JS
  rate, reference) / 60 Hz. 20 min: min/floor 3.1 / 3.1, damage 10.5 / 10.2, healing 11.0 / 10.7, boss
  37.1 / 36.8 s, accuracy 84 / 84%, kills/min 28.3 / 28.4, deaths 14 / 8. 60 min: min/floor 3.6 /
  3.5, damage 15.2 / 14.9, healing 15.2 / 15.0, boss 46.5 / 46.4 s, accuracy 85 / 86%, kills/min
  28.0 / 28.2, deaths 105 / 73 (novice 68 / 58, average 24 / 9, skilled 13 / 6). Runs diverge between
  rates, so deaths are not paired. What 60 Hz still does differently, each measured:
  - **Deaths are 30% lower at 60 min, and the cause is not identified.** Part is run-to-run spread:
    200 Hz, physically almost the JS rate, gives 93 deaths. Across rates deaths fall with the tick
    (210: 105, 200: 93, 120: 89, 90: 87, 60: 73) and so does enemy shot damage an hour (108.3,
    108.8, 107.2, 103.7, 102.3), while net HP a floor stays equal. Enemy shot damage is 5.5% lower at
    60 Hz. Not the cause, each measured: enemy fire rate (now carried, exact), shell sampling (4
    sub-steps), the shell hit rate against a strafing player in a controlled test (equal within noise),
    sweeping shells against the moving player hitbox (enemy shot damage unchanged, so not kept) and
    leading the shell's late tick in the aim (also unchanged, not kept).
  - Q presses when low fall with the deaths: 1.5 -> 1.0 a run at 60 min, 0.3 -> 0.2 at 20 min. A press
    needs HP at 1-2, so it counts the same near-death moments, and a few runs carry most presses.
  - Each wait in whole ticks (`Sec`) is within half a tick, 8 ms. A lunge ends up to one step past its
    mark (14.7 px at 60 Hz against 4.2).
  Corrected off the JS rate only (210 stays exact): knock slides (exact coast distance), held fire and
  enemy and Warden cooldowns (each carries its overrun; the Bolt was 2% slow, the Beam 7%, enemy fire
  2%), shells and body contact (4 sub-steps at 60 Hz; contact damage 145 -> 154 an hour, 152 at
  210). Owner: decide whether 60 Hz difficulty wants a retune.
- **Difficulty curve** (2026-10-07, C# only; owner to playtest). Owner's direction: a non-linear
  curve (hard start, easier-but-not-easy middle, hard end), scarce pickups, buffed enemies later on,
  every part of the kit load-bearing. Built as seven named stages in `csharp/Depths.Core/Curve.cs`
  laid over the depth ladder: `start` (1) and `peak` (2) are a bump while the player has no kit, `soft`
  (3) and `middle` (4-10) a dip, `ramp` (11) and `climb` (12) a gate before the last area, `end` (13+)
  the last area's mix and the exponential ladder. Dials per stage, each with its why in the file:
  `Tough`, `Rate` (still under `DepthRateCap`), `Bodies`, `Pack`, `Heavy` (gunner chance), `Drops`
  (kill drop chance x). No silent damage changes: a hit is worth the same on every floor. The targets
  live in the same file (`Curve.Targets`). Tests run with the curve off (assembly `[CurveOn(false)]`), so
  the 148 parity rows keep the JS ladder.
  Bot: `play --curve off` plays the JS ladder; `play --dial middle.Drops=0.15,end.Tough=1.2` tries a
  value without a rebuild (recorded in the JSON header, flagged by `report`); `curve <label>` prints one
  ok/MISS line per target with its rubric number, exits 2 on a miss, and writes
  `playtest/curve-<label>.md`. The bot records damage and healing by source per floor.
  Bot, 60 Hz, 60 min, seeds 101-300, 600 runs (hazard = deaths / floor entries, floors reached by 30+ runs):

  | | START before > after | MIDDLE | END | HP leaving 2 | median floor | dmg / heal a floor, START and MIDDLE, after |
  |---|---|---|---|---|---|---|
  | novice | 1.26% > 12.3% | 0.45% > 2.72% | 4.80% > 28.8% | 8 > 5 | 17 > 12 | 13.2 / 11.2, 12.6 / 12.7 |
  | average | 0.00% > 4.28% | 0.00% > 0.85% | 0.89% > 11.2% | 8 > 6 | 17 > 17 | 9.6 / 8.4, 9.0 / 9.3 |
  | skilled | 0.00% > 2.26% | 0.06% > 0.54% | 0.50% > 4.98% | 8 > 7 | 17 > 17 | 7.8 / 6.9, 7.8 / 8.1 |

  Novice hazard on floors 11-16: 16, 21, 15, 41, 37, 58% (16 is reached by 26 novices); 41% of novices
  reach floor 13. What moved what: `Drops` 0 on floors 1-2 makes the start cost hearts (HP leaving 2:
  8 > 5); the gunner's 4 HP shell decides deaths and skill barely dodges it, so fewer gunners (`Heavy`
  -0.3) plus faster, dodgeable shooters (`Rate` 1.3) separate skilled from average; the dip is softer
  bodies, one body less, fewer packs and gunners and scarce drops (x0.11); the gate is `Tough` 1.3.
  **Owner decision, rubric 5 for MIDDLE:** "healing a floor below damage a floor" is missed by 0.2-0.3
  HP for every profile. Over the band, healing minus damage is exactly the change in HP and armour, and
  the start (by design) sends players into floor 3 at about 5 of 8, so a middle that never gives any of
  it back has to drain an already hurt player; every drop rate that did that broke the middle's death
  ceilings. Two restatements, both met by this curve: (a) "the middle gives back no more than the start
  took plus new max HP" (survivors recover 2.8 / 3.1 / 2.8 against 4.0 / 3.4 / 3.2 allowed, novice /
  average / skilled), or (b) "pickup healing a floor below damage a floor" (2.5 / 2.2 / 2.0 against
  12.6 / 9.0 / 7.8). Pick one, or keep the original and accept a harder middle.
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
