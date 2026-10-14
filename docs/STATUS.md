# Status

The handoff file. Any session (including an unattended or scheduled one) starts here and updates
it before finishing. Keep it under a page; history goes in `docs/history/`.

_Last updated: 2026-10-13, late (the second notes batch: frame rate apart from the tick, the secret wall's crack, eased
dashes, -30% player damage, momentum, the middle five retuned and in runs through per-run area pools; see "Notes batch 2"
under Owner decisions; before that, hud-c: the ledger HUD and the book menus, see the Unity bullet; before that, 2026-10-12: Unity lane re-run at 981f973 with no code changes: `.\verify.ps1 -Unity` green, see Known issues; before that, 2026-10-07: Unity run on the owner's machine: bootstrap, 24 EditMode tests, build, screenshots; 60 Hz and the heart row verified in the player; before that: the difficulty curve, `Curve.cs`, built but off by default pending the owner; before that: C# switched to 60 Hz with per-second units; before that: the won-fight regen refill, the C# playtest bot, Brunch A+, `js-final`). The previous
session's write-up is `docs/report-2026-10-06.html` (its numbers are from the end of that session: 388 C# tests, before 60 Hz and the curve)._

## Where things are

- **JS (`depths.html`, `src/`)**: frozen, **tagged `js-final`** (Brunch A+ is the default; A and B stay
  behind `?brunch=` for comparison). Suite 246/246 (headless Linux: 245, the known font test).
- **C# (`csharp/`)**: deterministic core, netstandard2.1, also a Unity package (`com.depths.core`).
  614/614, 148 parity rows. The whole run loop is ported and checked against JS recordings.
  **The game ticks at 60 Hz** (`Balance.TickHz`); every time-based dial is in seconds or per-second
  units. The suite runs at 210 Hz (assembly `[TickRate]` in `TickRate.cs`), where every dial is
  bit-identical to the JS one, so the parity rows are untouched; `RateTests` (23) compares key
  behaviours at both rates and checks every rate dial by unit. At 60 Hz shells and body contact are
  cut into 4 sub-steps (`Substeps`). After
  `js-final`, C# differs from the JS on purpose: whole-number HP, the regenerating heart and the
  placeholder items (below).
  `Balance.JsReference` restores the JS damage rules for parity tests that record player HP.
- **Unity (`unity/`)**: 6000.3.25f1. Plays a real seeded run in the JS pixel art (`Runtime/Art/`,
  `WorldView`: one atlas, batched quads, no per-frame strings). F2 is the lab (`LabMode`), F10 the dev line.
  **HUD ("the ledgers", winner of a 3-way blind tournament, 3 critic rounds)**: two ledger boards stand the screen's height beside
  the room, all in one 5x7 pixel hand (`LedgerType`). Left, read as icons with no form labels: one row
  of eight 2x hearts (red, regen, armour, exact halves), momentum under them, the keys and blink pips
  in dark wells, the depth seal, the descent ladder (floors struck off per area), slain and the seed tag.
  Items have pixel pictures (`Art/ItemIcons.cs`, in the atlas, also on the floor).
  Right: the map inked on the chart (door marks on every walked room, shells, padlocks, the Warden's
  ring, pickups) and the kit (slots with name plates, Q cap, an EMPTY plate, carried items). The band
  above the room holds the floor heading and the Warden notice; below it the Warden's bar. The last
  heart pulses red round the room's rim. Pause and the run's account (with RECORDS and NEW, kept by
  `RunRecords` in PlayerPrefs) are paper sheets. `Runtime/Hud/`: `HudLayout` is the one geometry
  table, `LedgerArt` the bakes, `LedgerHud` retained + zero-alloc, `HudOverlays`. F10 shows the dev
  line. Menus are an open ledger on a desk (`MainMenu.uxml/uss`, textures baked in the controller,
  Georgia read from the OS via `LedgerFonts`). EditMode 83/83 (`HudTests`: layout never on the room,
  hearts one row at 2x, half hearts exact, 0 bytes per 100 updates, repaint only on change, records).
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
- **`.\verify.ps1 -Unity`** runs the whole gate plus the Unity lane below (test, build, menu/game/hearts
  shots; a few minutes; the editor must not have the project open). Use it before any commit that
  touches `unity/` or `Depths.Core`.
- `bash tools/unity.sh test`: EditMode tests (`-runTests -testPlatform EditMode`), prints the totals and each failure with its message. 88/88. Views also `lab` (the row live) and `labrow` (frozen, every sprite).
- `bash tools/unity.sh build`: `-executeMethod Depths.Unity.EditorTools.BuildTools.BuildWindows` -> `unity/Build/Depths.exe`
- `bash tools/unity.sh shot menu|controls|audio|gameplay|game|hearts|boss|sandbox|summary|pause|banner|warning|lowhp|ledger|focuskey|focusslider|dropdown|hover|gameplayon|lab|labrow|secret|rail`: the player at 1280x720 with
  (`DEPTHS_SEED=n` passes `-depthsSeed n`: `secret` defaults to seed 1, whose fake wall is room (4,2) N as in the JS, and
  checks the blast opens it and the hook is behind; `rail` shows egg track n % 8)
  `-depthsShot out.png -depthsView <view>`. `game` measures the tick rate against the wall clock
  (60 +-2%); `hearts` freezes a fixture (5/8 HP, regen 1/4, armour 3) and samples both lobes of
  every heart on the ledger page (`HudLayout.HeartTopLeft`) against the atlas heart colours in `HeartRow`.
  The game check also logs the longest frame (a hitch past 250 ms loses clock time; with other Unity
  processes busy on the machine it can fail at 55-64 Hz, rerun it alone). Each check logs `[Depths] check PASS|FAIL`; any
  FAIL exits 1.

## Plan

| # | Session | Effort | State |
|---|---|---|---|
| 1 | Cleanup and freeze the JS | Low | done, `js-final` tagged |
| 2 | Unity skeleton | Medium | done |
| 3 | Main menu: Start / Options / Quit, key binds, volume | Medium | done (gamepad + display options later) |
| 4 | Finish the port | High | done |
| 5 | Switch C# to 60 Hz, per-second units, re-baseline | Medium | done (see the 60 Hz entry below) |
| 6 | New sound engine: event-based, sample assets, mixer | Medium | done 2026-10-07: the game had no AudioListener and was silent until then; now one listener on `SoundEngine` with the JS master compressor (`Compressor`, `MasterBus`), sample overrides (`Resources/Sounds/<voice>.wav` replaces a synth voice), procedural music per area (`Score`, `MusicEngine`: calm + fight layers, 76 bpm, 8-bar seamless loops). Player check: the mix is audible, the area plays, the fight layer rises |

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
- **Difficulty curve** (2026-10-07, C# only; **off by default until the owner plays it**). `Curve.On`
  is false and the bot plays the JS ladder unless given `--curve on`; in Unity, Options > Difficulty
  curve switches it (saved). Three cold critic rounds scored it 7/10 each (stall rule), verdicts
  outside the repo. Owner, 2026-10-07: rubric 5 reads as `Pickups` (now `Curve.MidRule`'s default);
  the floor-2 and floor-12 spikes stay as they are for now. Still open: whether to drop the middle's
  gunners so skill orders deaths more clearly. Owner's direction: a non-linear
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
  Kept as they are for now (owner, 2026-10-07; may be revisited): floor 2 is the start's spike (novice 20%, average 6-7%, the highest
  MIDDLE-or-earlier floor; every END floor is above it); spreading it over 1-2 moved the start out of
  its targets on one seed set or the other, so it is left for the owner to feel. Floor 12 (`climb`) is
  the novice wall: 31.5% / 22.8% of novices who reach it die there, novice END hazard is 40% and the
  novice median floor (12) sits at it; a smaller `climb.Tough`, or the body step moved to 13, would
  soften it. And no kill ever drops anything on floors 1-2: a
  player will read that as broken without a tell (a later UI idea, not built).
  **Rubric 5 for MIDDLE: `Pickups`** (owner, 2026-10-07; `Curve.MidRule`). As written ("healing a floor
  below damage a floor") it is missed by 0.2-0.4 HP a floor for every profile, because the start sends
  players into floor 3 at about 5 of 8. `Pickups`: MIDDLE healing a floor less the regenerating heart
  (clock and refill) below MIDDLE damage a floor. Met with room: novice 3.43 < 13.87 / 3.31 < 13.82, average 2.75 < 9.73 / 2.72 < 10.05,
  skilled 2.60 < 8.37 / 2.55 < 8.50 (measured before the 2026-10-07 regen nerf).
  The alternatives, `Original` and `Recovery`, stay selectable (`curve <label> --mid-rule`); `Recovery`
  cannot fail on HP alone (the critic), so it only measures armour.
- **Playtest notes of 2026-10-13 (C# only, `Balance.Tuned`, on in the game and the bot; the test assembly
  plays with it off so the parity rows hold, `TunedTests` turns it on).** Each part has its own switch
  (`Balance.TuneWall/Rush/Dodge/Loot/Beam/Warden`; the bot: `play --tuned off`, `--untune loot,rush`):
  - Wall: a guarding Brunch walks to its slot at up to 320 px/s (was 151), easing in over 0.25 s.
  - Rush: a Brunch with the player inside the Scatter's full-damage reach (`CloseRange` = its FNear,
    180 px) AND nearer than its slot rushes the player (24 px of hysteresis); a formed wall holds as the
    player walks up; a rushing Brunch spends itself on contact like a chasing one.
    (The first rule, any Brunch within reach, made walls body-slam: +5 HP of damage a floor. Fixed.)
  - Dodge: a Lunger dashes (lunge speed) sideways off a bolt, or clear of a blast's burst (its mark, or
    the first body its path meets, neighbours included), when the shot arrives in 0.12-0.6 s; 1.4 s
    cooldown; never inside `CloseRange`, never out of its wind-up, lunge or recovery (the punish window).
    Near a wall it takes the other side of a bolt, or slides along the wall from a burst, never back
    through it. Startle, once a life: a Lunger new to the room (1.5 s of play, or not yet noticing)
    ignores range and reaction for its first dodge, so one spawned beside you is no free kill; after
    that, close range kills. A cold critic pass found the recovery skip, the flip through a burst and
    a too-short startle window; fixed, each with a test that fails on the old code.
  - Warden: the wall's Brunch wake on the normal beat (0.43 s) and guard the Warden (JS: inert until hit).
  - Loot: one draw a kill as before, but only a room's last kill can drop, 75% of rooms (x the curve's
    Drops), one pickup, the 9:9:4:4 split. Beam: a Brunch takes 0.45 x a Beam bolt (2 bolts point blank,
    3 at range; was 1). Unity: the Dvorak preset binds by letter (, A O E); the V key (Brunch rule) is gone.
  Bot A/B (after the critic fixes), seeds 101-200 x 3 profiles, 60 min, tuning off -> on: deaths 30 -> 155
  (novice 27 -> 93, average 1 -> 37, skilled 2 -> 25), damage a floor 14.8 -> 17.4 HP. Everything but the
  loot rule: 30 -> 37 (novice 30, average 4, skilled 3), damage 17.3 HP a floor.
  So the enemy changes together cost about 7 deaths and **the loot rule about 118**: pickups were 70% of all
  healing and are 42% now (the regen heart's clear refill carries the rest). Owner to decide: keep it,
  or keep "one pickup per room, last kill" and raise the chance (85-90%) or weight the split toward
  whole hearts, which keeps the anti-pile-up rule without a third less healing.
- **Middle enemies (owner's idea 2026-10-12; built 2026-10-12; IN RUNS since 2026-10-13 through the pools).**
  Keep the gunner for floors 11+ and give floors 3-10 enemies a skilled player can read and dodge.
  `csharp/Depths.Core/Middle.cs` and `Eggs.cs`, C# only; all forms spawn in the Unity lab (F2, F4 to arm,
  F7/F8 to drop; 12 specimens incl. the orbiting and the rail egg). Untuned they never spawn
  (`MiddleTests.UntunedNoneOfThemSpawnsInARun`). Retuned 2026-10-13 behind `Balance.TuneMiddle` (`Middle.Sharp`;
  off gives the lab-era numbers in brackets):
  - Splitter: slow pink shell (2 HP), half the shooter's aim jitter, bursts 120 px out (170) into 3 shards
    (1 HP each, +-0.30 rad (0.42)).
  - Mortar: leads you like the Lunger (your velocity x the flight, trusted by 1 - swerve), the mark fills for the
    whole 1 s flight; 2 HP inside 56 px (34). Standing still is under the mark; a turn beats it.
  - Burrower: walks 1.6 s, digs (0.45 s tell), runs under at 430 px/s (300) for up to 1.8 s (1.2), re-aiming each tick
    at the intercept (where your velocity takes you by the time it is there and has risen), a 0.6 s crack tell, then
    up: 2 HP within 30 px. A straight runner meets it; a turn on the tell escapes it.
  - Sprayer: 0.85 s wind-up (1.0), cadence x0.82, aimed again as it fires (half jitter), a cone of 5 weak shots
    (1 HP each, +-0.26 rad (0.35)).
  - Turret egg: a 0.6 s tell, then a ring every ~2-2.5 s; the hook cannot drag it. Forms (`Eggs.Dress`, pooled waves):
    fixed (8 shells); orbiting another body of its wave (45% when one exists; 5 shells at 0.8x speed); riding a rail,
    Kiln Works only (60% there; 6 shells, 70 px/s, eight tracks: loop, figure eight, zigzag, serpentine, ring, spiral,
    diamond, clover, each mirrored four ways, 80 px off the walls so the doors stay clear). A riding egg is placed by
    its ride: bodies do not shove it nor it them.
  - Their own side: the Sprayer's cone sweeps to a centre whose 5 lines miss every ally (else holds the blocked
    shells); the egg's ring turns to the rotation crossing the fewest allies and holds what still would. (Hostile
    shells only ever hurt Lungers, as in the JS.)
  A buried Burrower blocks no other body's shot and is never the bot's or the demo's target; the
  Burrower's walk and dive speed up with depth like every body. A cold critic pass (2026-10-13) found
  the lunge clash still shoving it underground; fixed, with a test that fails on the old code.
  Unplaytested: the HP, cooldowns and damage are first guesses. Next: the owner plays them in the lab;
  then a bot A/B with them in the middle pool (`SpawnPlan` mix) against the gunner, for the hazard table.
- **Notes batch 2 (owner 2026-10-13, after playing the build): all C# only, each its own switch, on in the game
  and the bot (`--untune ease,damage,momentum,pools,middle`).**
  - Frame rate apart from the tick: the sim stays 60 Hz; the screen draws between the last two ticks (`Interp.cs`,
    `TickClock.Alpha`), so it is smooth at any refresh. Options > Gameplay > Frame rate: VSync (default) / 30 / 60 /
    120 / 144 / 165 / 240 / Uncapped. Cause of the roughness: the player ran at ~275 fps with VSync on this machine, so
    each tick was shown for 4 or 5 frames unevenly.
  - Secret wall: the JS tell (3 hairlines at 24%) vanished at 1:1; now a scorched split filling the wall band
    (`Sprites.Crack`). Generation, blast and hook room were always working (`secret` shot checks all three).
  - `TuneEase`: the Lunger's lunge keeps its ticks and reach (so its aim holds) on a smoothstep speed curve
    (in 20%, out 25%); its dodge covers the same ground in ~0.13 s at up to 1.15x lunge speed (in 25%, out 40%);
    shooters and the middle walkers glide into and out of their walk (~0.1 s).
  - `TuneDamage`: every player hit (shots, blast, hook field) x0.7 (`Enemy.Vuln`). A test setting, per the owner.
    It makes the Beam's Brunch rule 5 bolts, not 2-3 (`TunedTests` measures the rule with it off).
  - `TuneMomentum`: ~5 s of running under pressure fills the meter (was 0.6 s), ~3 s still drains it (0.8 s); full
    adds 0.40 to the speed bonus (0.18: walking 1.25x -> 1.65x) and doubles the acceleration bonus; only momentum
    may pass the stat's 0.44 cap. An amber afterimage trail from 20% of the meter (`WorldView.TrailFor`).
  - `TunePools` (`Pools.cs`): each run rolls 4 kinds per area from the nine (own stream from the seed; at least one
    Lunger or Burrower and one Shooter/Splitter/Mortar/Sprayer; at most 2 shared with the area before). The next
    area's newcomers spawn 2 floors early at weight 0.12, then 0.25 on the last floor (a group kind is 1). A Gunner
    or a pack only where the floor's groups hold one; at most 2 eggs a room; no shooter on the doorstep while a
    walker can take its place. Depth scaling is unchanged for every kind.
  - Cold critic pass: the Mortar's first lead used the Lunger's belief, which keeps its last heading after a stop,
    so a player who had once moved could stand still forever (marked 252 px off); now the velocity. Also fixed: the
    retune put behind `TuneMiddle`, orbit eggs against a wall shoving their host, riding eggs frozen through the
    notice delay, the momentum cap lifting the speed stat's, a Brunch-only "walker" group, Eggs out of the rate
    guard, the blink ghost not interpolated, the frame setting not applied when Play starts in the Game scene.
    Each with a test that fails on the old code.
  - Bot A/B (seeds 101-200 x 3 profiles, 60 min; "prev" = the first batch's tuning, all of this batch off):
    prev -> now: deaths 155 -> 153 (novice 93 -> 81, average 37 -> 38, skilled 25 -> 34), median floor 17 -> 15,
    damage a floor 17.4 -> 19.7 HP, kills a minute 28.7 -> 19.8, boss fight 46 -> 64 s. One part off at a time
    (deaths, all profiles): no damage cut 146 (kills/min 22.7, boss 47 s), no pools 131 (skilled 14), no momentum
    162, no ease 145, **no middle retune 69** (novice 50, average 11, skilled 8). So the -30% costs ~7 deaths and
    makes every fight a third longer; the pools with the lab-era five are EASIER than the JS mix; the sharpened
    five (the Mortar's and Burrower's leads above all, to be confirmed per kind) are what kills, and mostly the
    better players, which is the direction asked for but likely too far; momentum saves ~9 deaths. The bot's
    dodging is a rough stand-in for a player: read the per-kind numbers off the owner's play before tuning.
- **Items**: overhaul later. Placeholders (C# only) until then: Hunter's Mark marks the room for 5s
  (+50% damage taken); Brass Compass opens the fake wall when carried into its room. Lantern Friend is
  still `unimplemented` (not loot).

## Known issues

- **Measuring allocations in Unity (2026-10-13).** `GC.GetAllocatedBytesForCurrentThread` returns 0 under
  Unity's Mono whatever is allocated (a 1 MB array read 0), so every EditMode "allocates nothing" test
  passed on nothing until then. They now use `AllocMeter` (the Test Framework's `AllocatingGCMemory`,
  self-tested on a 16-byte allocation); it found the FX tracker allocating every frame (fixed). In C# on
  .NET the counter works: `AFightTickWithNoNewShellsAllocatesNothing` found `Rooms.TickUnlock` allocating
  112 bytes every tick of every run since the port (a literal array; fixed). The player still runs ~4
  collections a second in the demo (incremental GC is on; frames stay under 15 ms): shells and pickups
  are new objects by design, and a release player has no GC counter, so attributing the rest needs a
  development build (`ProfilerRecorder` "GC Allocated In Frame").
- **The 60 Hz check (2026-10-13).** It once read 79 Hz: a ~1 s stall in the first game frame (the HUD's
  and the room's bakes) landed after the window opened on the wall clock but was fed to the clock inside
  it. The check now measures over the clock's own fed seconds, logs dropped time, a section breakdown of
  any Update over 200 ms, and keeps a failing log (`unity/Logs/fail-*.log`). The stall itself is gone:
  the main menu pre-bakes the atlas and the HUD's sheets (`LedgerHud.Prebake`), and a floor's five room
  textures cost one vignette pass plus a multiply-add each (`Materials.FloorBase`); the first game frame
  went from 1028 ms to under 200. One 336 ms frame was seen once in `PlayEvents`/music and did not recur
  in 4 runs; the section log will name it if it does.

- Verified in Unity 6000.3.25f1 on 2026-10-07 (was listed as unverified): **60 Hz**, measured in
  the built player as run ticks against the wall clock, 59.95 Hz at 279 fps, the sim stepping every
  clock tick (`TickClock`, 6 EditMode tests over steady, jittery, paused, NaN and hitch frames); the
  **heart HUD**, by pixels in the player, 18/18 checks over full, half and empty red and regen
  hearts and full/half armour (a mutant that drew half hearts full failed every half heart). The
  demo input is now timed in seconds, not frames.
  Re-verified 2026-10-12 at 981f973 (sound commit included): bootstrap clean (scene fileID churn
  only, reverted), EditMode 35/35, build 0 errors, 59.92 Hz over 4.01 s at 279 fps with 240/240
  ticks stepped, hearts 18/18 checks, audio 4/4; full `.\verify.ps1 -Unity` green (C# 579/579, JS
  246/246 at 3 viewports, 148 parity rows).
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
