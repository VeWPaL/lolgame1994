# DescendParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/DescendParityTests.cs`. The code keeps a one-line gist tagged `[h:DescendParityTests-N]`.

## [h:DescendParityTests-1]
near: `[TestFixture]`

/// <summary>
/// Parity for <c>RunState.Descend</c> and for the room phase's handling of it.
///
/// <para>
/// The values here were measured by putting the running game into the dirtiest state it can be
/// put into - velocity, knockback, spent blinks, a live transition, an armed door, a shell in
/// flight, every per-floor flag set - and then descending, and reading what came out. That
/// method matters: a descent test written from the source lists what the function <i>appears</i>
/// to reset, and a line it appears to reset but does not is invisible. Dirtied state and a
/// diff is the only version that catches one.
/// </para>
/// <para>
/// The measured result, for the record: floor 1 to 2, root seed preserved, both clocks zeroed,
/// cursor back to (3, 3), player at (400, 355) with the lagged hitbox moved with it and all four
/// velocity components zero, blink charges 2 / regen 0 / grace 0, every per-floor flag false, the
/// transition null, the armed door and its timer cleared, shells cleared, the fade armed at 189
/// ticks with <c>roomFade</c> at 1, and the state still <c>playing</c>.
/// </para>
/// </summary>

## [h:DescendParityTests-2]
near: `[Test]`

/// <summary>
/// The new floor is a PURE FUNCTION of (root, floor) - not a chain from the previous floor.
/// This is the property the whole seeding design rests on, and it is the reason the descent
/// reseeds at all. Two descents from the same root must produce the same dungeon.
/// </summary>

## [h:DescendParityTests-3]
near: `[Test]`

/// <summary>
/// The room phase's mid-function return is honoured: reaching the exit skips everything after
/// the pickup loop. If it did not, a descent would also re-open a boss exit on the floor the
/// player just arrived on.
/// </summary>

## [h:DescendParityTests-4]
near: `Assert.That(run.floor, Is.EqualTo(2), "the descent happened");`

/* The portal is STILL THERE after the descent, and that is measured, not assumed.

   The original calls `descend()` and returns without removing it, and a descent builds a
   NEW dungeon rather than emptying this one - so in the browser the old room keeps its
   contents while the player's cursor lands in a room with none. Measured on floor 1 with
   an exit and a heart: after the descent the old room still holds 2, the new start room
   holds 0.

   The port stashes the old room's pickups on the room in Descend (ExitPickupTests checks it),
   and the run's list stands for the new start room, which is empty. What this test is for
   is that the phase stopped: had it fallen through, it would have run the boss-exit check
   against the floor the player just arrived on. */

## [h:DescendParityTests-5]
near: `[Test]`

/// <summary>
/// The room phase reports the descent to its caller, so the tick can honour the original's
/// whole-tick abort semantics around it.
/// </summary>
