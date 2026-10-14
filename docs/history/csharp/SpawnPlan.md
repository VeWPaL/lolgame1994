# SpawnPlan.cs - moved comments

Long comments moved out of `csharp/Depths.Core/SpawnPlan.cs`. The code keeps a one-line gist tagged `[h:SpawnPlan-N]`.

## [h:SpawnPlan-1]
near: `public sealed class PlannedBody`

/// <summary>
/// One body's place inside a pack, and the two numbers the formation code is given.
///
/// <para>
/// A pack id and a slot index are the ENTIRE interface between "where a pack was placed" and "how
/// a pack moves". Nothing downstream needs to know how many packs a floor has, where they started,
/// or what a pack is: it asks which wall a body belongs to and where in it the body stands.
/// </para>
/// </summary>

## [h:SpawnPlan-2]
near: `public sealed class WavePlanner`

/// <summary>
/// THE SPAWN PLANNER, and the one piece of run-scoped state in the port that is not the RNG.
///
/// <para>
/// One per RUN, not one per floor. That distinction is the whole reason this is a class rather than
/// a <c>static int</c>, and it comes from finding #8: the JavaScript kept <c>PACK_CURSOR</c> at
/// module scope, which meant it survived <c>startGame</c> and could not be reset by a floor change
/// either. Both halves of that were wrong for different reasons - it leaked ACROSS runs, and
/// resetting it per FLOOR would have been wrong too, because a pack id only has to be unique within
/// a run and a body that outlives a floor transition must not collide with a pack it has never met.
/// </para>
///
/// <para>
/// So the cursor lives here, on the object that outlives floors, and the constructor is where it
/// starts. C# would not have warned about a <c>static</c> here - a module-level counter reads
/// naturally as static, and nothing in the type system connects a static field to the run that owns
/// it. The class makes the scoping structural instead of a comment: two planners cannot share a
/// cursor, and a planner cannot be reused for a second run without being constructed again.
/// </para>
///
/// <para>
/// <b>THE CONTRACT IS DRAW ORDER, as in the generator.</b> The plan costs a fixed 640 draws for the
/// candidate loop plus a tail that depends on the room, and the tail is what the parity rows pin -
/// measured across floors 1 to 12, 644 draws for a two-body room with no pack, 650 for a five-body
/// room with one.
/// </para>
/// </summary>

## [h:SpawnPlan-3]
near: `public static (double X, double Y) EntryPoint(Dir dir)`

/// <summary>
/// Where the player walks in, which is what "far from the door" is measured from.
///
/// <para>
/// Inside the wall by 36 rather than ON it, because the door itself is drawn at the wall and a
/// spawn that cleared the doorway by a body's radius would still be standing in it.
/// </para>
/// </summary>

## [h:SpawnPlan-4]
near: `public List<SpawnSlot> Plan(int count, Dir fromDir)`

/// <summary>
/// Picks <paramref name="count"/> places in the room for bodies to stand.
///
/// <para>
/// 320 candidate points are drawn and filtered rather than points being accepted one at a time,
/// because the filters are not independent: a point rejected for being in the middle of the
/// room should not consume the body it was drawn for. 320 against a room 700x450 leaves a
/// healthy surplus after both filters, and the surplus is what the spacing pass chooses from.
/// </para>
///
/// <para>
/// THE FIRST PICK IS OPTIMISED AGAINST THE DOOR AND THE LATER ONES AGAINST THE BODIES ALREADY
/// PLACED. That is deliberate. A room where the first body is chosen for distance from the other
/// bodies has nobody to compare it to, so it lands wherever the arithmetic happens to put it -
/// which is why the nearest-of number is a large finite 1e4 and not Infinity. With Infinity
/// every candidate scores the same and "farthest from the door" silently degenerates into "the
/// first candidate drawn", which is the top-left of the room every time.
/// </para>
/// </summary>

## [h:SpawnPlan-5]
near: `private static double Nearest(double x, double y, List<SpawnSlot> pts)`

/// <summary>
/// The distance to the nearest already-placed point, or a large finite number when there are
/// none yet. See the note on 1e4 above - Infinity here silently breaks the first pick.
/// </summary>

## [h:SpawnPlan-6]
near: `public int RollPack()`

/// <summary>
/// How many bodies a pack is, by weight.
///
/// <para>
/// The walk subtracts as it goes and falls off the end into the smallest size, which is only
/// correct because the weights sum to exactly 1 - so the fall-off is unreachable in practice and
/// the last return exists to satisfy the compiler rather than to be behaviour.
/// </para>
/// </summary>

## [h:SpawnPlan-7]
near: `public WavePlan PlanWave(int floor, Dir fromDir, Area area = Area.Area1, int weaponIdx = 0, BodyKind[][]? pools = null)`

/// <summary>
/// Plans a whole wave for a room the player enters from <paramref name="fromDir"/>.
///
/// <para>
/// The density roll is 2 plus a fraction of a body, floored at 2. The floor of two is not
/// politeness: a one-body room is not a fight, it is an obstacle, and the extra body from the
/// depth ladder has to be able to make a room fuller rather than emptier.
/// </para>
/// <para>
/// <c>depthBodies</c> ADDS to the roll rather than moving it, and that is the design: the
/// ladder cannot change WHICH bodies a room draws, only how many. A deeper floor should be a
/// fuller room of the same fight, not a different fight - the player learns the shapes on floor
/// one and the shapes are still the shapes on floor twelve.
/// </para>
/// </summary>

## [h:SpawnPlan-8]
near: `var heavy = n >= 3 && _rng.Run() < mix.Heavy + Curve.At(floor).Heavy;`

// At most one gunner, and only where there are enough bodies to space it from the rest.
// The n>=3 test SHORT-CIRCUITS, so a two-body room does not draw here at all. That is
// load-bearing: it is why a two-body room costs 644 draws and a four-body room 647.

## [h:SpawnPlan-9]
near: `var pack = _rng.Run() < Balance.DepthPack(floor, mix.Brunch) && n >= 2 ? RollPack() : 0;`

// A pack is the most interesting thing a room can contain and the most reliable cover, so
// if deep floors were only tougher they would be the same rooms with longer fights. Capped
// in DepthPack, because a room that is always a pack is a single shape.

## [h:SpawnPlan-10]
near: `WavePlan PlanPooled(int floor, Dir fromDir, Area area, int weaponIdx, BodyKind[][] pools)`

/// <summary>
/// The wave from the run's area groups (Pools, Balance.TunePools): the same count, the same spots, the same
/// one-gunner and pack rules, but each slot's kind drawn by the floor's weights. A Gunner or a pack only
/// where the floor's groups hold one (scaled by a preview's weight); at most <see cref="Pools.MaxEggs"/>
/// eggs; a body that shoots never starts on the doorstep while one that comes to you can take its place.
/// </summary>

## [h:SpawnPlan-11]
near: `int trait = Spawn.TakesTrait(s.Kind) ? Spawn.RollTrait(_rng, weaponIdx) : 0;`

/* THE TRAIT ROLL IS PART OF SPAWNING A BODY, and the port left it out, so every gunner cost
two draws fewer than the game's and the whole spawn-plan draw count was out by exactly
that per gunner - measured: the page read 649 where the port read 647 for a four-body
wave with one gunner, and 653 against 650 on another seed. A CONSTANT gap, which is the
signature of a missing step rather than of divergent randomness.

In the game the trait is rolled INSIDE spawnEnemy -> applyTrait -> rollTrait, so the
stream moves when the body is created. Here the body is a value in a list, and
`Bodies.ApplyTrait(Body, Trait)` deliberately takes the trait as an argument and spends
no draw - the trait belongs to whoever is assembling the body. So the roll has to happen
here, in the same place and the same order, or the port's stream drifts from the game's
on any room containing a gunner.

It is drawn only for a body that can TAKE one: `applyTrait` returns early for a walker
(no stand-off band to move) and for anything without a far/close kit, so the roll is for
the RANGED kinds only. Rolling it unconditionally would spend draws the game does not,
and the count would then be wrong in the other direction - which is the trap in fixing a
parity gap by adding a call rather than by finding where the call belongs.

`Spawn.RollTrait(rng, weaponIdx)` consumes the draw by the HELD GUN (it used to be
Bodies.TraitFor, which indexed the gun table by body kind), and the spawn applies it
the game uses, so the two draws are spent here exactly as they are spent there. */

## [h:SpawnPlan-12]
near: `var packId = _packCursor++;`

/* THE ID IS PER PACK, TAKEN ONCE, BEFORE THE BODIES. The JavaScript incremented the
   counter inside the body loop as well as after it, so every body in a pack got its own
   id and no two of them ever agreed on which pack they were in. The wall rule asks for
   packC[packId].n >= BrunchWallMin - a count of bodies sharing one id - so with a
   unique id per body every pack counted 1 and the wall could never form.

   Measured over 40 seeds and 221 rooms containing Brunch: 1227 bodies, 1227 distinct
   ids, and ZERO packs reaching the threshold. An entire documented mechanic was dead in
   the game and live only in the boss's hand-placed wall, which uses a fixed id and so
   was unaffected. That is why it was never noticed: the one wall anyone had seen was
   the one that worked. */

## [h:SpawnPlan-13]
near: `public static class HypotNote`

/// <summary>
/// THE HYPOT DIVERGENCE IS REAL AND DOES NOT MATTER - measured, because it is the kind of thing
/// that gets assumed either way and both assumptions are wrong.
///
/// <para>
/// The JavaScript measures every distance with <c>Math.hypot</c>, which is not
/// <c>Math.sqrt(dx*dx + dy*dy)</c>. Counted in the browser, replacing one with the other changes the
/// RESULT 8,509,056 times, with a maximum relative error of 4.397e-16 - about two units in the last
/// place. It is a different function, not a slower version of the same one.
/// </para>
///
/// <para>
/// And it changed nothing here. Over 8,000 plans - 400 seeds, four entry directions, five counts -
/// the resulting positions were byte-identical, zero differences. A two-ulp difference is far too
/// small to reorder 320 candidates or to flip a comparison between two of them unless they are
/// already tied to that precision.
/// </para>
///
/// <para>
/// So the port uses <c>Math.Sqrt</c> throughout, which is what <c>Hit</c> and <c>Intercept</c>
/// already do, and the parity table asserts positions EXACTLY while asserting distances to a tenth
/// of a pixel. The distances genuinely do differ - 2,910 of the 8,000 first points - so a test
/// comparing them at full double precision would fail on correct code, and one comparing them
/// loosely would be hiding the difference rather than measuring it.
/// </para>
/// </summary>
