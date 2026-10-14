using Depths;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>Parity for TickOrder.BrunchArcSlot - the single function that decides whether a Brunch is a shield or a crowd. [h:BrunchArcParityTests-1]</summary>
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

        /// <summary>A 6-pack's six slots, measured at full precision: 49.642207661, 65.603039328, 50, 67, 49.642207661... [h:BrunchArcParityTests-2]</summary>
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

        /// <summary>A pack too small to have a shape gets null, and the caller walks it straight in. [h:BrunchArcParityTests-3]</summary>
        [Test]
        public void APackBelowTheShieldThresholdHasNoArc()
        {
            Assert.That(TickOrder.BrunchArcSlot(200, 300, 400, 300, 1, 0, 10), Is.Null);
            Assert.That(Balance.BrunchShieldMin, Is.EqualTo(2));
            Assert.That(TickOrder.BrunchArcSlot(200, 300, 400, 300, 2, 0, 10), Is.Not.Null);
        }

        /// <summary>THE STAND-OFF IS A FRACTION OF THE GAP - the measurement that replaced a constant radius. [h:BrunchArcParityTests-4]</summary>
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

        /// <summary>The fraction is what makes the arc track a CLOSING gap. [h:BrunchArcParityTests-5]</summary>
        [Test]
        public void TheArcTracksAGapThatCloses()
        {
            var close = TickOrder.BrunchArcSlot(200, 300, 300, 300, 6, 0, 10)!.Value;
            var far = TickOrder.BrunchArcSlot(200, 300, 700, 300, 6, 0, 10)!.Value;

            Assert.That(close.X, Is.EqualTo(224.55334).Within(1e-4));
            Assert.That(close.Y, Is.EqualTo(295.29539).Within(1e-4));
            Assert.That(far.X, Is.EqualTo(324.58936).Within(1e-4));
            Assert.That(far.Y, Is.EqualTo(289.87615).Within(1e-4));

            // Measured: the close arc swings 4.70px off the bearing and the far one 10.12px. [h:BrunchArcParityTests-6]
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

        /// <summary>A 3-pack is the smallest that walls, and it is asymmetric - the middle body sits on the centreline and the... [h:BrunchArcParityTests-7]</summary>
        [Test]
        public void AThreePackIsMeasuredAndAsymmetric()
        {
            Assert.That(Along(3, 0), Is.EqualTo(49.642207661).Within(1e-6));
            Assert.That(Along(3, 1), Is.EqualTo(66.32762017).Within(1e-6));
            Assert.That(Along(3, 2), Is.EqualTo(49.642207661).Within(1e-6));
            // The rank is a difference in RADIUS, not in the x projection. [h:BrunchArcParityTests-8]
            Assert.That(Dist(3, 1) - Dist(3, 0), Is.EqualTo(Balance.BrunchWallRank).Within(1e-6));
        }

        /// <summary>A 12-pack does NOT spread wider than a 6-pack. [h:BrunchArcParityTests-9]</summary>
        [Test]
        public void ATwelvePackIsTheSameWidthAsASixPack()
        {
            double sixOuter = System.Math.Max(Along(6, 0), Along(6, 4));
            double twelveOuter = System.Math.Max(Along(12, 0), Along(12, 10));
            Assert.That(twelveOuter, Is.EqualTo(sixOuter).Within(1e-6),
                "a wider arc for a bigger pack is a pack standing beside its own shield rather than "
                +"behind it");
        }

        /// <summary>Every slot of a 12-pack lands on one of the two measured radii - there is no third position, which is the... [h:BrunchArcParityTests-10]</summary>
        [Test]
        public void EverySlotOfALargePackLandsOnOneOfTwoRadii()
        {
            var radii = new System.Collections.Generic.HashSet<double>();
            for (int slot = 0; slot < 12; slot++)
                radii.Add(System.Math.Round(Dist(12, slot), 2));
            Assert.That(radii.Count, Is.LessThanOrEqualTo(2),
                "twelve bodies on two ranks is a wall; twelve bodies on twelve positions is a crowd");
        }

        /// <summary>The cone ceiling stops a large boss at close range wrapping into a mob, which is the whole reason the ceiling... [h:BrunchArcParityTests-11]</summary>
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
