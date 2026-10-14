# PlayerPhaseParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/PlayerPhaseParityTests.cs`. The code keeps a one-line gist tagged `[h:PlayerPhaseParityTests-N]`.

## [h:PlayerPhaseParityTests-1]
near: `[TestFixture]`

/// <summary>
/// Parity for <c>TickOrder.TickPlayer</c> - the second tick phase to carry real behaviour, and
/// the one the whole tick's ORDER hangs on.
///
/// <para>
/// Every curve below was measured out of the running game by driving the browser's
/// <c>tickPlayer()</c> with the same inputs. These are not "close enough" assertions: the
/// acceleration ramp is compared tick by tick to six decimal places, because a ramp that is
/// 1% wrong is a player whose top speed arrives 4 ticks late, and that is exactly the kind of
/// difference that reads as feel rather than as a bug.
/// </para>
///
/// <para>
/// The measured reference, for the record:
/// </para>
/// <list type="bullet">
/// <item>terminal speed 1.4025, reached in roughly 40 ticks</item>
/// <item>the first ten ticks of velocity holding east: 0.16269, 0.306508, 0.433643, 0.54603,
/// 0.645381, 0.733207, 0.810845, 0.879477, 0.940147, 0.99378</item>
/// <item><b>the diagonal curve is BIT-IDENTICAL to the straight one</b> - measured as an exact
/// array comparison, not a tolerance. A diagonal is not a speed advantage, and this is the
/// assertion that says so.</item>
/// <item>a wall with a CLOSED door clamps the player to exactly <c>RoomLeft + r</c> = 63 with
/// <c>vx</c> = 0</item>
/// <item>a player pinned against that wall for 200 ticks has momentum 0; the same 200 ticks in
/// open floor with bodies present reaches 1</item>
/// <item>after 30 ticks running east the player is at 431.6515 and the lagged hitbox at
/// 414.1123 - the gap is the mechanic</item>
/// </list>
/// </summary>

## [h:PlayerPhaseParityTests-2]
near: `private static Dungeon ClosedWestRoom()`

/// <summary>
/// A one-room dungeon whose west side has no door at all, so the west wall actually clamps.
/// <para>
/// Built by hand rather than generated, because the trap is precisely that a GENERATED
/// room's doors are in an unpredictable state: the start room happens to have its west door
/// present, so a player held against it walks through - correctly - and a probe that calls
/// that "a wall" reports a missing clamp that does not exist. This is the third time that
/// fixture produced a confident false result about a wall.
/// </para>
/// </summary>

## [h:PlayerPhaseParityTests-3]
near: `[Test]`

/// <summary>
/// The acceleration ramp, tick by tick against the measured curve. This is the assertion that
/// makes <c>MoveAccel</c> load-bearing: get it one percent wrong and tick 3 is wrong.
/// </summary>

## [h:PlayerPhaseParityTests-4]
near: `[Test]`

/// <summary>
/// A DIAGONAL IS NOT FASTER. Measured as an exact array comparison in the browser: the two
/// curves are bit-identical, because the input is normalised before it is scaled. This is
/// worth an exact assertion rather than a tolerance - a tolerance would happily accept a
/// diagonal that is 0.5% faster, which is enough to be felt and not enough to be seen.
/// </summary>

## [h:PlayerPhaseParityTests-5]
near: `double straightSp = System.Math.Sqrt(straight.player.vx * straight.player.vx`

/* THE MAGNITUDE MATCHES, TICK FOR TICK - and that is the property, because a diagonal is
   not a speed advantage. The COMPONENTS cannot match the straight-line components: they
   are this divided by root two, which is what "normalised" means. An earlier version of
   this test asserted the components were equal to the straight ones, which can never be
   true of a normalised diagonal and failed for the right reason at the wrong quantity.

   Measured in the browser: the two curves are identical as arrays, and the ratio of the
   diagonal's vx to the straight line's is exactly 0.7071067811865476. */

## [h:PlayerPhaseParityTests-6]
near: `RunPinnedInPlace(run, 0, 0, 1, hold: false);`

// Measured in an EMPTY room (so momentum does not scale the acceleration):
// 1.23981 after one tick, 0.40872 after ten, 0.034711 after thirty, 0.000859 after
// sixty. The first of my earlier values here was taken from a run that had bodies in the
// room, which charges momentum and therefore scales the acceleration term - the
// deceleration from a boosted player is faster, so the numbers do not agree and the
// discrepancy is a fixture difference rather than a port one.

## [h:PlayerPhaseParityTests-7]
near: `[Test]`

/// <summary>
/// A wall CLOSES the player and ZEROES their velocity - measured at exactly
/// <c>RoomLeft + r</c> = 63 with <c>vx</c> = 0, against a room whose west door is closed.
/// </summary>

## [h:PlayerPhaseParityTests-8]
near: `run.dungeon = ClosedWestRoom();`

// A room with no west door, so this is a wall and not a doorway. The start room's doors
// are all locked, which is the trap: a player held against its west side walks straight
// through, correctly, and a probe that calls that "a wall" reports a missing clamp.

## [h:PlayerPhaseParityTests-9]
near: `[Test]`

/// <summary>
/// Momentum is earned from DISPLACEMENT, so a player pinned against a wall gains none however
/// long they hold the key. Measured: 200 ticks pinned gives 0.
/// </summary>

## [h:PlayerPhaseParityTests-10]
near: `[Test]`

/// <summary>
/// The swerve meter rises on a reversal and decays otherwise. Measured from a reversal:
/// 0.4365 immediately, then 0.419, 0.3665 and 0.3 at five, twenty and thirty-nine ticks - a
/// fall of 0.105 per thirty ticks, which is 0.0035 a tick.
/// </summary>

## [h:PlayerPhaseParityTests-11]
near: `RunPinnedInPlace(reversed, -1, 0, 1);`

/* ONE TICK OF THE REVERSAL, and the reading is taken AFTER it - which is where this
   assertion was off by a single tick's decay and looked like a 1% error in the rate. The
   meter rises on the reversing tick AND decays on it, so the value after tick 1 is
   already 0.4365 and every later reading is 0.4365 less 0.0035 per tick since.

   Measured tick by tick: 0.4365, 0.433, 0.4295, ... 0.3 at tick forty. The implied rate
   over those forty ticks is 0.0035 exactly, which is the number that matters. */

## [h:PlayerPhaseParityTests-12]
near: `[Test]`

/// <summary>
/// The lagged hitbox trails the real position and is PUBLISHED for the body phase. Measured
/// after 30 ticks running east: player 431.6515, hitbox 414.1123. The equality matters as much
/// as the gap - if they were ever equal for a moving player, the lag would be broken, and if
/// they were unequal the gunners would be aiming at nothing.
/// </summary>

## [h:PlayerPhaseParityTests-13]
near: `[Test]`

/// <summary>
/// The hitbox converges on the player when they stop, rather than trailing for ever. It is an
/// ease toward the position, so it must arrive.
/// </summary>

## [h:PlayerPhaseParityTests-14]
near: `[TestCase(0, new[] { 0, 89, 178, 267, 356, 445 })]`

/// <summary>
/// Held fire: the ticks a held button shoots on, measured in the running game 2026-10-06
/// (mouseDown held from a standing start, empty room, 600 ticks). Every gun fires on tick 0 and
/// then once per cooldown/TEMPO, rounded up by the per-tick decrement.
/// </summary>
