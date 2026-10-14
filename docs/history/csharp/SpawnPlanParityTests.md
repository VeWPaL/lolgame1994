# SpawnPlanParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/SpawnPlanParityTests.cs`. The code keeps a one-line gist tagged `[h:SpawnPlanParityTests-N]`.

## [h:SpawnPlanParityTests-1]
near: `[TestFixture]`

/// <summary>
/// The spawn planner, checked against plans read out of the running JavaScript.
///
/// <para>
/// As with the generator, nothing here was computed. Every slot position, every pack body position,
/// every pack id and every draw count came out of the browser with only <c>spawnEnemy</c> stubbed, so
/// the function under measurement was still the real one.
/// </para>
///
/// <para>
/// The draw count is asserted on every row and is the more useful of the two assertions when
/// something breaks, for the same reason as in the generator: it separates "the stream was consumed
/// differently" from "the geometry went wrong" before you read a line of the planner. The tail
/// counts encode real short-circuits - a two-body room costs 644 draws and a four-body room 647,
/// because <c>heavy</c> skips its draw entirely below three bodies.
/// </para>
/// </summary>

## [h:SpawnPlanParityTests-2]
near: `[TestCase(1u, 1, Dir.N, 649,`

// READ OUT OF THE RUNNING JAVASCRIPT:
//   startGame(seed); run.floor = floor;
//   room = {x:1,y:1,type:'normal',enemies:[],pickups:[],spawnPlan:null,doors:{}};
//   const before = Rnd.calls.run; spawnWave(room, dir); const used = Rnd.calls.run - before;
// with spawnEnemy stubbed to return a plain record.
//
// Seed 12345 floor 1 north appears TWICE on purpose. The second run is the same seed and the
// same commands, which is finding #8 restated in the spawn planner's own terms: a planner that
// shared state between runs would produce a different second wave, and the row would be green
// forever after.

## [h:SpawnPlanParityTests-3]
near: `[Test]`

/// <summary>
/// The plan itself, positionally, at the precision it can actually be compared at.
///
/// <para>
/// Positions are EXACT and distances are to a tenth of a pixel. That split is not caution, it is
/// the measurement: <c>Math.hypot</c> and <c>Math.Sqrt(dx*dx + dy*dy)</c> are different
/// functions, disagreeing 8,509,056 times with a maximum relative error of 4.397e-16. Across
/// 8,000 plans that changed no position at all, so positions are compared exactly - but 2,910 of
/// those first points had a distance in the last two ulps different, so a distance compared at
/// full double precision would fail on correct code.
/// </para>
/// </summary>

## [h:SpawnPlanParityTests-4]
near: `[Test]`

/// <summary>
/// No shooter starts inside the range a Scatter answers in.
///
/// <para>
/// This is the rule that makes the Scatter's falloff band a decision rather than a statistic: a
/// room that can open with a shooter at 150px is a room where the answer is "walk at it", and
/// the band never gets used. Measured against the same constant the JavaScript uses, 210.
/// </para>
/// </summary>

## [h:SpawnPlanParityTests-5]
near: `[Test]`

/// <summary>
/// The wall can still form.
///
/// <para>
/// Finding #1: the pack id was handed out once per BODY rather than once per PACK, so no two
/// bodies ever agreed on which pack they were in, the wall rule counted 1 for every pack, and
/// the mechanic was dead in the game. It shipped because the only wall anyone had seen was the
/// boss's, which uses a fixed id.
/// </para>
/// <para>
/// So the assertion is not "a pack happened" but "a pack's bodies SHARE an id and there are
/// enough of them", checked over enough seeds that a pack is guaranteed.
/// </para>
/// </summary>

## [h:SpawnPlanParityTests-6]
near: `Assert.That(sizesSeen.Count, Is.EqualTo(Bodies.BrunchPack.Length),`

/* NOT "SOME PACKS DO NOT FORM WALLS". Every size in the table is 4 or more and the
   threshold is 3, so every pack forms a wall, and an assertion that some do not would be
   arithmetic dressed up as a design rule. The variety that matters is in the SIZE: a pack
   that is always five bodies is one shape, and the weights exist to stop that. */

## [h:SpawnPlanParityTests-7]
near: `[Test]`

/// <summary>
/// Pack ids are unique per PACK and never reused, and never reset by a floor change.
///
/// <para>
/// The scope is the run. A pack id has to be unique within a run and no further, but resetting
/// it per FLOOR would be wrong: a body that outlives a floor transition must not end up in a
/// formation with a pack it has never met. Finding #8 was the same counter leaking the other
/// way, across runs rather than within one.
/// </para>
/// </summary>

## [h:SpawnPlanParityTests-8]
near: `Assert.That(planner.PacksIssued, Is.EqualTo(seen.Count),`

/* AND THE CURSOR ADVANCES BY EXACTLY ONE PER PACK. Uniqueness alone is not enough, and
   finding out that cost two mutation checks.

   The first mutation made the cursor advance once per BODY while every body still shared
   the id - which is harmless, because the wall rule only asks how many bodies share an id.
   The suite stayed green. The second gave each body a unique id, which is finding #1's
   actual bug, and went red as it should.

   So a cursor that skips values passes everything that matters about packs, and the line
   that used to carry the bug is exactly the line that stops skipping. Asserting the count
   itself, not just the uniqueness of what it hands out, is what closes it. */

## [h:SpawnPlanParityTests-9]
near: `var rng = new Rng(12345);`

// Measured from a FRESH seed, not from a started run. The JavaScript column was
// `Rnd.set(12345); for 60: rollPack()`, so the stream has to start there too - the first
// version of this generated a dungeon first, consumed a variable number of draws in the
// retry loop, and compared the right function at the wrong point in the stream.

## [h:SpawnPlanParityTests-10]
near: `[Test]`

/// <summary>
/// The pack sizes come out at the weights they claim.
///
/// <para>
/// The weights are 0.30/0.25/0.20/0.15/0.10. Measured over 200,000 strided seeds in the browser:
/// 59985 / 50023 / 39700 / 30297 / 19995. Those are the numbers the port is held to, at a
/// tolerance loose enough to be a distribution check rather than a coin-flip check.
/// </para>
/// </summary>

## [h:SpawnPlanParityTests-11]
near: `private static string Render(List<SpawnSlot> slots) =>`

/* `Letter(k) + Math.Round(x)` IS ARITHMETIC IN C#, NOT CONCATENATION. char converts to its
   numeric value, so 'g' + 142 is 245, and the first version of this helper produced a table of
   charCode-plus-coordinate that looked like the generator had shifted every spawn 103px to the
   right. It was not the generator. The draw counts on those same rows were already identical,
   which is the measurement that said so - and is why the count is asserted first and the
   geometry second, rather than the other way round. */

## [h:SpawnPlanParityTests-12]
near: `private static char Letter(BodyKind k) => k switch`

/// <summary>
/// The lowercase letter the JavaScript room/body strings use, written out rather than derived
/// from the enum name's first character. Deriving it couples the parity table to the C# NAMES -
/// rename Lunger to Chaser and every row goes red for a reason that is not about behaviour.
/// </summary>

## [h:SpawnPlanParityTests-13]
near: `private static string Render(List<PlannedBody> bodies) =>`

/// <summary>
/// Bodies rendered by position and kind, with pack membership NOT rendered.
///
/// <para>
/// The absolute pack id is deliberately absent. It is read from a module counter in the
/// JavaScript, so its value depends on where the session left it and is not part of the
/// contract; what IS the contract is that one pack's bodies SHARE an id and the slots run
/// 0..n-1, and both of those are asserted structurally in
/// <see cref="EveryPackIsOneIdSharedByEnoughBodiesToBeAWall"/> rather than smeared across a
/// string of digits that would have to be re-measured every time a pack moved.
/// </para>
/// </summary>
