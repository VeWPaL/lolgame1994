using System;
using NUnit.Framework;

namespace Depths.Tests
{
    /// <summary>
    /// The depth ladder, checked against the values measured in the JavaScript.
    ///
    /// <para>
    /// These are not written from the formula. Every expected number is a figure that came out of
    /// the running game - the floor-1 through floor-1000 columns, the ceiling, and the three shell
    /// rates - so a change to the formula that nobody re-measures fails here rather than quietly
    /// shipping a different ladder to the Unity build. A port that agrees with its own source is
    /// not parity; a port that agrees with a measurement is.
    /// </para>
    ///
    /// <para>
    /// The one that matters most is <see cref="AGunnerNeverOutrunsThePlayer"/>. Rate scales approach
    /// speed as well as cadence, so a ladder that looks reasonable as a percentage can quietly make
    /// a body faster than the player, which converts a reactive game into a pre-emptive one. That
    /// is the failure the bounded ladder exists to prevent, and it is worth a test of its own rather
    /// than a line inside a loop.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class DepthLadderParityTests
    {
        // measured in the JavaScript, at these floors, with rolled=0. The density column was
        // hand-computed the first time and three of the nine were wrong - 9.5 where the game says
        // 9.0129, 19.3 where it says 19.0467, 25.2 where it says 25.7890. Every figure below was
        // then read out of the running game rather than recomputed, which is the only way a
        // parity table is worth anything.
        [TestCase(1, 1.00, 1.00, 0.0000)]
        [TestCase(2, 1.29, 1.09, 3.8816)]
        [TestCase(5, 1.87, 1.27, 9.0129)]
        [TestCase(10, 2.38, 1.45, 12.8945)]
        [TestCase(20, 2.83, 1.62, 16.7761)]
        [TestCase(30, 3.04, 1.71, 19.0467)]
        [TestCase(50, 3.24, 1.79, 21.9073)]
        [TestCase(100, 3.41, 1.86, 25.7890)]
        [TestCase(1000, 3.58, 1.94, 38.6834)]
        public void TheLadderMatchesTheMeasuredJavaScriptValues(int floor, double tough, double rate, double bodies)
        {
            Assert.That(Balance.DepthTough(floor), Is.EqualTo(tough).Within(0.005),
                "health scaling at floor " + floor);
            Assert.That(Balance.DepthRate(floor), Is.EqualTo(rate).Within(0.005),
                "rate scaling at floor " + floor);
            Assert.That(Balance.DepthBodies(floor, 0), Is.EqualTo(bodies).Within(0.001),
                "density at floor " + floor + " - the JS figure is depthBodies(0), not the whole room");
        }

        [Test]
        public void FloorOneIsAFirstFloor()
        {
            // exactly 1, not approximately. A ladder that starts at 1.0001 has already begun
            // climbing before the player has picked up a controller.
            Assert.That(Balance.DepthTough(1), Is.EqualTo(1.0));
            Assert.That(Balance.DepthRate(1), Is.EqualTo(1.0));
            Assert.That(Balance.DepthBodies(1, 0), Is.EqualTo(0.0));
            // and nothing below the first floor can make it negative
            Assert.That(Balance.DepthTough(0), Is.EqualTo(1.0));
            Assert.That(Balance.DepthTough(-5), Is.EqualTo(1.0));
            Assert.That(Balance.DepthBodies(-5, 3), Is.EqualTo(3.0));
        }

        [Test]
        public void BothDialsStopAtTheirCeiling()
        {
            // the ceiling IS the asymptote, so there is nothing to cap and nothing to clamp
            Assert.That(Balance.DepthTough(1000000), Is.LessThan(1 + Balance.DepthHpGrowth));
            Assert.That(Balance.DepthRate(1000000), Is.LessThan(1 + Balance.DepthRateGrowth));
            Assert.That(Balance.DepthTough(1000000), Is.GreaterThan(1 + Balance.DepthHpGrowth - 0.01),
                "it has to get CLOSE to the ceiling, not merely stay under it, or it is a ramp with a lid");
        }

        [Test]
        public void TheLadderIsMonotoneEverywhere()
        {
            // a ladder that dips somewhere is worse than one that is shallow: descending can make a
            // room easier, and a player who notices will stop descending
            double prevT = 0, prevR = 0;
            for (int f = 1; f <= 500; f++)
            {
                double t = Balance.DepthTough(f), r = Balance.DepthRate(f);
                Assert.That(t, Is.GreaterThanOrEqualTo(prevT), "health dips at floor " + f);
                Assert.That(r, Is.GreaterThanOrEqualTo(prevR), "rate dips at floor " + f);
                prevT = t; prevR = r;
            }
        }

        [Test]
        public void TheStepsShrinkRatherThanGrow()
        {
            // this is the property that makes it a CEILING rather than a slower ramp. A linear
            // ladder has equal steps; a saturating one has steps that get smaller forever.
            double early = Balance.DepthTough(3) - Balance.DepthTough(2);
            double late = Balance.DepthTough(41) - Balance.DepthTough(40);
            Assert.That(early, Is.GreaterThan(late),
                "the per-floor step grows with depth (" + early + " early, " + late +
                " at floor 40), so this is a linear ramp with a small slope and floor 1000 is floor 100");
            double bEarly = Balance.DepthBodies(3, 0) - Balance.DepthBodies(2, 0);
            double bLate = Balance.DepthBodies(41, 0) - Balance.DepthBodies(40, 0);
            Assert.That(bEarly, Is.GreaterThan(bLate), "the density step grows with depth");
        }

        [Test]
        public void AGunnerNeverOutrunsThePlayer()
        {
            // THE fairness property, and the reason the rate dial is bounded at all. A ranged body
            // whose approach speed exceeds the player's cannot be responded to, only anticipated,
            // and anticipating is a different game. Measured across the whole ladder, the gunner's
            // approach tops out at 0.87 px/tick against a player who moves at 1.20.
            for (int f = 1; f <= 1000; f += 1)
            {
                double approach = 0.3 * Balance.PressureRate * Balance.DepthRate(f);
                Assert.That(approach, Is.LessThan(Balance.PlayerMove),
                    "at floor " + f + " a gunner approaches at " + approach +
                    " px/tick against a player who moves at " + Balance.PlayerMove);
            }
        }

        [Test]
        public void ADeepGunnerStaysReadable()
        {
            // the same dial read through the cadence instead of the approach. At the old linear
            // ladder, floor 50 came to 24.8 shells a second.
            for (int f = 1; f <= 1000; f += 1)
            {
                double cdTicks = System.Math.Max(1, Balance.Sec(0.8) / (Balance.PressureRate * Balance.DepthRate(f)));
                double shellsPerSecond = Balance.TickHz / cdTicks;
                Assert.That(shellsPerSecond, Is.LessThan(6.0),
                    "at floor " + f + " a gunner fires " + shellsPerSecond.ToString("F2") +
                    " shells a second, which is not a fight the player can read");
            }
        }

        [Test]
        public void ThePackChanceStops()
        {
            // it is the one dial still on a linear ramp, because its ceiling was the point all along
            Assert.That(Balance.DepthPack(1000, 0.45), Is.EqualTo(Balance.DepthPackCap).Within(1e-9));
            Assert.That(Balance.DepthPack(100000, 0.45), Is.LessThanOrEqualTo(Balance.DepthPackCap));
            Assert.That(Balance.DepthPack(1, 0.45), Is.EqualTo(0.45).Within(1e-9),
                "floor 1 already has the base chance and nothing added to it");
        }

        [Test]
        public void TheLadderNeverReadsThePlayer()
        {
            // There is nothing to assert here that is not in the signature: none of these take a
            // player, a stat, a health number or an item count. That IS the assertion, and it is
            // enforced by the shape rather than by a check - which is the right way to enforce it,
            // because a check can be deleted and a signature cannot be deleted by accident.
            // The discipline is restated here so the reason is on the page next to the thing.
            var tough = typeof(Balance).GetMethod("DepthTough");
            var rate = typeof(Balance).GetMethod("DepthRate");
            var bodies = typeof(Balance).GetMethod("DepthBodies");
            Assert.That(tough, Is.Not.Null, "DepthTough is missing, so the reflection below proves nothing");
            Assert.That(rate, Is.Not.Null, "DepthRate is missing, so the reflection below proves nothing");
            Assert.That(bodies, Is.Not.Null, "DepthBodies is missing, so the reflection below proves nothing");
            Assert.That(tough!.GetParameters().Length, Is.EqualTo(1), "DepthTough takes a floor and nothing else");
            Assert.That(rate!.GetParameters().Length, Is.EqualTo(1), "DepthRate takes a floor and nothing else");
            Assert.That(bodies!.GetParameters().Length, Is.EqualTo(2), "DepthBodies takes a floor and the existing roll");
        }
    }
}
