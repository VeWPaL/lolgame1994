# THE PORT MANIFEST — what is ported, and what the gate checks about it

`src/` is the reference implementation and stays alive. `csharp/` is a **parity port of the
deterministic core**, kept in step for a future Unity ship. It is not a rewrite and not a second
game: every value in it is meant to match `src/` exactly, and the tests assert that against numbers
read out of the running JavaScript.

This file is the list of what is ported. It exists because the boundary used to live in prose in
`CONVENTIONS.md`, which nobody reads at the moment of making a change — and the cost of that showed up
as `SwerveDeadzone` reading 300 in the game and 350 in the port for an unknown period, defended by a
test that re-derived the prose it was supposed to be auditing.

**Read this before changing anything in the list below.** If you change a ported behaviour in `src/`,
change it in `csharp/` **in the same commit**. There is no "port it later": the port exists to be
trusted, and an untrusted port is worse than none because it looks like a second source of truth.

## Ported, and what each piece must keep matching

| C# | Mirrors | Rule |
|---|---|---|
| `Rng.cs` | `05-rng.js` — Mulberry32, the three streams, `floorSeed`, the base36 codec | A seed must reproduce the run it names, in both languages |
| `Balance.cs` | `00-balance.js` tuning block + the depth ladder | Every constant, in seconds or per-second units (converted for `TickHz`; equal to the JS value at 210 Hz). This is the file that drifts |
| `Hit.cs` | the player hit test in `60-tick.js` | Radius, offset, damage |
| `Intercept.cs` | lunge and gun solutions in `40-combat.js` | Swerve deadzone/full are **functions of a room**, not constants |
| `Bodies.cs` / `Body.cs` | `30-enemies.js` body table, traits, per-area mix dials | Body stats and build-dependent traits |
| `Frame.cs` | the camera and world frame in `70-view.js` | `RoomBounds` clamp; the oversized-room branch must be exercised |
| `World.cs` | `20-world.js` — `Dir`, `RoomKind`, `Room`, `Map`, `Dungeon` | Signature must match byte for byte |
| `SpawnPlan.cs` | `spawnPlan()` in `20-world.js` | Position, distance, and the **draw count** |
| `Movement.cs` | `stepLunge`, `solveIntercept`, `idleWander` in `30-enemies.js`; `knockEnemy`, `clampEnemy`, `separateBodies`, `clearShot` in `40-combat.js`; the gunner dodge | The lunger, draw for draw on the jitter stream. `LungerParityTests`, trajectories from `tools/lunger-parity.js`. Separation is a double loop: the game's 96px grid (8+ bodies) is not ported |
| `Items.cs` | `06-stats.js` (Stats), `25-items.js` (give/remove/use, rarity, pool, hooks), `25b-items-roster.js` | The 13 items; stats rebuilt from the build, never subtracted; one active slot; Q. `ItemRoomParityTests` (tools/item-room-parity.js): six item rooms, with and without items held. `applyVitals` runs on every rebuild here; the game skips it when the build is empty (latent, no item reaches it) |
| `Rooms.cs` | `enterRoom` in `30-enemies.js`; doors, keys, `tickUnlock`, `passDoor` in `20-world.js`; `clampPlayer`, `checkDoorTransition` in `40-combat.js` | Rooms hold their own bodies and pickups; the run's lists are the current room's. `RunState.Start` and `Descend` generate the floor from the RUN stream, as the game does. `RoomEntryParityTests` (tools/room-entry-parity.js): six seeds from startGame through the first normal room, every draw and every body. Item rooms place both weapon and item pedestals |
| `Spawn.cs` | `spawnEnemy` and `bossInit` in `30-enemies.js`/`60-tick.js` | A placed spawn draw for draw (idle then notice on jitter, the flank cursor, a ranged body's cooldown and trait). Exercised by the Warden's statues and by `RoomEntryParityTests`; the band trait is rolled by the HELD GUN once (the planner rolls, the spawn applies) - `Bodies.TraitFor` indexed the gun table by body kind and is now only used by two archetype tests |
| `Weapons.cs` | `WEAPONS` in `00-balance.js` + `fireWeapon` and `fireAlt` (the blast) in `40-combat.js` | Every shell of every gun, draw for draw on the jitter stream; never the run stream. `FireParityTests`, from `tools/fire-parity.js`. Strength and precision are parameters until stats are ported |
| `Area.cs` | `areaForFloor()` in `00-balance.js` | Same thresholds. Takes a floor argument on both sides |
| `Combat.cs` | `40-combat.js` + `50-run.js` + `10-art.js` — `falloffMult`, `alertEnemy`, `slowEnemy`, `damagePlayer`, `killEnemy`, `dropLoot` | On `Enemy`, the live body. `killEnemy` drops loot (one run draw per kill) and takes the Warden's wall with it |
| `RunState.cs` / `Actors.cs` / `Tick.cs` | the run holder and the `update()` gates in `50-run.js`/`60-tick.js` | Tick order and RNG discipline. **`Tick.Update` is a skeleton** |

## Deliberately NOT ported

The HUD, the boss bar, the character sheet, Scatter recoil, the hook rework, the Brunch-arc AI, and
the view/presentation layer generally. They are cheapest to iterate in JavaScript and the port has no
presentation layer to keep in sync. This is a decision, not an oversight — do not "fix" it by porting
the view.

## Two rates: the game at 60 Hz, the parity rows at 210 Hz

The JavaScript ticks at 210 Hz and every row below was recorded there. Since 2026-10-06 the C# game
ticks at 60 Hz (`Balance.TickHz`, default `GameHz`), with every time-based dial written once in
seconds or per-second units and converted for the rate (`Sec`, `SecF`, `PerSec`, `Decay`, `Ease`,
`Chance`, `KnockScale`). Parity is kept by running the rows at the JS rate, not by editing them:
`csharp/Depths.Tests/TickRate.cs` sets `TickHz = JsHz` around every test in the assembly, and at 210
each converted dial is bit-identical to the literal the rows were recorded with (`RateTests` pins the
list). The bot's default matrix played with `--hz 210` matches the pre-switch build byte for byte.
What changes at 60 Hz is only discrete-time error (a wait rounded to whole ticks, one step of travel),
measured by `RateTests` and by the bot (STATUS has the wide-matrix comparison). Three such errors are
corrected off the JS rate only, so 210 stays exact: held fire carries its cooldown overrun, and shells
and body contact are cut into `Substeps` (4 at 60 Hz) so nothing steps over a hitbox.

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

## Where the port resumes

### the four phases — ONE PORTED

`update()` was 1,212 lines and has been split into four named phases: `tickPlayer`,
`tickProjectiles`, `tickBodies`, `tickRoom`. The boundaries are where the data stops flowing, and a
static scope check over comment-stripped code confirms **no phase reads a local of the phase before
it**. Two shared values are named rather than left to chance: `r` (each phase takes its own from
`currentRoom()`) and the player's **lagged hitbox**, which goes through module state because the
gunner intercept solves against the position the player is visually leaving.

    phase              lines   C#
    update()              93   gates + dispatch, all four called in order
    tickPlayer()         169   PORTED - TickOrder.TickPlayer (movement core)
    tickProjectiles()    160   PORTED - TickOrder.TickProjectiles, the blast and the hook (explode, its
                                field and resistance, early detonation; BlastParityTests, HookParityTests) ProjectilePhaseParityTests, from
                                tools/proj-parity.js, 2026-10-06
    tickBodies()         697   PORTED - every body: lunger, shooter, gunner, Brunch (A/B/A+ via
                                Balance.BrunchVariant) and the Warden (volley, sweep, the wall of
                                statues, phases; BossParityTests), 2026-10-06
    tickRoom()           109   PORTED - TickOrder.TickRoom

`TickPlayer` is the second, and porting it found a **pre-existing defect in `TickMomentum`**: the C# had
lost the original's empty-room guard, so a player walking alone charged momentum - a free speed bonus
with no counterweight. It was invisible until there was a caller, because nothing else in the port
called it. It surfaced as an acceleration curve reading 0.433763 where the game reads 0.433643, and
that fourth-decimal difference is a momentum of 0.0017.

Not ported inside the player phase: the boss warning, the on-use field effects and the hook resistance.
Firing, the blink (TickBlink, TryBlink/DoBlink: BlinkParityTests, tools/blink-parity.js) and Momentum's
share of the speed bonus are ported (2026-10-06; the momentum term was missing and only showed with a body
in the room). `CheckDoorTransition` is the geometry half only and returns false rather than
guessing, so a player at a wall is never teleported into the next room by a stub.

`BrunchArcSlot` is PORTED - the single function that decides whether a Brunch is a shield
or a crowd, and the source of the session's three earlier Brunch fixes. Its measurements found a PORT
bug: the original truncates only `(slot/2)` and writing that as C# integer division truncates both,
which mirrors the arrangement for odd packs and is invisible at even ones.

`AssemblePacks` is the first third of the body phase and it is PORTED: pack centroids, target
selection, and the guard lists. Its expectations in `PackAssemblyParityTests.cs` are generated from
the running game by `tools/pack-parity.js`, and both load-bearing rules are mutation-checked - removing
the `!shieldTarget` guard fails two tests, and letting the guard list accumulate fails three.

**Two structural corrections the pack port forced, neither of which a test could have found:**

  - `RunState.enemies` was `List<Body>` and is now `List<Enemy>`. `Body` is the ARCHETYPE - the
    build-dependent stats for a kind - and `Enemy` is the live instance: position, health, cooldowns,
    pack, shield target. The archetype was where the instance belonged, and it was invisible because
    archetypes are never asked where they are. It surfaced the moment the pack pass asked.
  - `Enemy.packId` was a plain `int` and is now `int?`, because the JavaScript tests
    `packId === undefined` and a sentinel makes "pack zero" and "no pack" indistinguishable.

**And a design property, measured rather than read: a pack COMMITS to its target.** The scan is
`!shieldTarget && (frameCount % BrunchScanTicks === 0)`, and the guard means a pack with a living
target never re-picks - measured as no switch across 400 frames with a nearer ranged body 20px from
the centroid. My probe expected a switch and got none; the game was right.

`TickRoom` is the first pass with real behaviour. Its expectations in `RoomPhaseParityTests.cs` were
**generated from the running game** by `tools/room-parity.js`, not written by hand - the same
discipline as the five parity tables. `Descend` came with it, and its expectations in
`DescendParityTests.cs` were measured by dirtying every field the function touches and reading what
came out.

**The item branch is not ported and throws.** `Items.give` has no C# counterpart; a silent no-op
would be a port that looks finished and plays differently.

**One structural difference worth naming:** in JavaScript a pickup lives on the ROOM, so descending
builds a new dungeon and the old room keeps its contents. This port keeps the current room's pickups
on the RUN, so `Descend` cannot express that directly - `enterRoom` is where the original empties a
room, and it is the remaining gap on this path. It is named in the code rather than papered over.

At `update()` in `60-tick.js` — 868 of its 1123 lines, and the tick order is already pinned by
`TickOrderTests`. **Measured fact worth keeping:** `update()` contains zero references to `ctx`,
`document`, `performance.now` or `Date.now` — the simulation is already free of the host, so porting
it is not tangled with rendering. Slice it in tick order. **The measured order is player → projectiles → bodies → room**, not
projectiles first as this file once claimed; see the note below. Wire it with
a parity test per slice.

### The pass ORDER, measured — and it was wrong here before it was right in the manifest

**The game runs `player → projectiles → bodies → room`.** The port pinned
`projectiles → bodies → player → room`, in the code, in the class doc, and in the *name of its own
test* — `TheTickOrderIsProjectilesThenBodiesThenPlayerThenRoom`.

Measured by putting a probe inside `update()` on each pass and reading the recorded sequence out of
one live tick. The reason reading the source does not settle it: `update()` is a single function with
the loops inline, so scanning for the loops in order finds the projectile loop at 594 and the bodies
loops after it, and the player integration is at **432–517** — *above* all of them. "Projectiles then
bodies" is only true from line 594 onward.

It is load-bearing twice, so this was not a stale label on a stub:

- The player pass calls `checkDoorTransition`, which can set `trans`, and `src/60-tick.js:517` is
  `if(trans) return;` — a **whole-tick abort**. On a tick where the player walks through a door,
  projectiles and bodies must not run at all. Resolve projectiles first and that shell advances on a
  tick it must not, on the tick the player leaves the room.
- A gunner steps *after* the player has moved, so its aim is solved against where the player **is**,
  not where they were. The swerve meter is computed from the player's *target* velocity before
  integration (comment at `src/60-tick.js:466`), so a body stepped first leads a position the player
  has not reached.

### The slice boundaries, measured rather than estimated

`update()` is one function from line 346 to end of file, and its four passes are inline inside it. The
first pass — the projectile loop — is **lines 592–741, 150 lines**, found by brace-matching the
`for(let i=projectiles.length-1;i>=0;i--)` loop rather than by reading for a marker.

**The pass is NOT independently portable, and this is the thing that is easy to get wrong.** It calls
six helpers that are not ported, and they are small — smaller than the pass itself:

| helper | lives in | lines |
|---|---|---|
| `falloffMult` | `40-combat.js` | 5 |
| `alertEnemy` | `40-combat.js` | 4 |
| `slowEnemy` | `40-combat.js` | 1 |
| `killEnemy` | `50-run.js` | 6 |
| `explode` | `40-combat.js` | 38 |
| `damagePlayer` | `40-combat.js` | 70 |
| the pass itself | `60-tick.js` | 150 |

So the real first slice is ~275 lines across three files, not 150 in one. Landing it as a single
commit that adds seven functions is worse than landing the two trivial helpers first, because
`damagePlayer` is where the death and iframes rules live and those have their own parity tests.

**The blocker is the `Projectile` record, not the loop.** `csharp/Depths.Core/Actors.cs:55` declares
seven fields — `x, y, vx, vy, r, dmg, friendly` (plus `heavy`, which the pass does not read). The pass
reads **23**, of which **16 are absent**:

    present (7 read)   x y vx vy r dmg friendly
    absent  (16)       age alt color dx dy hit mode owner ox oy phase pierce scale speed tx ty

`heavy` is declared on the record but never read by this pass — it is consumed by the view and the
sound layer, neither of which is ported.

and shells are CONSTRUCTED with 29 distinct fields across `40-combat.js`, `50-run.js`, `60-tick.js` and
`80-ui.js`. Seven of those are never read by this pass (`fFar`, `fMin`, `fNear`, `from`, `it`, `shrink`,
`heavy`) — `it` is the arcane-beam lifetime counter and the `f*` trio is the falloff band, both of which
belong to the VIEW and to a later slice. Widening the record to all 29 up front would put fields in the
port that no ported code touches, which is the shape PORTED.md exists to prevent.

**Recommended first commit:** add the 16 absent fields to `Projectile`, port the four
one-to-six-line helpers (`falloffMult`, `alertEnemy`, `slowEnemy`, `killEnemy`), and leave the loop for
the commit after. That is a self-contained slice, it keeps all 163 C# tests green, and it is verifiable:
the widening can be pinned by a test that constructs a shell with every field and reads it back.

### Slices 1 and 2, landed

**Slice 1** widened `Projectile` to the 23 fields the pass reads and pinned it three ways. One mutation
caught a claim in my own test: changing `pierce` from `int` to `double` left the suite green, because
NUnit's `Is.EqualTo` treats 2 and 2.0 as equal — so a value assertion cannot pin a type, and the test
now asserts on `FieldType`. See `ProjectileRecordTests`.

**Slice 2** is `Combat.cs`: `falloffMult`, `alertEnemy`, `slowEnemy` and `killEnemy`, plus the
`RunState` fields they need (`kills`, `enemies`, `pickups`) and three constants. What is left for slice 3
is the 150-line loop itself plus `explode` (38) and `damagePlayer` (70).

Three things in it are worth stating rather than leaving to the next reader:

- **`killEnemy` drops nothing.** `dropLoot` is not ported and `Loot.Drop` always returns null. That is a
  deliberately partial function, so it is asserted as one (`Kills.DropsLoot`) — a partial function is
  the shape that gets mistaken for a complete one. When the loot table lands, the drop must draw from
  `run.rng.run` in the same place the original does, because the spawn-plan draw counts are asserted and
  a drop that spent a draw would move every one of them.
- **`DropsLoot` is `static readonly`, not `const`.** As a `const` the compiler proved the drop branch
  unreachable and refused to build (CS0162), which is a louder signal than a silent omission would have
  been.
- **`alertEnemy` only ever RAISES the aggro timer.** Mutation-checked both ways: turning the `Max` into an
  assignment, and dropping `slowEnemy`'s `pursuit` reset, each turn exactly one test red. The second is
  the one a transcription loses — a body that is slowed should restart its approach ramp, not resume at
  the speed it had built.

**Expected values are read out of the browser**, including the degenerate falloff bands: a zero-width band
and a band whose far edge is *before* its near edge both land on the floor, 0.4. That is not obvious
from reading `Math.max(1, fFar-fNear)` and is obvious from measuring it — a naive transcription would
divide by zero or invert the ramp into a damage bonus for shooting from further away.

### Slice 1, landed: the `Projectile` record

The record is now the 23 fields the pass reads, and `ProjectileRecordTests` pins it three ways.

**The seven view-only fields are asserted ABSENT**, by name: `fNear`, `fMid`, `fFar`, `from`, `it`,
`shrink`. Shells are constructed with 29 fields across four JavaScript files and these have no ported
reader — the falloff trio belongs with `explode`, and the rest are presentation. Asserting their absence
is deliberate: a field in the port that nothing reads is the drift this manifest exists to prevent, and
a test that merely omitted them would leave the decision to whoever reads the file next.

**ONE MUTATION CAUGHT A CLAIM IN MY OWN TEST, and it is the reason the type assertions exist.**
Changing `pierce` from `int` to `double` left the suite GREEN, because NUnit's `Is.EqualTo` treats 2 and
2.0 as equal. So an assertion on the value cannot pin the type — and the comment above it claimed it did,
which is precisely the failure mode this repo keeps warning about, committed by me. `age` and `pierce`
are integer counts in the original (`p.age++`, `p.pierce--`, and `p.pierce>0` decides whether a bolt keeps
going), so the test now asserts on `FieldType` rather than on value. That assertion is red under the
mutation and green without it.

This is worth more than the field it was written for: a value assertion that cannot fail is the same
shape as a placeholder assertion, and the only reason it was caught is that the mutation was run.

---

## The constant audit, and why it is in the gate

`Balance.cs` is the single place the game's tuning lives, and two constants in it have been found
WRONG by value while the C# suite was green through both:

    SwerveDecay   0.011 against the game's 0.0035    the gunners forgot a reversal three times too fast
    BrunchRamp    Sec(2.2) against the game's 1.5s   the PRE-TUNING value, kept through a deliberate
                                                  change, so the port still carried a ramp the game
                                                  had replaced

Both were found by hand, and only because something finally happened to read them. An unpinned
constant in that file is indistinguishable from a correct one until a caller exists - and most of the
tick is still unported, so most of them have no caller.

`tools/constant-audit.js` therefore compares them all, whether or not anything reads them yet:

    47 constants compared, 0 mismatches, 0 missing

It is step **6d** of `verify.ps1`, and it is REPORT-ONLY: an audit that fixes what it finds cannot tell
you how much was wrong. Verified by putting `SwerveDecay` back to 0.011 and watching the gate fail
with the constant named and both values printed.

Three parser false positives were fixed while building it, all the same mistake - a declaration can
name several constants (`public const int A = 1, B = 2;`) or resolve through `Sec()`, and reading
only the first made eight correct constants look absent. That is the false-positive twin of the bug
the audit exists to find, and it is recorded because "the audit says eight are missing" and "the audit
says eight are wrong" are very different findings wearing identical output.
