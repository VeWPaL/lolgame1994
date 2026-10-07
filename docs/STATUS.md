# Status

The handoff file. Any session (including an unattended or scheduled one) starts here and updates
it before finishing. Keep it under a page; history goes in `docs/history/`.

_Last updated: 2026-10-07 (Unity run on the owner's machine: bootstrap, 24 EditMode tests, build, screenshots; 60 Hz and the heart row verified in the player; before that: the difficulty curve, `Curve.cs`, built but off by default pending the owner; before that: C# switched to 60 Hz with per-second units; before that: the won-fight regen refill, the C# playtest bot, Brunch A+, `js-final`). The previous
session's write-up is `docs/report-2026-10-06.html` (its numbers are from the end of that session: 388 C# tests, before 60 Hz and the curve)._

## Where things are

- **JS (`depths.html`, `src/`)**: frozen, **tagged `js-final`** (Brunch A+ is the default; A and B stay
  behind `?brunch=` for comparison). Suite 246/246 (headless Linux: 245, the known font test).
- **C# (`csharp/`)**: deterministic core, netstandard2.1, also a Unity package (`com.depths.core`).
  579/579, 148 parity rows. The whole run loop is ported and checked against JS recordings.
  **The game ticks at 60 Hz** (`Balance.TickHz`); every time-based dial is in seconds or per-second
  units. The suite runs at 210 Hz (assembly `[TickRate]` in `TickRate.cs`), where every dial is
  bit-identical to the JS one, so the parity rows are untouched; `RateTests` (23) compares key
  behaviours at both rates and checks every rate dial by unit. At 60 Hz shells and body contact are
  cut into 4 sub-steps (`Substeps`). After
  `js-final`, C# differs from the JS on purpose: whole-number HP, the regenerating heart and the
  placeholder items (below).
  `Balance.JsReference` restores the JS damage rules for parity tests that record player HP.
- **Unity (`unity/`)**: 6000.3.25f1. Main menu + Options (layout presets, rebinding, volume). Plays a
  real seeded run with placeholder shapes: pause (Esc), death summary, floor banner. EditMode 24/24
  (`TickClock`, `HeartRow`, menus, sound); the frame-to-tick step lives in `TickClock`, the heart
  row's layout and colours in `HeartRow`.
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

Editor on the owner's machine: **`C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe`**
(Unity Hub default; bash: `/c/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Unity.exe`).
`tools/unity.sh` wraps all four commands from the repo root in bash (`UNITY=...` overrides the path;
logs and screenshots go to `unity/Logs/`, ignored). A fresh worktree has no `unity/Library`: copy
it from another checkout or the first command spends minutes importing.

- `bash tools/unity.sh bootstrap`: regenerate scenes/mixer/settings (`-executeMethod Depths.Unity.EditorTools.Bootstrap.Run`).
  It rewrites the scenes' fileIDs every run; revert them if nothing else changed.
- `bash tools/unity.sh test`: EditMode tests (`-runTests -testPlatform EditMode`), prints the totals and any failed test. 24/24.
- `bash tools/unity.sh build`: `-executeMethod Depths.Unity.EditorTools.BuildTools.BuildWindows` -> `unity/Build/Depths.exe`
- `bash tools/unity.sh shot menu|controls|audio|game|hearts`: the player at 1280x720 with
  `-depthsShot out.png -depthsView <view>`. `game` measures the tick rate against the wall clock
  (60 +-2%); `hearts` freezes a fixture (5/8 HP, regen 1/4, armour 3) and samples both lobes of
  every heart against the colours in `HeartRow`. Each check logs `[Depths] check PASS|FAIL`; any
  FAIL exits 1.

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
  The heart row is checked by pixels in the built player (`tools/unity.sh shot hearts`).
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
- **Difficulty curve** (2026-10-07, C# only; **off by default, BLOCKED on the owner**). `Curve.On`
  is false and the bot plays the JS ladder unless given `--curve on`; flip `Curve.On` (one line) once
  the owner signs the tune off. Three cold critic rounds scored it 7/10 each (stall rule), verdicts
  outside the repo. Open decisions: rubric 5 for MIDDLE (below), the floor-2 and floor-12 spikes, and
  whether to drop the middle's gunners so skill orders deaths more clearly. Owner's direction: a non-linear
  curve (hard start, easier-but-not-easy middle, hard end), scarce pickups, buffed enemies later on,
  every part of the kit load-bearing. Built as eight named stages in `csharp/Depths.Core/Curve.cs`
  laid over the depth ladder: `start` (1) and `peak` (2) a bump while the player has no kit, `soft` (3)
  and `middle` (4-10) a dip, `ramp` (11) and `climb` (12) into the last area, `end` (13-16) and `deep`
  (17+). Dials per stage, each with its why in the file: `Tough`, `Rate` (still under `DepthRateCap`),
  `Bodies`, `Pack`, `Heavy` (gunner chance), `Drops` (kill drop chance x). A hit is worth the same on
  every floor. From floor 11 on no floor is easier than the one before (body health, whole extra
  bodies, gunners, packs; pinned by tests); the extra bodies step at 12 and 18. Targets live in the same
  file (`Curve.Targets`). Tests run with the curve off (assembly `[CurveOn(false)]`), so the 148 parity
  rows keep the JS ladder.
  Bot: `play --curve on` plays the curve (off plays the JS ladder); `play --dial middle.Drops=0.15,end.Tough=1.2` tries a
  value without a rebuild (recorded in the JSON header, flagged by `report`); `curve <label>
  [--mid-rule original|recovery|pickups]` prints one ok/MISS line per target with its rubric number,
  exits 2 on a miss, and writes `playtest/curve-<label>.md`. The bot records damage and healing by
  source per floor. Bot, 60 Hz, 60 min, 600 runs a set, seeds 101-300 (the rubric's) / 301-500 (a
  check against fitting to noise); hazard = deaths / floor entries, floors reached by 30+ runs:

  | | START (base 101-300 > now 101-300 / 301-500) | MIDDLE | END | HP leaving 2 | median floor |
  |---|---|---|---|---|---|
  | novice | 1.26% > 12.3% / 11.4% | 0.45% > 2.29% / 3.03% | 4.80% > 40.2% / 38.6% | 8 > 5 / 5 | 17 > 12 / 12 |
  | average | 0.00% > 4.28% / 3.26% | 0.00% > 0.42% / 0.34% | 0.89% > 12.0% / 13.7% | 8 > 6 / 6 | 17 > 17 / 16 |
  | skilled | 0.00% > 2.26% / 2.50% | 0.06% > 0.40% / 0.33% | 0.50% > 7.9% / 7.9% | 8 > 7 / 7 | 17 > 17 / 17 |

  Every target except rubric 5 for MIDDLE holds on both sets. Thin margins, from the bot's profiles
  rather than the curve: average and skilled die about equally often in a gentle middle (pooled over 8
  matrices: 54 against 42 deaths, mostly to the gunner's shell), so the MIDDLE order average > skilled
  (0.42 > 0.40, 0.34 > 0.33) rests on a death or two; skilled START sits at 2.3-2.5% under a 3% ceiling;
  novice HP leaving 2 is exactly 5. Average hazard on floors 11-17 (101-300): 0.6, 4.6, 7.7, 11.0, 10.9,
  13.0, 18.9%; skilled 0.5, 4.4, 8.0, 4.3, 9.0, 7.1, 11.6%; novice 11.9, 31.5, 39.5, 41.3, 48.1% (15-17
  are reached by fewer than 30 novices). What moved what: `Drops` 0 on floors 1-2 makes the start cost
  hearts (HP leaving 2: 8 > 5); fewer gunners (`Heavy`) and faster, dodgeable shooters (`Rate`) are what
  separate skill; the dip is softer bodies, few gunners and scarce drops (x0.14, x0.35 on floor 3 so a
  start with none can be survived); 11-12 add health and the extra body before the last area.
  To playtest, not settled: floor 2 is the start's spike (novice 20%, average 6-7%, the highest
  MIDDLE-or-earlier floor; every END floor is above it); spreading it over 1-2 moved the start out of
  its targets on one seed set or the other, so it is left for the owner to feel. Floor 12 (`climb`) is
  the novice wall: 31.5% / 22.8% of novices who reach it die there, novice END hazard is 40% and the
  novice median floor (12) sits at it; a smaller `climb.Tough`, or the body step moved to 13, would
  soften it. And no kill ever drops anything on floors 1-2: a
  player will read that as broken without a tell (a later UI idea, not built).
  **Owner decision, rubric 5 for MIDDLE.** As written ("healing a floor below damage a floor") it is
  missed by 0.2-0.4 HP a floor for every profile. Over the band, healing minus damage is exactly the
  change in HP and armour, and the start (by design) sends players into floor 3 at about 5 of 8, so a
  middle that gives none of it back has to drain an already hurt player; every drop rate that did broke
  the middle's death ceilings. `Curve.MidRule` holds the choice (one line; default `Original`):
  - `Recovery`: over the runs that leave floor 10, mean (HP + armour leaving 10) - (HP + armour entering
    3) is at most mean (max HP entering 11) - (HP entering 3), i.e. the middle gives back no more than
    the start took plus new max HP. Met: novice 3.22 <= 4.13 / 3.45 <= 4.54, average 3.45 <= 3.61 /
    3.20 <= 3.58, skilled 3.10 <= 3.30 / 2.94 <= 3.15 (101-300 / 301-500; thin for average and skilled).
    A median reading (median HP leaving 2) is not this formula and fails for average and skilled.
  - `Pickups`: MIDDLE healing a floor less the regenerating heart (clock and refill) below MIDDLE damage
    a floor. Met with room: novice 3.43 < 13.87 / 3.31 < 13.82, average 2.75 < 9.73 / 2.72 < 10.05,
    skilled 2.60 < 8.37 / 2.55 < 8.50.
  Recommended: `Pickups`. The critic showed `Recovery` cannot fail on HP alone (`CurveCheck.cs`, the
  recovery check), so it only measures armour.
- **Items**: overhaul later. Placeholders (C# only) until then: Hunter's Mark marks the room for 5s
  (+50% damage taken); Brass Compass opens the fake wall when carried into its room. Lantern Friend is
  still `unimplemented` (not loot).

## Known issues

- Verified in Unity 6000.3.25f1 on 2026-10-07 (was listed as unverified): **60 Hz**, measured in
  the built player as run ticks against the wall clock, 59.95 Hz at 279 fps, the sim stepping every
  clock tick (`TickClock`, 6 EditMode tests over steady, jittery, paused, NaN and hitch frames); the
  **heart HUD**, by pixels in the player, 18/18 checks over full, half and empty red and regen
  hearts and full/half armour (a mutant that drew half hearts full failed every half heart). The
  demo input is now timed in seconds, not frames.
  Not covered: a display at 59.94 Hz double-steps one frame every ~17 s (any fixed-step accumulator
  does); `HeartRow` rounds with .NET's half-to-even, so armour 2.5 shows one heart (cannot happen
  today: HP, regen and armour are whole outside `JsReference`); the hearts check samples positions
  from `HeartRow.Step`, so it would not catch a wrong spacing.
- C# leftovers cleared 2026-10-07: `Intercept.cs` deleted (its spread/reach live on in `Aim.cs`, which
  the gunner now calls); `Pickup.shown` gone (a Mark in a cleared room with loot no longer spends a
  charge); `AssemblePacks` allocation-free once warm (`AllocationTests`). Still open: `run.unlocked`
  from the compass is not ported (nothing read it); the 96px separation grid is not ported.
- JS, left as found (reference behaviour): Brunch scan throttle is dead (`pickShield` every tick); stale
  `packSlot` skews a shrinking wall; boss can spawn on the player; body-contact hitbox is 20px off the
  shot hitbox; Lab eats the first S press.
- Node is at `C:\Program Files\nodejs` on the owner's machine; shells opened before 2026-10-06 need it on PATH.
