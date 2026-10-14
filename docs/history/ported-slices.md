# PORTED.md - the porting history (moved 2026-10-14)

The slice-by-slice record of the port, as PORTED.md kept it while the JS was the live game. Historical: numbers and file names are as they were then.

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
