# GeneratorParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/GeneratorParityTests.cs`. The code keeps a one-line gist tagged `[h:GeneratorParityTests-N]`.

## [h:GeneratorParityTests-1]
near: `[TestFixture]`

/// <summary>
/// The generator, checked against floors read out of the running JavaScript.
///
/// <para>
/// Every signature below was produced by the browser, not by running the C#. The signature is the
/// whole graph - every room's grid position, its doors, and which of the two keys, the boss, the
/// upgrade and the secret it holds - so one string pins the entire layout of a floor, and a single
/// moved room makes it red rather than merely suspicious.
/// </para>
///
/// <para>
/// The draw count is asserted alongside it, and it is the more useful of the two when something
/// breaks. A layout difference tells you WHAT changed; a draw count tells you whether the
/// generator even consumed the stream the same way, which splits "the seed stream diverged" from
/// "the geometry went wrong" before you have read a single line of the generator.
/// </para>
///
/// <para>
/// To refresh these, run <c>GeneratorSignatureDump</c> and read the JavaScript side with the same
/// query. A parity table of STALE values is worse than none, because it agrees with a game that no
/// longer exists - that is how the density column in the depth-ladder table came to be wrong three
/// times before anybody measured it.
/// </para>
/// </summary>

## [h:GeneratorParityTests-2]
near: `[TestCase(0u, 16, 19,`

// READ OUT OF THE RUNNING JAVASCRIPT, via
//   Rnd.set(seed); generateDungeon();
// then rooms sorted by x then y, doors sorted, flags appended. 13 seeds: the first ten were
// chosen to span the seed space including 0 and uint.MaxValue, and the last three were added
// after the first parity run found a bug - see EveryFloorIsGeneratedFromTheRoomMap, which is
// the test that bug would have been caught by.

## [h:GeneratorParityTests-3]
near: `[Test]`

/// <summary>
/// A floor is a TREE, and a tree is the property that makes every progression rule hold.
///
/// <para>
/// The whole design rests on there being exactly one route between any two rooms: the key has to
/// be a detour you choose rather than a toll you pay, the critical path has to be walkable, and
/// a loop would mean a route a player can learn in one run. It is also the cheapest possible
/// structural assertion - a room added or a door doubled changes the count immediately.
/// </para>
/// </summary>

## [h:GeneratorParityTests-4]
near: `var walkable = d.AllRooms.Where(r => r.Type != RoomKind.Secret).ToList();`

// THE SECRET IS EXCLUDED, and that is not a convenience. It is a room with NO doors at
// all - the whole point is that a fake wall is the only way in - so it cannot be part
// of the walkable graph, and counting it makes the tree arithmetic wrong by exactly two.
// First version of this test counted every room and expected 2*(n-1); it failed on the
// first seed with 28 against 30, which is the shape of the mistake rather than a bug in
// the generator. A structural assertion that is off by a known design feature is worse
// than no assertion, because it will be "fixed" by loosening the number.

## [h:GeneratorParityTests-5]
near: `[Test]`

/// <summary>
/// Every walkable room is reachable from the start, on every seed. A floor you cannot finish.
///
/// <para>
/// The secret is excluded again, because it has no doors and is opened by breaking a wall.
/// </para>
/// </summary>

## [h:GeneratorParityTests-6]
near: `[Test]`

/// <summary>
/// The shape the design depends on, asserted as a shape on every seed rather than as a property
/// of the thirteen parity rows.
///
/// <para>
/// A parity table can only say "these thirteen floors are right". It cannot say "no floor is
/// wrong", because the floors it never sampled are exactly the ones that would be wrong. These
/// are the invariants that hold for EVERY seed, and they are what the parity table leans on.
/// </para>
/// </summary>

## [h:GeneratorParityTests-7]
near: `[Test]`

/// <summary>
/// The gold key must not be behind the boss.
///
/// <para>
/// The boss door is sealed until the gold key is found, so a gold key that is only reachable
/// through the boss room is a floor nobody can finish. The generator refuses to fork from a
/// trunk tip for exactly this reason, and that comment records the measurement: 113 of 300
/// dungeons had an unreachable gold key before it was caught.
/// </para>
/// </summary>

## [h:GeneratorParityTests-8]
near: `[Test]`

/// <summary>
/// One seed, one floor, every time - which is the whole reason the cursors are instance state.
///
/// <para>
/// This is the generator half of finding #8. The JavaScript kept its flank walk and its pack-id
/// counter at module scope, where <c>startGame</c> could not reach them, so the second run from
/// a seed began part-way round. Here the state is an instance field of a <see cref="Dungeon"/>
/// and the property is structural: two generators cannot share it.
/// </para>
/// </summary>

## [h:GeneratorParityTests-9]
near: `[Test]`

/// <summary>
/// Neighbouring seeds must give different floors, and over a wide spread.
///
/// <para>
/// Not a substitute for the parity table. It cannot say the floors are RIGHT, only that they
/// are not all the same - which is a different and much easier failure to ship, and one a single
/// unlucky seed would hide completely.
/// </para>
/// </summary>

## [h:GeneratorParityTests-10]
near: `[Test]`

/// <summary>
/// Every seed produces a floor, on a wide spread.
///
/// <para>
/// The generator gives up after 800 attempts. The JavaScript's answer to running out is to leave
/// the caller holding the PREVIOUS floor's rooms - a run walking into a dungeon that is not on
/// the screen, with no error anywhere. The port throws instead, so the question becomes whether
/// that ever happens, and the answer recorded here is that it does not across 5000 seeds.
/// </para>
/// </summary>

## [h:GeneratorParityTests-11]
near: `yield return i * 0x9E3779B1u;   // uint arithmetic already wraps; no >>> needed in C#`

// A stride rather than i itself, so the sample is not a run of adjacent seeds. Adjacent
// seeds produce similar dungeons - that is what the jitter offset is FOR - so sampling
// 0,1,2,... is close to sampling the same floor 400 times.
