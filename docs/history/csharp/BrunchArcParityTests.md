# BrunchArcParityTests.cs - moved comments

Long comments moved out of `csharp/Depths.Tests/BrunchArcParityTests.cs`. The code keeps a one-line gist tagged `[h:BrunchArcParityTests-N]`.

## [h:BrunchArcParityTests-1]
near: `[TestFixture]`

/// <summary>
/// Parity for <c>TickOrder.BrunchArcSlot</c> - the single function that decides whether a Brunch
/// is a shield or a crowd.
///
/// <para>
/// Every position below was measured out of the running game by <c>tools/arc-parity.js</c>: a
/// shooter at (200,300), a player at (400,300), target radius 10, and every slot of packs of 2, 3,
/// 6, 9 and 12 recorded as its distance along the bearing from the target.
/// </para>
/// <para>
/// <b>AT FULL PRECISION, and that is worth saying.</b> The first version of the probe rounded with
/// <c>toFixed(2)</c>, which reported 49.64 where the game produces 49.642207661 - and a correct
/// port then looked 0.002 wrong against a 1e-6 tolerance. Six failures, all of them the probe's
/// rounding rather than the code. A measurement that is rounded before it is compared is a
/// measurement whose tolerance is the rounding, and the lesson generalises: the tolerance belongs
/// to the PRECISION OF THE MEASUREMENT, not to the size of the disagreement you expected.
/// </para>
///
/// <para>
/// The three behaviours asserted here each replaced something that was wrong, and the measured
/// evidence for all three is in the method it guards:
/// </para>
/// <list type="number">
/// <item>the stand-off is a FRACTION of the gap, not a constant - a 118px radius put the wall
/// BEHIND the player on a 164px gap, and blocked line of sight 27-39% of the way rather than
/// closing it;</item>
/// <item>the cone is DERIVED from the target's apparent width, so a small enemy at close range
/// gets a shield sized to it - the old 45-degree floor meant a 3-pack blocked 0 of a 23px shot
/// line and a 12-pack blocked 4, the same 33% at every size;</item>
/// <item>the surplus stacks BEHIND the front rank at the same angle, so a 12-pack is two deep
/// rather than six stragglers standing in empty floor beside a 6-pack's wall.</item>
/// </list>
/// </summary>

## [h:BrunchArcParityTests-2]
near: `[TestCase(0, 49.642207661)]`

/// <summary>
/// A 6-pack's six slots, measured at full precision: 49.642207661, 65.603039328, 50, 67,
/// 49.642207661, 65.603039328.
/// <para>
/// The pairs are front and back rank - the odd slots sit <c>BrunchWallRank</c> = 17 behind -
/// and slot 2 is the one exactly on the centreline, where the half-step column calculation
/// lands on a whole number and gives a clean 50. Slots 0 and 4 differ from slot 2 by a
/// fraction of a pixel because the cone CLIP is active at six bodies and not at three.
/// </para>
/// </summary>

## [h:BrunchArcParityTests-3]
near: `[Test]`

/// <summary>
/// A pack too small to have a shape gets null, and the caller walks it straight in. Measured:
/// a pack of one returns null.
/// </summary>

## [h:BrunchArcParityTests-4]
near: `[Test]`

/// <summary>
/// THE STAND-OFF IS A FRACTION OF THE GAP - the measurement that replaced a constant radius.
/// Measured: with the target 200px from the player the arc forms 50px out, which is a quarter
/// of the gap. Against the old 118px constant on a 164px gap the wall formed BEHIND the
/// player.
/// </summary>

## [h:BrunchArcParityTests-5]
near: `[Test]`

/// <summary>
/// The fraction is what makes the arc track a CLOSING gap. Measured: a 100px gap forms the arc
/// 24.55 along and off-axis (the cone is wider at close range, so the outer slots swing
/// further), while a 500px gap forms it 124.59 along. Both are a quarter of their gap.
/// </summary>

## [h:BrunchArcParityTests-6]
near: `Assert.That(System.Math.Abs(close.Y - 300), Is.EqualTo(4.704610).Within(1e-4));`

/* Measured: the close arc swings 4.70px off the bearing and the far one 10.12px. So the
   FAR arc is the wider one in absolute terms - because a fixed angular cone covers more
   linear distance further out, which is the opposite of what I asserted when writing this
   test. The angular width is the thing that is constant; the linear spread scales with
   the radius.

   Which is correct for a shield: a distant wall spans enough ground to matter, and a
   point-blank one is compact because there is no room for anything else. */

## [h:BrunchArcParityTests-7]
near: `[Test]`

/// <summary>
/// A 3-pack is the smallest that walls, and it is asymmetric - the middle body sits on the
/// centreline and the two outside it clip differently. Measured at full precision:
/// 49.642207661, 66.32762017, 49.642207661.
/// </summary>

## [h:BrunchArcParityTests-8]
near: `Assert.That(Dist(3, 1) - Dist(3, 0), Is.EqualTo(Balance.BrunchWallRank).Within(1e-6));`

// The rank is a difference in RADIUS, not in the x projection. Measured: a 3-pack's slots
// sit at radius 50 and 67 from the target, so the back rank is exactly BrunchWallRank
// further out. Asserting it on `Along` instead - which is the x projection of a chord at
// the slot's angle - gives 16.685 rather than 17, and looks like a geometry bug.

## [h:BrunchArcParityTests-9]
near: `[Test]`

/// <summary>
/// A 12-pack does NOT spread wider than a 6-pack. Measured at full precision: both have their
/// outer slots at 49.642207661 and their back rank at 65.603039328 - the width is bounded by
/// the CONE, and the extra bodies stack. This is the assertion that kills the old behaviour,
/// where a 12-pack put half its bodies in empty floor beside a 6-pack's wall.
/// </summary>

## [h:BrunchArcParityTests-10]
near: `[Test]`

/// <summary>
/// Every slot of a 12-pack lands on one of the two measured radii - there is no third
/// position, which is the stacking property stated as a fact about every slot.
/// </summary>

## [h:BrunchArcParityTests-11]
near: `[Test]`

/// <summary>
/// The cone ceiling stops a large boss at close range wrapping into a mob, which is the whole
/// reason the ceiling exists.
/// </summary>
