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
  beam,warden,ease,damage,momentum,pools,middle,aim`. `Curve.On` (Options) is the difficulty curve, off by default.

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
- **Q**: the active item (placeholders until the item overhaul).

## 3. Rooms, floors, loot

- A floor is a generated map (`World.cs`, parity with the JS). Areas: Shallows 1-4, Kiln Works 5-8, Flooded Galleries
  9-12, Warden's Deep 13+ (the boss). Depth scales every body: health (`DepthTough`), speed and cadence (`DepthRate`).
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
- **Warden** (boss, floor 13+): volleys, sweeps, a wall of Brunch statues that guard it (tuned: they wake and act),
  phases; a phase change drops a cast in progress.

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
  secret, rail (DEPTHS_SEED=n picks a track), leap, thud, rush, lab, labrow, gameplay.
- The owner plays `Documents\Depths-playtest` (`tools/unity.sh publish`); snapshots via `tools/snapshot.sh DDMMYY.V.S`.
- Bot: `dotnet run --project csharp/Depths.Playtest -c Release -- play --label X --minutes 60 --seeds 101,...` then
  `report A B`. Bot dodging is a rough stand-in for a player: it sidesteps shells and, after its reaction time,
  runs out of Mortar and Burrower landing marks and steps off a Lunger's or a Brunch's line (crossing it rather than
  sliding along a wall; blinking when it cannot clear on foot). Its aim (87%) is better than a person's. Confirm feel
  with the owner.
- `python tools/move-comments.py` moves any comment block of 3+ lines out of C# (the gate fails otherwise).
