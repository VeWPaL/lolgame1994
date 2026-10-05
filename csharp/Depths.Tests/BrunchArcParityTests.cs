using Depths;
using NUnit.Framework;

namespace Depths.Tests
{
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
    [TestFixture]
    public sealed class BrunchArcParityTests
    {
        // The measured layout: target at (200,300), player at (400,300), so the bearing is +x and
        // "along" is simply the slot's x offset from the target.
        private static double Along(int n, int slot) =>
            TickOrder.BrunchArcSlot(200, 300, 400, 300, n, slot, 10)!.Value.X - 200;

        private static double Dist(int n, int slot)
        {
            var p = TickOrder.BrunchArcSlot(200, 300, 400, 300, n, slot, 10)!.Value;
            return System.Math.Sqrt((p.X - 200) * (p.X - 200) + (p.Y - 300) * (p.Y - 300));
        }

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
        [TestCase(0, 49.642207661)]
        [TestCase(1, 65.603039328)]
        [TestCase(2, 50.0)]
        [TestCase(3, 67.0)]
        [TestCase(4, 49.642207661)]
        [TestCase(5, 65.603039328)]
        public void ASixPackLaysOutWhereTheGameLaysItOut(int slot, double expectedAlong)
        {
            Assert.That(Along(6, slot), Is.EqualTo(expectedAlong).Within(1e-6));
        }

        [Test]
        public void TheSecondRankStandsSeventeenPixelsBehindTheFirst()
        {
            for (int slot = 0; slot < 6; slot += 2)
            {
                double front = Dist(6, slot);
                double back = Dist(6, slot + 1);
                Assert.That(back - front, Is.EqualTo(Balance.BrunchWallRank).Within(1e-6),
                    "the surplus deepens the wall rather than widening it - a shield that is a "
                    +"single file with stragglers beside it is a queue");
            }
        }

        /// <summary>
        /// A pack too small to have a shape gets null, and the caller walks it straight in. Measured:
        /// a pack of one returns null.
        /// </summary>
        [Test]
        public void APackBelowTheShieldThresholdHasNoArc()
        {
            Assert.That(TickOrder.BrunchArcSlot(200, 300, 400, 300, 1, 0, 10), Is.Null);
            Assert.That(Balance.BrunchShieldMin, Is.EqualTo(2));
            Assert.That(TickOrder.BrunchArcSlot(200, 300, 400, 300, 2, 0, 10), Is.Not.Null);
        }

        /// <summary>
        /// THE STAND-OFF IS A FRACTION OF THE GAP - the measurement that replaced a constant radius.
        /// Measured: with the target 200px from the player the arc forms 50px out, which is a quarter
        /// of the gap. Against the old 118px constant on a 164px gap the wall formed BEHIND the
        /// player.
        /// </summary>
        [Test]
        public void TheArcFormsAQuarterOfTheWayFromTheTargetToThePlayer()
        {
            double gap = 200.0;
            double formed = Along(6, 2);
            Assert.That(formed, Is.EqualTo(gap * Balance.BrunchShieldFrac).Within(1e-6));
            Assert.That(Balance.BrunchShieldFrac, Is.EqualTo(0.25).Within(1e-12));

            // and it is INSIDE the gap, which is the property the old constant violated
            Assert.That(formed, Is.LessThan(gap),
                "a shield that forms beyond the player is a shield on the wrong side of the person "
                +"it is protecting");
        }

        /// <summary>
        /// The fraction is what makes the arc track a CLOSING gap. Measured: a 100px gap forms the arc
        /// 24.55 along and off-axis (the cone is wider at close range, so the outer slots swing
        /// further), while a 500px gap forms it 124.59 along. Both are a quarter of their gap.
        /// </summary>
        [Test]
        public void TheArcTracksAGapThatCloses()
        {
            var close = TickOrder.BrunchArcSlot(200, 300, 300, 300, 6, 0, 10)!.Value;
            var far = TickOrder.BrunchArcSlot(200, 300, 700, 300, 6, 0, 10)!.Value;

            Assert.That(close.X, Is.EqualTo(224.55334).Within(1e-4));
            Assert.That(close.Y, Is.EqualTo(295.29539).Within(1e-4));
            Assert.That(far.X, Is.EqualTo(324.58936).Within(1e-4));
            Assert.That(far.Y, Is.EqualTo(289.87615).Within(1e-4));

            /* Measured: the close arc swings 4.70px off the bearing and the far one 10.12px. So the
               FAR arc is the wider one in absolute terms - because a fixed angular cone covers more
               linear distance further out, which is the opposite of what I asserted when writing this
               test. The angular width is the thing that is constant; the linear spread scales with
               the radius.

               Which is correct for a shield: a distant wall spans enough ground to matter, and a
               point-blank one is compact because there is no room for anything else. */
            Assert.That(System.Math.Abs(close.Y - 300), Is.EqualTo(4.704610).Within(1e-4));
            Assert.That(System.Math.Abs(far.Y - 300), Is.EqualTo(10.123853).Within(1e-4));
            Assert.That(System.Math.Abs(far.Y - 300), Is.GreaterThan(System.Math.Abs(close.Y - 300)),
                "a fixed angular cone covers more ground further out, so the far wall is the wider "
                +"one - measured 10.12 against 4.70");
        }

        /// <summary>The arc is built on the bearing from the target to the player, not on an axis.</summary>
        [Test]
        public void TheArcFollowsTheBearingAndNotTheAxes()
        {
            // Measured: target (200,300), player (400,100) - a bearing of 45 degrees up and right.
            var p = TickOrder.BrunchArcSlot(200, 300, 400, 100, 6, 0, 10)!.Value;
            Assert.That(p.X, Is.EqualTo(245.40263).Within(1e-4));
            Assert.That(p.Y, Is.EqualTo(245.79113).Within(1e-4));

            // it moved TOWARD the player in both axes, which an axis-aligned wall would not do
            Assert.That(p.X, Is.GreaterThan(200));
            Assert.That(p.Y, Is.LessThan(300));
        }

        /// <summary>
        /// A 3-pack is the smallest that walls, and it is asymmetric - the middle body sits on the
        /// centreline and the two outside it clip differently. Measured at full precision:
        /// 49.642207661, 66.32762017, 49.642207661.
        /// </summary>
        [Test]
        public void AThreePackIsMeasuredAndAsymmetric()
        {
            Assert.That(Along(3, 0), Is.EqualTo(49.642207661).Within(1e-6));
            Assert.That(Along(3, 1), Is.EqualTo(66.32762017).Within(1e-6));
            Assert.That(Along(3, 2), Is.EqualTo(49.642207661).Within(1e-6));
            // The rank is a difference in RADIUS, not in the x projection. Measured: a 3-pack's slots
            // sit at radius 50 and 67 from the target, so the back rank is exactly BrunchWallRank
            // further out. Asserting it on `Along` instead - which is the x projection of a chord at
            // the slot's angle - gives 16.685 rather than 17, and looks like a geometry bug.
            Assert.That(Dist(3, 1) - Dist(3, 0), Is.EqualTo(Balance.BrunchWallRank).Within(1e-6));
        }

        /// <summary>
        /// A 12-pack does NOT spread wider than a 6-pack. Measured at full precision: both have their
        /// outer slots at 49.642207661 and their back rank at 65.603039328 - the width is bounded by
        /// the CONE, and the extra bodies stack. This is the assertion that kills the old behaviour,
        /// where a 12-pack put half its bodies in empty floor beside a 6-pack's wall.
        /// </summary>
        [Test]
        public void ATwelvePackIsTheSameWidthAsASixPack()
        {
            double sixOuter = System.Math.Max(Along(6, 0), Along(6, 4));
            double twelveOuter = System.Math.Max(Along(12, 0), Along(12, 10));
            Assert.That(twelveOuter, Is.EqualTo(sixOuter).Within(1e-6),
                "a wider arc for a bigger pack is a pack standing beside its own shield rather than "
                +"behind it");
        }

        /// <summary>
        /// Every slot of a 12-pack lands on one of the two measured radii - there is no third
        /// position, which is the stacking property stated as a fact about every slot.
        /// </summary>
        [Test]
        public void EverySlotOfALargePackLandsOnOneOfTwoRadii()
        {
            var radii = new System.Collections.Generic.HashSet<double>();
            for (int slot = 0; slot < 12; slot++)
                radii.Add(System.Math.Round(Dist(12, slot), 2));
            Assert.That(radii.Count, Is.LessThanOrEqualTo(2),
                "twelve bodies on two ranks is a wall; twelve bodies on twelve positions is a crowd");
        }

        /// <summary>
        /// The cone ceiling stops a large boss at close range wrapping into a mob, which is the whole
        /// reason the ceiling exists.
        /// </summary>
        [Test]
        public void TheConeIsCappedAtSixtyDegrees()
        {
            Assert.That(Balance.BrunchArcCeil, Is.EqualTo(System.Math.PI / 3).Within(1e-12));
            Assert.That(Balance.BrunchArcFloor, Is.EqualTo(System.Math.PI / 20).Within(1e-12));

            // a boss at point-blank range still cannot produce a cone wider than the ceiling
            var bossAtCloseRange = TickOrder.BrunchArcSlot(200, 300, 205, 300, 12, 0, 28)!.Value;
            var centre = TickOrder.BrunchArcSlot(200, 300, 205, 300, 12, 6, 28)!.Value;
            double span = System.Math.Abs(bossAtCloseRange.Y - centre.Y);
            double radius = System.Math.Sqrt((bossAtCloseRange.X - 200) * (bossAtCloseRange.X - 200)
                                          + (bossAtCloseRange.Y - 300) * (bossAtCloseRange.Y - 300));
            Assert.That(System.Math.Atan2(span, radius), Is.LessThanOrEqualTo(Balance.BrunchArcCeil + 1e-9));
        }
    }
}
