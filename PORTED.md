# THE PORT MANIFEST - what is ported, and what the gate checks about it

`src/` (`depths.html`) is the **frozen JavaScript reference**, tagged `js-final`: no new features land there.
`csharp/Depths.Core` is the game's deterministic core, run by Unity. Where the core was ported from the JS,
the parity tables pin it to numbers read out of the running JavaScript, and those rows must keep passing:
a ported behaviour changes only behind a switch (`Balance.Tuned` and its `Tune*` parts, `Curve.On`), which
the test assembly turns off, so the JS rows stay exact while the game plays the tuned version.

`verify.ps1` step 6b fails if a file in `Depths.Core` is not named here.

## Ported, and what each piece must keep matching

| C# | Mirrors | Rule |
|---|---|---|
| `Rng.cs` | `05-rng.js`: Mulberry32, the three streams, `floorSeed`, the base36 codec | A seed must reproduce the run it names |
| `Balance.cs` | `00-balance.js` tuning block and the depth ladder | Every constant in seconds or per-second units, equal to the JS value at 210 Hz (the constant audit checks 47 by value). The tuned dials live in their own block |
| `Hit.cs` | the player hit test in `60-tick.js` | Radius, offset, damage |
| `Aim.cs` | the gunner's spread in `60-tick.js` | Swerve deadzone and full reach are functions of a room. `Aim.Intercept` is C# only (`Balance.TuneAim`): every ranged body's aim when tuned |
| `Bodies.cs` / `Body.cs` | `30-enemies.js`: the body table and the per-area mix dials | Every row declares its armour (`BodyParityTests`) |
| `Spawn.cs` | `spawnEnemy` and `bossInit` | A placed spawn draw for draw; the band trait is rolled by the held gun (the planner rolls, the spawn applies). `BodyParityTests` pins the trait band and the depth scaling on this, the one spawner |
| `Frame.cs` | the camera and world frame in `70-view.js` | `RoomBounds` clamp; the oversized-room branch is exercised |
| `World.cs` | `20-world.js`: `Dir`, `RoomKind`, `Room`, `Map`, `Dungeon` | Signature matches byte for byte |
| `SpawnPlan.cs` | `spawnPlan()` in `20-world.js` | Position, distance and the draw count. `PlanPooled` is C# only (`Balance.TunePools`) |
| `Movement.cs` | `stepLunge`, `solveIntercept`, `idleWander`, `knockEnemy`, `clampEnemy`, `separateBodies`, `clearShot`, the gunner dodge | The lunger draw for draw (`LungerParityTests`). The Lunger dodge, eased dashes and `LineClear`/`ClearCone` are C# only |
| `Items.cs` | `06-stats.js`, `25-items.js`, `25b-items-roster.js` | The 13 items; stats rebuilt from the build; one active slot (`ItemRoomParityTests`) |
| `Rooms.cs` | `enterRoom`, doors, keys, `tickUnlock`, `passDoor`, `clampPlayer`, `checkDoorTransition` | Rooms hold their own bodies and pickups (`RoomEntryParityTests`) |
| `Weapons.cs` | `WEAPONS`, `fireWeapon`, `fireAlt` | Every shell of every gun draw for draw on the jitter stream (`FireParityTests`) |
| `Area.cs` | `areaForFloor()` | Same thresholds |
| `Combat.cs` | `falloffMult`, `alertEnemy`, `slowEnemy`, `damagePlayer`, `killEnemy`, `dropLoot` | `killEnemy` drops loot (one run draw a kill) and takes the Warden's wall with it |
| `RunState.cs` / `Actors.cs` / `Tick.cs` | the run holder and `update()` in `50-run.js`/`60-tick.js`, all four phases | Tick order and RNG discipline (`TickOrderTests`, the phase parity tests) |

## C# only (after `js-final`): no JS counterpart

| C# | What | Rule |
|---|---|---|
| `Curve.cs` | the difficulty curve's eight stages over the depth ladder (off by default) | Tests run with it off (`[CurveOn(false)]`) |
| `Middle.cs` | the five middle enemies (Splitter, Mortar, Burrower, Sprayer, Turret egg); retuned behind `Balance.TuneMiddle` (`Middle.Sharp`) | Appended after the JS kinds; untuned they never spawn (`MiddleTests.UntunedNoneOfThemSpawnsInARun`), tuned the run's area groups bring them. The shared code they touch keeps every JS path bit-identical |
| `Eggs.cs` | the turret egg's forms: fixed, orbiting a body, riding a rail in the Kiln Works (eight tracks) | `Eggs.Dress` runs only on a pooled (tuned) wave and in the lab |
| `Pools.cs` | each run's kinds per area, the next area's newcomers previewed two floors early | `Balance.TunePools`; untuned, `RunState.pools` is null and `PlanWave` plays the JS plan draw for draw |
| `Interp.cs` | each body's, shell's and the player's place at the last tick, for the presenter's blend | Nothing in the simulation reads it (`InterpTests.SnappingNeverChangesTheGame`) |
| `Spikes.cs` | spike rooms on floors 5-12: an elite or a crowded wave, a sure drop, a skull on the map | `Balance.TuneSpike`; marked from its own stream (`SpikeTests.MarkingDrawsNothingFromTheRunStreamAndIsTheSameEveryTime`), so untuned and tuned floors are the same |
| `FloorBoss.cs` | the Gatekeeper, the floor boss of floors 1-13: charge, slam, fan, then ring and rampage by phase, weighted by area | `Balance.TuneFloorBoss`; untuned (and on floor 14) the boss room spawns the Warden as before (`FloorBossTests.UntunedOrSwitchedOffEveryFloorKeepsTheWarden`) |

## Not ported from the JS

The presentation layer (HUD, menus, art, sound) is Unity's own (`unity/Assets/Depths`), built new rather
than translated. The JS Scatter recoil and character sheet were never ported.

## Two rates: the game at 60 Hz, the parity rows at 210 Hz

The JavaScript ticks at 210 Hz and every row below was recorded there. Since 2026-10-06 the C# game
ticks at 60 Hz (`Balance.TickHz`, default `GameHz`), with every time-based dial written once in
seconds or per-second units and converted for the rate (`Sec`, `SecF`, `PerSec`, `Decay`, `Ease`,
`Chance`, `KnockScale`). Parity is kept by running the rows at the JS rate, not by editing them:
`csharp/Depths.Tests/TickRate.cs` sets `TickHz = JsHz` around every test in the assembly, and at 210
each converted dial is bit-identical to the literal the rows were recorded with (`RateTests` pins the
list). The bot's default matrix played with `--hz 210` matches the pre-switch build byte for byte.
What changes at 60 Hz is only discrete-time error (a wait rounded to whole ticks, one step of travel),
measured by `RateTests` and by the bot (STATUS has the wide-matrix comparison). These errors are
corrected off the JS rate only, so 210 stays exact: held fire and the enemy and Warden cooldowns carry
their overrun, and shells and body contact are cut into `Substeps` (4 at 60 Hz) so nothing steps over
a hitbox.

## The parity tables are hand-transcribed, and that is the weak point

`csharp/Depths.Tests` pins 148 `[TestCase]` rows (48 `FireParityTests`, 4 held-fire cadences and 3 `RoomLoopTests` seeds, 2026-10-06) of numbers **read out of the running JavaScript**.

Three of those rows are `RoomScaledParityTests`, and they are a different KIND of row from the rest.
Every other table pins a constant or a function of a seed, so a drift is caught by comparing names.
Those three pin functions of the ROOM — `room.W * 0.25` and friends — which no grep can find, and
which are right in one room and wrong in another. The third row is a room **half** the standard width
on purpose: a value captured once at load reads correctly in the room that existed at load, so only a
room that disagrees can see it. That bug shipped twice on 2026-10-03 — once in the port lagging the
game, and once in the game's own fix, where one half of a single expression followed the room and
the other half was frozen at load.
They go stale silently, and `GeneratorParityTests` says so about itself: *"a parity table of STALE
values is worse than none, because it agrees with a game that no longer exists."*

**`depths.html?parity` prints the whole table from the running game.** Load it, copy, diff against
the C# file, paste, commit. It exists because the refresh cost was high enough that nobody did one
unless a test was already red — which is exactly how the density column came to be wrong three times.
It asserts nothing on purpose: a page that regenerated its own expectations would be a test that
checks itself.

## The audit is automated now, because the workflow was wrong three times

`node tools/parity-audit.js` reads the live `depths.html?parity` output and diffs all five tables
against the C# test files. It **reports** and never writes the C# side — a gate that regenerates its own
expectations asserts nothing. `verify.ps1` step **6c** runs it and fails on any difference.

It exists because the workflow this file describes — *load the page, paste the rows over the C# file* —
had been assumed correct for the life of the project and was wrong three times in one session:

| the generator said | the truth | what a paste would have done |
|---|---|---|
| `Area1` for floors 5, 8, 9, 12 | `Area2`, `Area2`, `Area3`, `Area3` | **broken a correct port** |
| a flat `640` draws on all 21 spawn rows | 644–653, varying by seed and floor | replaced 4 correct rows with 21 identical wrong ones |
| 11 depth-ladder floors | the table pins 18 | silently deleted 7 rows of coverage |

In every case **the game and the port were both right and the generator was wrong**, which is the worst
of the three outcomes available: the page's whole purpose is to be pasted.

The underlying rule, and the reason every list on that page is now the C# table's rather than the page's
own idea of an interesting case:

> **A generator that emits less than the truth is worse than no generator**, because its output is
> designed to be pasted over something real, and a partial paste is indistinguishable from a whole one.

The spawn-plan table also caught a genuine **port** bug while this was being built: `applyTrait` rolls a
trait per ranged body inside `spawnEnemy`, and `WavePlanner.PlanWave` never rolled one, so every gunner
cost the port two draws fewer than the game. The gap was a constant, which is what a missing step looks
like and what divergent randomness never looks like.

## The constant audit

`tools/constant-audit.js` (verify step 6d) reads every mapped tuning constant out of the running game and
compares it BY VALUE with the C# side, which it gets from the C# itself
(`dotnet run --project csharp/Depths.Playtest -- constants`: every `Balance` dial at the JS rate, untuned).
It fails on a mismatch and on a mapped constant the C# side does not have. It replaced a regex over
`Balance.cs` that silently lost every dial written in per-second units (11 of 42 on 2026-10-14). The
history of the two stale constants that made it necessary: `docs/history/ported-slices.md`.
