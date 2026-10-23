# Depths - how the game works (design reference)

What the game IS today, for a session that starts cold. `docs/STATUS.md` is the plan and the log; `CONVENTIONS.md` the
rules; `PORTED.md` what came from the JS. Numbers here were read from the code on 2026-10-14; the code wins if they
disagree. Long code comments live in `docs/history/csharp/<File>.md` under `[h:File-N]` tags.

## 1. Architecture and the loop

- **`csharp/Depths.Core`**: the whole simulation, deterministic (seeded `Rng`, three streams), netstandard2.1, C# 9.
  Unity only presents it and feeds it input. Same seed, same input, same run.
- **`unity/Assets/Depths/Runtime`**: `GameView` (the frame loop), `WorldView` (draws the room: one `QuadBatch` mesh over
  a runtime-baked atlas), `Hud/LedgerHud` + `HudOverlays` (the ledger HUD, pause, summary, banners), `LabMode` (F2),
  `ScreenshotMode` (`-depthsShot` automated checks), `MainMenuController` (the book menus), `ScreenShake`, `Audio/*`.
- **Rates**: the game ticks at 60 Hz (`Balance.TickHz`); every time dial is written in seconds or per second and
  converted (`Sec`, `SecF`, `PerSec`, `Ease`, `Decay`). The test suite ticks at 210 Hz (the JS rate) so the parity rows
  recorded from the JS stay bit-exact. `RateTests` fails if a rate-derived dial is not declared.
- **Frame vs tick**: `TickClock` turns frame time into ticks (a hitch is cut at 0.25 s, never fast-forwarded).
  `Interp.Snap` before each tick + `TickClock.Alpha` lets the renderer draw bodies, shells and the player between the
  last two ticks, so motion is smooth at any refresh. Options > Gameplay > Frame rate: VSync / 30-240 / Custom (a typed cap, 20-500) / Uncapped.
- **Tick order** (`TickOrder.Update`): gates and transitions, then player, projectiles, bodies, room. The player's
  lagged hitbox (`HitboxX/Y`, it trails after a blink) is what enemies aim at and what shells hit.
- **Switches**: `Balance.Tuned` (on in the game and the bot, off in tests) gates every C#-only change to ported
  behaviour; each part has its own switch (`Tune*`), and the bot can turn any off: `--untune wall,rush,dodge,loot,
  beam,warden,ease,damage,momentum,pools,middle,aim,depth,spike,end,floorboss`. `Curve.On` is the difficulty curve (`Curve.Stages`), **standard since 2026-10-22** (owner): no option; the bot's
  `--curve off` and the tests' `[CurveOn(false)]` (the parity rows were recorded without it) turn it off.

## 2. The player

- Moves at ~315 px/s (PlayerMove 252 x the 1.25 speed bonus), accelerating (`MoveAccel`). Blink (Shift): 116 px, two
  charges, 3.5 s recharge, i-frames; the hitbox trails behind (the faint ghost).
- **Momentum** (`TuneMomentum`): earned by moving while bodies are alive (0.0007 a px: ~5 s of running fills it),
  drains in ~1.25 s standing still, a hit keeps 55%. Full adds 0.40 to the speed bonus (1.25x -> 1.65x walking) and
  doubles acceleration; only momentum may pass the stat cap (0.44 -> 0.65). Shown as the HUD bar and afterimages, in
  the blink's blue.
- **Health**: whole HP, 1 HP = half a heart; 8 at the start, the last 2 a regenerating heart (refills in a won fight).
  Armour takes hits first. Hearts are one row on the ledger; past 8 they close up.
- **Damage dealt** x0.7 (`TuneDamage`, a test setting) on every hit: shots, the blast, the hook's field.
- **Guns** (1-4): Bolt (0.63 s, falls off with range), Scatter (1.1 s, 8 pellets, full damage inside 180 px - the
  game's "close range"), Arcane Beam (0.087 s stream), Voidball (0.4 s). **Right click**: the Blast (100 px AoE,
  3.6 s) or, once found in the secret room, the Hook (pulls and holds a field; a fresh click detonates it early).
- **Q**: the active item. **Items** (`TuneItems`, design and roster in `docs/items.md`): the overhaul's roster is
  played when tuned (the JS's stays for parity). Actives recharge by won fights (pips on the Q slot): Hush Bell
  (clears hostile shells within 220 px, throws bodies back; 2 fights), Time Glass (2.5 s: every hostile body runs on 35% of the
  ticks, timers and wind-ups included, and their shells fly at 35%; it ends with the room; 3), Tin Cup (a heart; 3), Hunter's Mark (2). Passives: Ricochet Shells
  (a shot bounces once off a wall), Ember Rounds (a hit burns a point a second for 3 s), Chain Spark (half a hit
  jumps to the nearest other body within 140 px), Glass Heart (cursed: x1.25 shot damage, half a heart less),
  Phoenix Feather (the first death of the run leaves 2 hearts, then it burns), Brass Compass.

## 3. Rooms, floors, loot

- A floor is a generated map (`World.cs`, parity with the JS). Areas: Shallows 1-4, Kiln Works 5-8, Flooded Galleries
  9-12, Warden's Deep 13-14. **The run ends** (`TuneEnd`, owner 2026-10-22): floor 13 is an ordinary floor; floor 14
  (`Dungeon.FinalFloor`) is an antechamber (a heart to take) with the final boss's arena north of it, its door open.
  The final boss's death wins the run ("THE DEPTHS ARE QUIET"); the records keep wins and the quickest. Untuned, the
  JS's endless descent. Depth scales every body: health (`DepthTough`), speed and cadence (`DepthRate`).
- **Secret room**: a fake wall in a room beside it, shown by a scorched crack; a blast near it opens it; the hook is
  inside. `shot secret` checks the whole chain.
- **Loot** (`TuneLoot`): only a room's last kill can drop, 75% of rooms, one pickup.
- **Area pools** (`TunePools`, `Pools.cs`): every run rolls 4 kinds per area from the nine (its own stream from the
  seed): at least one Lunger or Burrower and one Shooter/Splitter/Mortar/Sprayer, at most 2 shared with the previous
  area. The next area's newcomers appear 2 floors early at weight 0.12, then 0.25 (a group kind is 1). A Gunner or a
  Brunch pack only where the floor's groups hold one; at most 2 eggs a room; no shooter on the doorstep. The dev line
  (F10) shows the run's groups.
- **Middle lift** (`TuneDepth`, `Balance.DepthLift`): health already climbs exponentially with depth (`DepthTough`),
  which left floors 5-12 flat and the Deep a cliff. The lift is 0 through floor 4, rises evenly to 1 at floor 8, holds
  to 12, then 0.5 below (the Deep climbs on health). It adds pace (x1.25 at full, under the 1.34 fairness cap), 0.8
  bodies and +0.15 Brunch-pack chance, never health, so the middle gets denser rather than longer.
- **Spike rooms** (`TuneSpike`, `Spikes.cs`, owner 2026-10-22): on floors 5-12 a normal room is a spike 20% of the
  time on floor 5, rising to 33% on 12 (at most 2 a floor, never beside the start; rolled on the floor's own stream,
  so no other roll moves; off when `TuneLoot` is, which pays them). Half are **Elite**: the wave's strongest body
  (never an egg, a Brunch, a Burrower or a boss; if none qualifies, the floor's likeliest kind that can joins the wave
  as one) has 3x health, approaches 1.2x quicker (a lunge or a dodge never runs past its drawn line), fires 1.2x
  quicker if ranged, glows gold with a gold bar, and drops armour. Half are **Crowded**: 1.8x the bodies and one more Brunch pack (where the floor's groups hold the Brunch).
  Clearing one always drops. A red skull marks it on the ledger map once its door is seen.

## 4. Enemies

Untuned, only the JS four and the Warden spawn. Tuned, everything below.

- **Lunger**: approaches, winds up (the line shows), lunges (an eased dash, same reach and timing as the JS). Dodges
  a bolt it sees coming (0.12-0.6 s out, 1.4 s cooldown, never inside close range or out of its own lunge); once a
  life it dodges on spawn ("startle").
- **Brunch** (packs): walls a shooter or gunner (slots, eased in at up to 320 px/s). Its close attack (`TuneRush`,
  `TickOrder.Rush`): within reach (a chaser: 90 px; a guard: close range and nearer than its slot) it stops and
  trembles 0.25 s with an orange dashed line at the player (the Lunger's line), then dashes along the line it faced
  as the tell ended, aimed at the contact point (1.4x its run; long enough to pass 60 px beyond where a retreating
  player will be, planned for a retreat of up to 75% of its speed; at most 0.8 s; each pack slot 7% slower, so a pack
  lands in a ripple), then skids 0.35 s. The tell holds until the player is twice its reach away. Walking straight
  back is caught; stepping off the line is not. Spends half its health on each tick of contact (two kill it).
  Takes 0.45x Beam damage.
- **Shooter / Gunner**: hold a standoff band (a trait moves it), glide into and out of their walk, cast then fire at
  an intercept (`Aim.Intercept` when tuned: step-and-bisect, safe when the player outruns the shell). Gunner roots
  while casting and sidesteps bolts. Ranged bodies never shoot through their own (`ClearShot`).
- **Splitter**: a 360 px/s shell that bursts 120 px out into 3 shards (+-0.22 rad); aim solved for both speeds.
- **Mortar**: lobs at the player's velocity x the 1 s flight x a trust rolled 0.3-0.85 per shell, at most 230 px
  ahead; the mark fills on the floor; 2 HP inside 56 px.
- **Sprayer**: 0.85 s wind-up (a "tell" sound), aims again as it fires, 5 shots +-0.19 rad; cone kept off allies.
- **Turret egg** (radius 14): fixed (8 shells), orbiting a body of its wave (45%; 5 slower shells; 1.6 rad/s), or on
  a rail in the Kiln Works only (60%; 6 shells; 140 px/s; 8 track shapes at 38-55% of the room, anywhere in it).
  The ring turns to keep shells off allies. The hook cannot drag it.
- **Burrower** (redesigned 2026-10-14; earlier predicting chases were baited by circling, pacing and feints):
  weaving 150 px/s walk (1.6 s), dig (0.45 s skid), under the floor it homes straight on the player with no
  prediction, up to 400 px/s; within 170 px (>= 0.4 s under, or at 2.2 s) a 0.3 s crack, then a LEAP at where the
  player's velocity takes them over the 0.45 s flight (decided at takeoff, <= 220 px, marked on the floor), landing
  for 2 HP within 30 px; then dazed 0.6 s (stunned, skidding, tumbling) with a thud and screen shake. Untouchable
  under the floor and in the air. Counterplay: turn or stop once it is airborne.
- **Warden** (boss): volleys, sweeps, a wall of Brunch statues that guard it (tuned: they wake and act), phases; a
  phase change drops a cast in progress. Tuned, it holds no floor (the Gatekeeper 1-13, the Sunken King 14); untuned
  (or with `TuneFloorBoss` / `TuneFinalBoss` off) it is the boss of the floors they leave.
- **The Sunken King** (placeholder final boss of floor 14, `TuneFinalBoss`, `FinalBoss.cs`; Nuclear Throne's Throne
  and Gungeon's Lich were the reference: a finale that changes the room). A crowned, horned king in a robe split by
  molten seams (16 px wide at art 4, radius 34), 640 x 1.35 HP x `DepthTough`. Its arena mechanic is **the floor**:
  the room is a 10 x 6 grid of cells; a pattern's cells crack (an ember outline, cracks reaching further as the tell
  runs; no fill) and then burn for 1.0 s (a solid flickering fill; 2 HP a second, the i-frames' rate, while the hitbox
  stands on one); ground that will fall for good cracks violet over a darkening cell for 2 s, then is the abyss (a hole
  with a lit lip; 2 HP likewise). The floor throws a player on it toward the nearest good ground, and it keeps burning
  while the King is stunned. **While its floor cracks or burns, the floor is the attack: the King starts nothing**, and
  it never starts a tell over its own live lob marks. A rift's strip is not drawn over the pit. Its body hurts only as
  it charges (2 HP). At most 2 HP a hit; every tell at least 0.35 s (`FinalBoss.TellMin`). Fan: 9 shells.
  **The recall** (grafted 2026-10-22 from the Archivist, the tournament's runner-up; owner's call): once a phase,
  while fewer than 2 of its adds live, it marks two quiet cells (200 px from the player, 90 from it) with violet runes
  for 1.0 s, a ghost of what comes rising in each, then they stand: kinds of THIS run's area for the phase (the
  Shallows, the Kiln Works, the Galleries; never Brunch, Gunner or egg), at 70% health, waking after 0.5 s (a Lunger may still dodge). They crumble
  as the King falls. The run comes back for you.
  - **Phase 1, seated** (full to 66%): on its throne at the top, never moving. Fan, Slam, and the **fault lines**
    (every other row cracks 0.75 s, then burns: step one row).
  - **Phase 2, standing** (66-33%): the throne breaks and **the heart of the room (4 x 2 cells) falls for good**; it
    stands and walks (85 px/s) to a 190 px standoff. Adds Charge and Rift; its pattern is the **checker** (every other
    cell: the safe one is always beside you); its Slam becomes a cross of 5.
  - **Phase 3, risen** (33-0%): the pit closes and **the edge of the room falls instead**; it floats (115 px/s) with
    its broken crown circling. Adds Leap and Nova, drops Slam; Rift becomes three strips, Charge twice; its pattern is
    the **quadrants**: three of the inner floor's four crack 1.2 s and burn, the spared one turns clockwise each time
    and is outlined in pale light ahead of time.
  - Each phase opens with its pattern; a phase change (2.4 s, the collapse's crack) calls off the move in hand.
  - **Fan**: 0.55 s cast flash, 9 shells across +-0.6 rad at 420 px/s, 1 HP. **Nova**: 0.75 s flash, two rings of 18
    (240 and 168 px/s, offset half a gap), 1 HP. **Slam**: 0.4 s flash, lobs whose 58 px landing marks fill for
    0.85 s, 2 HP. **Charge**: 0.55 s orange dashed line, 660 px/s (risen: a second one off its own 0.45 s line).
    **Rift**: 0.6 s, a 48 px strip filled on the floor from the King to the wall (three, 0.55 rad apart, risen), then
    it erupts once, 2 HP. **Leap**: its 70 px landing ring is on the floor for the whole 0.95 s flight; 2 HP inside it,
    then 10 slow shells out from the landing.
  - **Death**: its body shakes and cracks with light for 1.8 s (`DeathBeat`, the shells cleared), the room flashes
    white, then the run is won (`run.finaleT`; TickRoom waits for it). Gaps between attacks 0.16-0.46 s, not scaled.
  - The bot reads it all after its reaction time: the strips and lines (`BotPolicy.Lines`), the leap's ring and the
    floor (`Marks`: off a cell that burns, has fallen, or has cracked long enough to be seen, toward the nearest good
    cell, leading its trailing hitbox), and it will not walk or blink onto such a cell (`FloorGuard`).
    `RunState.finalBy` books the fight's damage by attack (the report's "final floor" table).
- **The Gatekeeper** (placeholder floor boss of floors 1-13, `TuneFloorBoss`, `FloorBoss.cs`; Gungeon's first bosses
  were the reference: one readable gimmick, three patterns, a short fight). A horned iron guard with a brass-barred
  chest (art 4, radius 28), 345 x 1.35 HP x `DepthTough`; the gaps between attacks (1.0 s + up to 0.7 s) and its
  80 px/s walk to a 170 px standoff scale with `DepthRate`; the tells never shorten. Its body hurts only on a tick it
  charges (2 HP); touching it otherwise is safe. Five attacks, each told for at least 0.3 s in the game's language:
  - **Charge**: 0.6 s orange dashed line (the Lunger's), locked at the player as the tell starts and running 110 px
    past them (220-430 px, cut at the wall), then 640 px/s along it; 0.9 s winded after (the window to punish it).
  - **Slam**: a 0.3 s cast flash, then 3 lobs (the Mortar's): one on the player, two 95 px either side across the
    line from the boss; each landing mark fills for 0.95 s, 2 HP inside 50 px. A step along the line is safe.
  - **Fan**: 0.5 s cast flash, then 5 shells at the player across +-0.42 rad, 330 px/s, 1 HP.
  - **Ring** (from phase 2): 0.7 s cast flash, 16 shells all round at 190 px/s, 1 HP; walk through a gap.
  - **Rampage** (from phase 3): three charges in a row, each re-aimed with its own 0.4 s line.
  Phases at the Warden's 0.66 / 0.33 (the notches on the bar); a phase change calls the move in hand off. Attacks
  are weighted by area (Charge, Slam, Fan, Ring, Rampage): Shallows 4/1/2/2/2, Kiln Works 1/4/2/2/2, Flooded
  Galleries 1/1/4/3/2, the Deep 2 each; the one just used weighs 0.35x. The bar above the room names it. The bot
  steps off its line like a Lunger's and reads its lobs' marks as the Mortar's.

## 5. Feel and presentation

- **Screen shake** (Options > Gameplay; `ScreenShake.cs`): trauma per event, offset = reach x trauma^1.5 on smooth
  noise, its own clock (paused = still). Off; Medium (Isaac: landings, blasts, Mortar shells, taking damage; 10 px);
  High (Gungeon: plus kills, hits and a kick per shot weighted by the gun's cadence; 12 px); Insanity (Nuclear
  Throne: everything, 28 px, the room tilts, the ledger shakes too; every gun is loud).
- **Shells**: soft glow (x0.65) and a 1 px black rim so clusters stay countable. Shake events come from Core counters
  the simulation never reads: `run.thuds`, `run.booms`, `run.picked`, plus shots/hits/kills/dmgTaken.
- **Sounds** wired in `GameView.PlayEvents`: shot, hit, kill, hurt, over, pickup, boss warning, tell (long wind-ups),
  door (floor change, a landing's thud). The voices are pinned to the JS (`SoundTests`).
- **HUD**: two ledger boards, no page headings; the body page (hearts, momentum, keys, blink, depth ladder, slain,
  seed tag), the map and kit page. Momentum and blink share the blink blue.

## 6. Working on it

- Gate: `.\verify.ps1 -Unity` (hygiene incl. the comment rule, JS suite, C# suite + count, parity and constant audits,
  Unity tests, build, player shots). C#: `dotnet test csharp/Depths.sln`.
- Unity: `bash tools/unity.sh test|build|shot <view>|publish`. Views worth knowing: game (tick-rate check), hearts,
  secret, rail (DEPTHS_SEED=n picks a track), leap, thud, rush, spike, lab, labrow, gameplay.
- The owner plays `Documents\Depths-playtest` (`tools/unity.sh publish`); snapshots via `tools/snapshot.sh DDMMYY.V.S`.
- Bot: `dotnet run --project csharp/Depths.Playtest -c Release -- play --label X --minutes 60 --seeds 101,...` then
  `report A B`. Bot dodging is a rough stand-in for a player: it sidesteps shells and, after its reaction time,
  runs out of Mortar and Burrower landing marks and steps off a Lunger's or a Brunch's line (crossing it rather than
  sliding along a wall; blinking when it cannot clear on foot). Its aim (87%) is better than a person's. Confirm feel
  with the owner.
- `python tools/move-comments.py` moves any comment block of 3+ lines out of C# (the gate fails otherwise).
