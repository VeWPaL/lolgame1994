# Status

The handoff file. Any session (including an unattended or scheduled one) starts here and updates
it before finishing. Keep it under a page; history goes in `docs/history/`.

_Last updated: 2026-10-14, night (a typed frame cap; the bot dodges landing marks, with the numbers and the
recommended next steps under "Bot reads marks"; before that, the Burrower redesigned: it homes and leaps, see Middle enemies; before that,
notes batch 4: the Burrower follows a circling player, smaller placed egg tracks,
faster eggs, momentum in the blink's blue; before that, notes batch 3 and the sweep: the Burrower steers, `Aim.Intercept`, the HUD headings
gone, momentum and shell VFX; long comments moved to `docs/history/csharp/` and gated; four reviewers' findings
fixed; see "Notes batch 3" under Owner decisions. Older entries: `docs/history/status-2026-10.md`)._

## Where things are

- **How the game works now** (mechanics, every enemy, switches, feel, workflow): **`docs/DESIGN.md`**. Read it after
  this file; keep it current when a mechanic changes.

- **JS (`depths.html`, `src/`)**: frozen, **tagged `js-final`** (Brunch A+ is the default; A and B stay
  behind `?brunch=` for comparison). Suite 246/246 (headless Linux: 245, the known font test).
- **C# (`csharp/`)**: deterministic core, netstandard2.1, also a Unity package (`com.depths.core`).
  672/672, 148 parity rows. The whole run loop is ported and checked against JS recordings.
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
  Georgia read from the OS via `LedgerFonts`). EditMode 95/95 (`HudTests`: layout never on the room,
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
`tools/unity.sh` wraps the five commands (bootstrap, test, build, shot, publish) from the repo root in bash (`UNITY=...` overrides the path;
logs and screenshots go to `unity/Logs/`, ignored). A fresh worktree has no `unity/Library`: copy
it from another checkout or the first command spends minutes importing.

- `bash tools/unity.sh bootstrap`: regenerate scenes/mixer/settings (`-executeMethod Depths.Unity.EditorTools.Bootstrap.Run`).
  It rewrites the scenes' fileIDs every run; revert them if nothing else changed.
- **`.\verify.ps1 -Unity`** runs the whole gate plus the Unity lane below (test, build, menu/game/hearts
  shots; a few minutes; the editor must not have the project open). Use it before any commit that
  touches `unity/` or `Depths.Core`.
- `bash tools/unity.sh test`: EditMode tests (`-runTests -testPlatform EditMode`), prints the totals and each failure with its message. 95/95. Views also `lab` (the row live) and `labrow` (frozen, every sprite).
- `bash tools/unity.sh build`: `-executeMethod Depths.Unity.EditorTools.BuildTools.BuildWindows` -> `unity/Build/Depths.exe`
- `bash tools/unity.sh shot menu|controls|audio|gameplay|game|hearts|boss|sandbox|summary|pause|banner|warning|lowhp|ledger|focuskey|focusslider|dropdown|hover|gameplayon|customfps|lab|labrow|secret|rail|leap|thud`: the player at 1280x720 with
  (`DEPTHS_SEED=n` passes `-depthsSeed n`: `secret` defaults to seed 1, whose fake wall is room (4,2) N as in the JS, and
  checks the blast opens it and the hook is behind; `rail` shows egg track n % 8; `leap` freezes a Burrower mid-leap, `thud` just after it lands)
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

- **Bot reads marks; custom frame cap (owner 2026-10-14, night).**
  - Options > Gameplay > Frame rate has **Custom**: a typed cap, 20-500 fps (`DisplaySettings.Custom`, saved;
    junk keeps the old cap). `shot customfps` shows the field.
  - The bot (`BotPolicy.Marks`) runs straight out of a Mortar or Burrower landing mark once it has had its reaction
    time on it (or will walk into it within 0.5 s on its present course), slides along a wall rather than into it,
    adds the way out to a shell sidestep in the same tick, and rolls a blink when it cannot clear it on foot. The
    runner books a hit inside a landing Mortar shell's blast on the Mortar (it fell to the nearest body before).
    Not dodged: the untuned Burrower's "rise" landing (tests only).
  - Same build, seeds 101-200 x 3 profiles, 60 min (`playtest/b6-old`, `b6-dodge`): deaths 183 -> 111 (novice
    94 -> 53, average 48 -> 9, skilled 41 -> 8; 125 saved vs 12 newly died, p 7e-25). Damage share: Mortar 13% ->
    1%, Burrower 15% -> 4%. The earlier "middle retune is the main killer" reading was mostly the bot standing in
    marks. Now: Brunch contact 24%, Lunger contact 21%, Sprayer 12%; killing blows Brunch 31, Lunger 26, all
    else 13. The bot never reads a lunge or a rush, so those two are overstated the same way.
  - Where runs die (b6-dodge): 60% at floor 13+ (42 of 70); average and skilled together lose 7 of 200 runs
    before floor 13 and 17 in all. Damage a floor 13.5 / 15.3 / 17.9 / 24.1 by area; healing keeps pace (about 60%
    from the regen heart and its refill). 230 of 300 runs are still alive at 60 min: **there is no win** (nothing calls
    `EndRun(true)`), the Deep goes on forever.
  - The curve on (`b6-curve`): deaths 70 -> 236. It hurts less a floor but heals less still: with `TuneLoot` on top, the curve's dry start bleeds runs out. Not ready to default on.
  - Recommended next (for the owner): (1) a run end, the Warden as the final boss and a win screen, aiming at a
    35-45 min run; (2) teach the bot lunges and rushes, then retune Brunch and Lunger contact from honest numbers;
    (3) the item overhaul (actives healed 0.4 times a run); (4) the Deep is a cliff after a flat middle: steepen
    floors 5-12, or fix the curve's loot rule and use the curve for it.
- **Brunch**: A+ (decided 2026-10-06).
- **Done and archived** (`docs/history/status-2026-10.md`): heart values (whole HP, 8 at the start), the
  regenerating heart, the 60 Hz switch and its wide matrix, the difficulty curve (built, off until the owner
  plays it), and the first playtest-notes batch (`Balance.Tuned`: Brunch wall and rush, Lunger dodge, Warden
  statues, one drop a room from its last kill, the Beam vs Brunch, Dvorak).
- **Middle enemies (built 2026-10-12; in runs through the pools since 2026-10-13).** `Middle.cs`, `Eggs.cs`, C# only;
  all forms in the Unity lab (F2, F4 to arm, F7/F8 to drop; 12 specimens). Untuned they never spawn. The retune is
  `Balance.TuneMiddle` (`Middle.Sharp`; lab-era numbers in brackets); every ranged body aims with `Aim.Intercept`
  (`Balance.TuneAim`) and the shooter's jitter:
  - Splitter: a 360 px/s pink shell (300) that bursts 120 px out (170) into 3 shards, +-0.22 rad (0.42).
  - Mortar: marks the player's velocity x the 1 s flight x a trust rolled 0.3-0.85 per shell (x 1 - swerve), at most
    230 px ahead; 2 HP inside 56 px (34). A stander is under the mark; a turn beats it.
  - Burrower (redesigned 2026-10-14 after four rounds of prediction patches were each baited: circling, pacing, a
    1 s feint): a weaving 150 px/s walk (66); dives (0.45 s tell, a short skid); under the floor it homes straight on
    where the player is now, no prediction, gathering speed to 400 px/s (300); within 170 px (after at least 0.4 s,
    or when its 2.2 s run out) it breaks the floor with a 0.3 s crack, then LEAPS at where the player's velocity takes
    them by the time it lands (decided at takeoff, at most 220 px), marked on the floor for the 0.45 s flight; it lands
    there: 2 HP within 30 px, and is dazed 0.6 s where it hit (stunned, skidding on, tumbling) with a screen shake.
    Untouchable under the floor and in the air. Standing still and walking on are punished; turning or stopping once
    it is airborne escapes.
    Bot (seeds 101-200 x 3 profiles, 60 min) against batch 3's build: deaths 156 -> 146 (novice 86 -> 77, average
    41 -> 38, skilled 29 -> 31). A player who keeps moving escapes every leap (the critic's probe): watch whether it
    plays as harmless; the levers are the flight (0.6 s), the leap range and the strike radius.
  - Sprayer: 0.85 s wind-up (1.0), cadence x0.82, aimed again as it fires, 5 weak shots +-0.19 rad (0.35).
  - Turret egg (radius 14, was 18): fixed (8 shells), orbiting a body of its wave (45%; 5 shells at 0.8x; 1.6 rad/s),
    or on a rail in the Kiln Works (60%; 6 shells; 140 px/s; eight tracks at 38-55% of the room's size, laid anywhere
    inside it). The Sprayer's cone and the egg's ring keep off their allies.
  Unplaytested beyond the owner's notes: HP, cooldowns and damage are first guesses.
- **Notes batch 2 (2026-10-13), archived** (`docs/history/status-2026-10.md`): frame rate apart from the tick
  (`Interp.cs`, Options > Gameplay > Frame rate), the secret wall's crack, `TuneEase`, `TuneDamage` (x0.7, a test
  setting), `TuneMomentum`, `TunePools`, the bot A/B that found the middle retune the main killer.
- **Notes batch 3 and the sweep (owner 2026-10-14, then 6-8 h autonomous): all C# only.**
  - Burrower, ranged aim, Mortar: see Middle enemies. The "shoots completely away" report: the JS aim loop diverges
    when the player outruns the shell (the Splitter's 300 px/s against ~390 px/s at full momentum) and aims down the
    runner's path; `Aim.Intercept` steps and bisects instead (AimTests). Not reproduced on the live path in a test:
    owner to confirm in play.
  - HUD: THE BODY, THE MAP and THE KIT headings removed; pages re-spaced; hearts stay one row (they close up past 8).
  - Momentum drains in ~1.25 s; trail at most 3 ghosts, fainter, in the blink's blue (batch 4; so is the momentum
    bar, deep to full blink blue, and the blink puffs, which were white-to-green). Every shell: glow x0.65 and a 1 px
    black rim.
  - Reviews (a cold critic and three sweep reviewers) found and fixed, each with a test:
    - the Burrower circling its mark;
    - a held right click detonating the hook 0.18 s out;
    - bodies touching a hitbox 20 px above the shells' (a JS sign error, fixed when tuned);
    - the Warden's cast flash surviving a phase change, and expired statues still hitting;
    - hook fields carried down a floor;
    - blink and Q working while paused;
    - rebinds able to take the game's fixed keys (now refused);
    - view state surviving R/F1/F2;
    - no pause on alt-tab;
    - the music engine's per-frame allocation and its swap race;
    - a lab texture leaked per scene load;
    - help text ignoring rebinds;
    - a ninth heart drawn over the momentum bar;
    - the `pickup`, `boss` and `tell` sounds never played.
    F1 (sandbox) and B (Warden arena) now need the dev line (F10).
  - Gate: the constant audit reads the C# values from `Depths.Playtest constants` (the regex lost 11 of 42) and fails
    on a missing one; step 5 now fails when CONVENTIONS' C# count is wrong; C# comment blocks of 3+ lines fail the
    hygiene step (`tools/move-comments.py` moved 498, comment lines 19% -> 7% of the code).
  - Dead code removed: the second enemy model (`Body`, `Bodies.Spawn`; its tests now run on `Spawn.Body`), unused
    dials and helpers, the glyph atlas entries, the inert Sheet/Controls binds. PORTED.md rewritten for the frozen JS;
    its slice history moved to `docs/history/ported-slices.md`.
  - Bot A/B (seeds 101-200 x 3 profiles, 60 min), all on: 156 deaths (novice 86, average 41, skilled 29), median
    floor 15, 20.0 HP of damage a floor, boss fight 63 s. One part off: the new aim 139 (it adds ~17 deaths), the
    middle retune 70 (it still adds ~86, most of them average and skilled players: the main lever if the fights feel
    unfair), the damage cut 153 (~3 deaths, but fights a third longer: 22.4 -> 19.7 kills a minute, boss 47 -> 63 s).
- **Screen shake** (owner 2026-10-14; Options > Gameplay): Off / Medium (Isaac: landings, blasts, Mortar shells,
  taking damage) / High (Gungeon: those, plus kills, every shot and hit as small kicks) / Insanity (Nuclear Throne:
  everything, hard, the room tilting and the ledger shaking too). Trauma-based (`ScreenShake.cs`); Medium by default.
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
