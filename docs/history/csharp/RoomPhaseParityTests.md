# RoomPhaseParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/RoomPhaseParityTests.cs`. The code keeps a one-line gist tagged `[h:RoomPhaseParityTests-N]`.

## [h:RoomPhaseParityTests-1]
near: `[TestFixture]`

/// <summary>
/// Parity for <c>TickOrder.TickRoom</c> - the first of the four tick phases ported with real
/// behaviour, and the first set of expectations in this project generated rather than written.
///
/// <para>
/// EVERY number here was measured out of the running game by <c>tools/room-parity.js</c>, which
/// drives the browser's <c>tickRoom()</c> through these same fixtures. That is the point: a
/// hand-transcribed expectation is only a second opinion about what the code should do, whereas
/// two implementations of one function disagreeing is a real finding. Where the two disagreed
/// during this port, three of the disagreements were the fixture's fault and are recorded below
/// - they are kept because each mistake is repeatable.
/// </para>
///
/// <list type="bullet">
/// <item><b>The exit does not remove itself.</b> The original calls <c>descend()</c> and RETURNS,
/// so the portal is still in the list on the tick after it is touched. Asserting on the list
/// makes a distance of ZERO read as "not collected" - the exact opposite of what happened, and
/// what this file's first version reported. The observable is the floor.</item>
/// <item><b>The reward drops land on the player.</b> They spawn at <c>(MIDX, MIDY)</c>, and a
/// player standing there collects them on the tick they are created. A fixture parked at the
/// centre sees an empty list and concludes the drop is broken.</item>
/// <item><b>The touch test is strict.</b> A pickup at exactly its own radius is NOT collected, so
/// the exit at exactly 10px does not descend. "At the boundary" is where an off-by-one hides,
/// so the boundary is asserted rather than assumed.</item>
/// </list>
/// </summary>

## [h:RoomPhaseParityTests-2]
near: `private static RunState Room(uint seed = 4242)`

/// <summary>
/// A cleared, empty standard room with the player at the room centre and a known state, so a
/// test only has to state what it is actually testing.
/// </summary>

## [h:RoomPhaseParityTests-3]
near: `var run = new RunState(new Rng(seed)) { dungeon = Dungeon.FromSeed(new Rng(seed)) };`

// One Rng for the run and another for the dungeon, because `Dungeon.FromSeed` consumes
// its stream: sharing one would generate a floor from a different position than the run
// expects, and the mismatch would only show up as a room the tests cannot find.

## [h:RoomPhaseParityTests-4]
near: `[Test]`

/// <summary>
/// A room that has just been cleared refunds half a blink charge - and only the first time.
/// Measured: <c>BLINK_RECHARGE</c> is 735, so the refund is 367.5. The fraction is kept
/// because the original stores it in a float field; rounding it to 368 would make every
/// cleared room half a tick more generous.
/// </summary>

## [h:RoomPhaseParityTests-5]
near: `[TestCase(8, 8, 1)]`

/// <summary>
/// A heart at full health is left lying rather than eaten. Measured: 8/8 leaves it, 5/8 heals
/// to 7 and takes it, 7/8 heals to 8 and takes it, 6/8 heals to 8 and takes it.
/// </summary>

## [h:RoomPhaseParityTests-6]
near: `[Test]`

/// <summary>
/// The swap parks the OLD weapon where the player is standing, and marks it held. Without the
/// hold it would be re-collected on the same tick and the swap would oscillate forever.
/// </summary>

## [h:RoomPhaseParityTests-7]
near: `[Test]`

/// <summary>
/// The cooldown after a swap is a MINIMUM against the new weapon's, so swapping into a gun
/// that is already nearly ready grants no free shot.
/// </summary>

## [h:RoomPhaseParityTests-8]
near: `[TestCase(12.0, 0)]`

/// <summary>
/// The exit is collected at the bare <c>PLAYER_HIT_R</c> of 10, while every other pickup is
/// collected at <c>pk.r + player.r</c>. Walking into the way down is a deliberate act and
/// should not happen by brushing the edge of a 30px circle.
/// <para>
/// Asserted on the FLOOR, because touching the exit calls <c>descend()</c> and returns: the
/// portal is still in the list on the next tick. The test is strict, so exactly 10px does
/// NOT descend - measured, not assumed.
/// </para>
/// </summary>

## [h:RoomPhaseParityTests-9]
near: `[Test]`

/// <summary>
/// A normal pickup 12px away IS collected - which is what makes the exit's 10px a real
/// distinction rather than an accident of the same number appearing twice.
/// </summary>

## [h:RoomPhaseParityTests-10]
near: `[Test]`

/// <summary>
/// The silver and gold rewards drop once each, at the room centre, with their own radii
/// (14 and 16). The player is in a corner here: at the centre the drop is collected on the
/// tick it is created and the list looks empty.
/// </summary>

## [h:RoomPhaseParityTests-11]
near: `[Test]`

/// <summary>
/// A cleared boss room opens ONE exit, once - not a new portal on every tick the player
/// stands in an empty boss room, which is what the one-shot flag is for.
/// </summary>

## [h:RoomPhaseParityTests-12]
near: `[Test]`

/// <summary>
/// Death wins ties. A pickup arriving on the frame the player dies ends the run as a death,
/// and the boss-exit check beneath it does not run.
/// </summary>

## [h:RoomPhaseParityTests-13]
near: `[Test]`

/// <summary>
/// Picking up a hook leaves a held blast where the hook stood, and cannot be re-collected on
/// the same tick.
/// </summary>

## [h:RoomPhaseParityTests-14]
near: `[Test]`

/// <summary>
/// The item branch is NOT ported, and it says so by throwing. A silent no-op here would be a
/// port that looks finished and plays differently; a crash is a gap you cannot miss.
/// </summary>

## [h:RoomPhaseParityTests-15]
near: `var run = Room();`

// the spec (src/60-tick.js tickRoom + 25-items.js give): a passive raises its stat and is
// gone from the floor; a second active takes the one slot and the first is left where the
// new one lay, holding the charges it had, and cannot be picked straight back up

## [h:RoomPhaseParityTests-16]
near: `[Test]`

/// <summary>
/// An unknown pickup kind is left on the floor rather than silently eaten. Being strict about
/// a name the port does not know is what stops a typo in a spawn call from deleting a reward.
/// </summary>

## [h:RoomPhaseParityTests-17]
near: `[Test]`

/// <summary>
/// The swerve meter's per-tick decay, pinned because it WAS wrong and nothing noticed.
///
/// <para>
/// <c>Balance.SwerveDecay</c> held 0.011 against the original's 0.0035 - a little over three
/// times the rate, which means the game would have forgotten a reversal roughly three times
/// faster than it does, and every gunner in the game would have aimed at where the player is
/// instead of where they were going. No test failed. The suite asserted
/// <c>SWERVE_DECAY &gt; 0</c> and that a reversal survives a second, and 0.011 satisfies both.
/// Nothing in the port read the constant, so a wrong number sat in the file whose entire job
/// is to hold the game's numbers.
/// </para>
///
/// <para>
/// Two things catch it now. The bound is arithmetic rather than a restatement: the original's
/// own test requires a reversal to still be fully remembered after
/// <c>sec(1)</c> = 210 ticks, which caps the per-tick decay at about 0.0033 and rules 0.011
/// out on the game's own terms. And the value is pinned exactly, measured from a meter filled
/// to 1 and left alone: 0.5765 after one second, which is 0.0035 a tick.
/// </para>
/// </summary>
